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
/// Battle-wave-2 triage #131/#132/#133 (medium, state-on-data-change) for
/// FileViewer.
///
/// #131 — a stale &lt;img&gt;/child @onerror that fires *after* Src has already
/// advanced to a newer, valid file used to latch the Error state onto that newer
/// Src. The fix tags every load pass with a monotonic generation; the child
/// error callbacks capture the generation at render time and HandleChildError
/// ignores any call whose generation no longer matches the live one.
///
/// #132 — OnParametersSetAsync only compared Src/Kind/MimeType, so a bare
/// MaxCsvRows change on an already-loaded CSV was ignored and the table stayed
/// at the old cap. The fix re-parses the already-fetched bytes when only
/// MaxCsvRows changed.
///
/// #133 — after an error against a stable Src, the parameter-driven path
/// early-out (nothing changed) made the error unrecoverable. The fix adds a
/// public RefreshAsync() (also wired to a Retry button) that re-runs the load.
/// </summary>
public class FileViewerStateOnDataChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileViewerStateOnDataChangeTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // A controllable HttpMessageHandler: each call delegates to the current
    // _responder, so a test can change the response between the first load and a
    // retry. FileViewer resolves an HttpClient registered directly in DI.
    private sealed class StubHandler : HttpMessageHandler
    {
        private Func<HttpRequestMessage, HttpResponseMessage> _responder = _ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) };

        public int CallCount { get; private set; }

        public void RespondWith(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responder(request));
        }
    }

    private static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/csv") };

    private StubHandler RegisterHttpClient()
    {
        var handler = new StubHandler();
        _ctx.Services.AddSingleton(_ => new HttpClient(handler));
        return handler;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #131: a late error from a superseded Src must NOT latch Error onto the new
    // file. We drive the exact race: capture the generation the OLD <img> would
    // have carried, change Src (bumping the generation), then replay that stale
    // error — it must be ignored.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Stale_Image_Error_From_Previous_Src_Does_Not_Latch_Error_On_New_Src()
    {
        // Image kind needs no fetch, so the component settles to Loaded immediately.
        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, "https://cdn.example.com/old.png"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("img")));

        // The generation the first <img>'s @onerror lambda captured at render time.
        var staleGeneration = cut.Instance._loadGeneration;

        // Parent swaps to a different, valid image. This starts a new load pass
        // (generation bumps) and re-renders a fresh <img>.
        cut.Render(p => p.Add(c => c.Src, "https://cdn.example.com/new.png"));

        cut.WaitForAssertion(() =>
            Assert.Equal("https://cdn.example.com/new.png", cut.Find("img").GetAttribute("src")));

        // The OLD image now fails to load and its (stale) @onerror finally fires.
        await cut.InvokeAsync(() => cut.Instance.HandleChildError(staleGeneration, "Image failed to load."));

        // Pre-fix: this flipped the component to the Error panel, hiding the valid
        // new image. Post-fix: the stale generation is ignored, so the new image
        // is still shown and no error panel appears.
        Assert.NotEmpty(cut.FindAll("img"));
        Assert.DoesNotContain("Could not display file", cut.Markup);
    }

    [Fact]
    public async Task Current_Generation_Image_Error_Still_Latches_Error()
    {
        // Guards against the fix over-suppressing: a *current*-generation error
        // must still show the Error panel.
        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, "https://cdn.example.com/broken.png"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("img")));

        await cut.InvokeAsync(() => cut.Instance.HandleChildError(cut.Instance._loadGeneration, "Image failed to load."));

        Assert.Contains("Could not display file", cut.Markup);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #132: a bare MaxCsvRows change (Src unchanged) re-parses the already-fetched
    // CSV so the rendered table reflects the new cap.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MaxCsvRows_Change_With_Unchanged_Src_Reparses_The_Table()
    {
        var handler = RegisterHttpClient();
        var csv = "h\n" + string.Join("\n", Enumerable.Range(1, 20).Select(i => i.ToString()));
        handler.RespondWith(_ => Ok(csv));

        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, "https://files.example.com/data.csv")
            .Add(c => c.MaxCsvRows, 5));

        // Wait for the CSV table to render. Cap=5 → header + 4 data rows shown.
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tbody tr")));
        var rowsAtFiveCap = cut.FindAll("tbody tr").Count;

        // Bare MaxCsvRows change — Src is identical, so no new HTTP request should
        // be issued; the component must re-parse the bytes it already has.
        var callsBefore = handler.CallCount;
        cut.Render(p => p
            .Add(c => c.Src, "https://files.example.com/data.csv")
            .Add(c => c.MaxCsvRows, 50));

        // Pre-fix the OnParametersSetAsync early-out swallowed the change and the
        // table stayed at the old cap. Post-fix more rows appear from the same
        // fetched bytes.
        cut.WaitForAssertion(() => Assert.True(cut.FindAll("tbody tr").Count > rowsAtFiveCap));
        Assert.Equal(callsBefore, handler.CallCount); // no re-fetch
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #133: an error against a stable Src is recoverable via RefreshAsync()/Retry.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_Recovers_From_Error_On_Unchanged_Src()
    {
        var handler = RegisterHttpClient();
        // First load fails (server error); the retry succeeds.
        handler.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom")
        });

        var cut = _ctx.Render<L.FileViewer>(p => p
            .Add(c => c.Src, "https://files.example.com/data.csv")
            .Add(c => c.MaxCsvRows, 50));

        // The failed fetch lands in the Error panel, which now offers a Retry.
        cut.WaitForAssertion(() => Assert.Contains("Could not display file", cut.Markup));
        Assert.Contains("Retry", cut.Markup);

        // Flip the server to succeed, then retry WITHOUT changing Src. Pre-fix the
        // parameter early-out made this impossible (nothing changed → no reload).
        handler.RespondWith(_ => Ok("h\n1\n2\n3"));

        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Could not display file", cut.Markup);
            Assert.NotEmpty(cut.FindAll("tbody tr"));
        });
    }
}
