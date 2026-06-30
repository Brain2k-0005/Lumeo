using System.Net;
using System.Net.Http;
using System.Text;
using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.FileViewerComponent;

/// <summary>
/// Battle-wave-2 triage #210 (low, lifecycle) for FileViewer.
///
/// ResolveAndPrepareAsync runs a multi-await load pass (kind detection →
/// OnKindDetected callback → text fetch → final StateHasChanged). If the
/// component is disposed *while that pass is in flight*, the pass would
/// previously keep going past its awaits and call OnLoaded / StateHasChanged()
/// against a torn-down component — an ObjectDisposedException. The only guard
/// was a catch for OperationCanceledException, which never fires when the awaited
/// step doesn't observe the (now-cancelled) token.
///
/// The fix adds a private _disposed flag (set in DisposeAsync) and, after every
/// await, returns early when `token.IsCancellationRequested || _disposed` before
/// invoking callbacks or re-rendering.
/// </summary>
public class FileViewerLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileViewerLifecycleTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // A handler whose response is gated on a TaskCompletionSource the test
    // controls, so the fetch can be suspended *inside* SendAsync until we
    // deliberately release it — letting us dispose the component mid-fetch and
    // then complete the request. It does NOT observe the cancellation token, so
    // the only thing standing between the resumed pass and a StateHasChanged()
    // against the disposed component is the new _disposed guard.
    private sealed class GatedHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<bool> _gate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _gate.TrySetResult(true);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await _gate.Task;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("h\n1\n2\n3", Encoding.UTF8, "text/csv"),
            };
        }
    }

    private GatedHandler RegisterGatedHttpClient()
    {
        var handler = new GatedHandler();
        _ctx.Services.AddSingleton(_ => new HttpClient(handler));
        return handler;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #210: disposing the component while a text fetch is in flight, then letting
    // the fetch complete, must NOT throw (no StateHasChanged()/OnLoaded against a
    // torn-down component). Pre-fix the resumed pass re-rendered the disposed
    // component and threw ObjectDisposedException; post-fix the _disposed guard
    // short-circuits before any post-await work.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Disposing_During_In_Flight_Fetch_Does_Not_Throw()
    {
        var handler = RegisterGatedHttpClient();

        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, $"https://files.example.com/data-{Guid.NewGuid():N}.csv")
            .Add(c => c.MaxCsvRows, 50));

        // The fetch is suspended inside the gated handler — the component sits in
        // the Detecting/Fetching state, which renders a Spinner (aria-busy).
        cut.WaitForAssertion(() => Assert.Contains("aria-busy=\"true\"", cut.Markup));

        var exception = await Record.ExceptionAsync(async () =>
        {
            // Tear the component down while the fetch is still pending. This
            // cancels _cts and sets _disposed = true.
            await cut.Instance.DisposeAsync();

            // Now let the in-flight fetch complete. The load pass resumes past its
            // await and reaches the post-await guard + final StateHasChanged().
            handler.Release();

            // Give the resumed continuation a chance to run on the renderer.
            await Task.Delay(50);
        });

        Assert.Null(exception);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Guards against the fix over-suppressing on the normal path: with no
    // disposal, the same gated fetch must still complete and OnLoaded must fire.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Normal_Fetch_Still_Completes_And_Fires_OnLoaded()
    {
        var handler = RegisterGatedHttpClient();

        var loadedFired = false;

        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, $"https://files.example.com/data-{Guid.NewGuid():N}.csv")
            .Add(c => c.MaxCsvRows, 50)
            .Add(c => c.OnLoaded, () => loadedFired = true));

        cut.WaitForAssertion(() => Assert.Contains("aria-busy=\"true\"", cut.Markup));

        // Release the fetch on the renderer's synchronization context so the
        // resumed load pass settles cleanly.
        await cut.InvokeAsync(() => handler.Release());

        cut.WaitForAssertion(() =>
        {
            Assert.True(loadedFired);
            Assert.NotEmpty(cut.FindAll("tbody tr"));
        });
    }
}
