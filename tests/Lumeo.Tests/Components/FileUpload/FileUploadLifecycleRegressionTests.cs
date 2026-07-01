using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.IO;

namespace Lumeo.Tests.Components.FileUpload;

// ── Fake IBrowserFile (file-local; the other FileUpload test files have their
//    own file-scoped copies) ────────────────────────────────────────────────
file sealed class FakeBrowserFile : IBrowserFile
{
    public string Name { get; init; } = "test.txt";
    public DateTimeOffset LastModified { get; init; } = DateTimeOffset.UtcNow;
    public long Size { get; init; } = 100;
    public string ContentType { get; init; } = "text/plain";

    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        => new MemoryStream(new byte[Size]);
}

/// <summary>
/// Regression coverage for the LIFECYCLE battle-test bug (n=151):
/// "Progress&lt;int&gt; callback can call InvokeAsync(StateHasChanged) on a
/// disposed renderer".
///
/// A consumer's <see cref="Lumeo.FileUpload.OnUpload"/> Task — and the
/// <see cref="IProgress{T}"/> it holds — can keep running and reporting progress
/// (or completing successfully / failing) AFTER the component has been disposed.
/// The fire-and-forget <c>InvokeAsync(StateHasChanged)</c> in the progress
/// handler, the success/fail handlers, the per-item finally block and the
/// terminal all-uploaded check would then dispatch a render against a torn-down
/// renderer, throwing <see cref="ObjectDisposedException"/> and surfacing as an
/// unobserved task exception that can crash the circuit.
///
/// The fix sets a <c>_disposed</c> flag at the start of DisposeAsync and routes
/// those late dispatches through a guarded helper that short-circuits (and
/// swallows the ObjectDisposedException race) once the component is gone, while
/// still raising the consumer callbacks (OnFileUploaded/OnFileFailed/
/// OnAllUploaded) so completion is observed.
/// </summary>
public class FileUploadLifecycleRegressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileUploadLifecycleRegressionTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // Each test disposes the component-under-test itself (that IS the scenario);
    // disposing the context afterwards is idempotent against the already-disposed
    // FileUpload (its DisposeAsync clears _cancellations and nulls the semaphore).
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>
    /// Start an AutoUpload whose OnUpload parks on a TCS, dispose the component,
    /// THEN release the upload so its progress report and success dispatch fire
    /// after teardown. Without the _disposed guard the fire-and-forget
    /// InvokeAsync(StateHasChanged) dispatches throw ObjectDisposedException; with
    /// the fix they short-circuit and the whole sequence completes cleanly.
    /// </summary>
    [Fact]
    public async Task Progress_And_Completion_After_Dispose_Do_Not_Throw()
    {
        var release = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        var reportedAfterRelease = new TaskCompletionSource();

        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Multiple, false)
            .Add(b => b.AutoUpload, true)
            .Add(b => b.OnUpload, async (item, progress, ct) =>
            {
                started.TrySetResult();
                await release.Task;            // park until AFTER dispose
                // These run post-dispose: the progress report goes through the
                // fire-and-forget guarded dispatch, and the success return triggers
                // the OnFileUploaded + StateHasChanged dispatch.
                progress.Report(50);
                progress.Report(100);
                reportedAfterRelease.TrySetResult();
                return $"https://example.com/{item.Name}";
            }));

        var batch = CreateInputFileChangeEventArgs(
            new FakeBrowserFile { Name = "a.txt", Size = 10, ContentType = "text/plain" });
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, batch));

        // Wait until the upload is actually parked inside OnUpload.
        await started.Task;

        // Dispose the component while the upload is still in flight. This sets the
        // internal _disposed flag (the fix) and cancels the in-flight token.
        await cut.Instance.DisposeAsync();

        // Now let the upload finish — every post-await dispatch (progress, success,
        // finally, terminal) now races against a disposed renderer.
        var ex = await Record.ExceptionAsync(async () =>
        {
            release.SetResult();
            await reportedAfterRelease.Task;   // OnUpload body ran to completion
            await Task.Delay(150);             // let the queued dispatches drain
        });

        Assert.Null(ex);
    }

    /// <summary>
    /// Confirms the mechanism directly: DisposeAsync sets the private _disposed
    /// flag (the discriminator the fix introduced). Without it none of the
    /// post-await dispatch guards would short-circuit.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_Sets_Disposed_Flag()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.AutoUpload, true)
            .Add(b => b.OnUpload, (item, progress, ct) =>
                Task.FromResult<string?>("https://example.com/x")));

        Assert.False(GetDisposedFlag(cut));

        await cut.Instance.DisposeAsync();

        Assert.True(GetDisposedFlag(cut));
    }

    /// <summary>
    /// A failing upload that throws AFTER dispose must not bubble an
    /// ObjectDisposedException out of the catch's render dispatch.
    /// </summary>
    [Fact]
    public async Task Failure_After_Dispose_Does_Not_Throw()
    {
        var release = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        var threwAfterRelease = new TaskCompletionSource();

        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.AutoUpload, true)
            .Add(b => b.OnUpload, (Func<Lumeo.FileUpload.FileUploadItem, IProgress<int>, CancellationToken, Task<string?>>)(async (item, progress, ct) =>
            {
                started.TrySetResult();
                await release.Task;
                threwAfterRelease.TrySetResult();
                throw new InvalidOperationException("boom after dispose");
            })));

        var batch = CreateInputFileChangeEventArgs(
            new FakeBrowserFile { Name = "f.txt", Size = 10, ContentType = "text/plain" });
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, batch));

        await started.Task;
        await cut.Instance.DisposeAsync();

        var ex = await Record.ExceptionAsync(async () =>
        {
            release.SetResult();
            await threwAfterRelease.Task;
            await Task.Delay(150);
        });

        Assert.Null(ex);
    }

    // ── Helpers (mirror FileUploadConcurrencyRegressionTests) ─────────────────

    private static InputFileChangeEventArgs CreateInputFileChangeEventArgs(params IBrowserFile[] files)
    {
        var allFlags = System.Reflection.BindingFlags.NonPublic
                      | System.Reflection.BindingFlags.Public
                      | System.Reflection.BindingFlags.Instance;

        var ctors = typeof(InputFileChangeEventArgs).GetConstructors(allFlags);
        var filesList = (IReadOnlyList<IBrowserFile>)files.ToList().AsReadOnly();

        foreach (var ctor in ctors)
        {
            var ps = ctor.GetParameters();
            if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(filesList.GetType()))
            {
                return (InputFileChangeEventArgs)ctor.Invoke(new object[] { filesList });
            }
        }

        foreach (var ctor in ctors)
        {
            var ps = ctor.GetParameters();
            if (ps.Length == 1)
            {
                try { return (InputFileChangeEventArgs)ctor.Invoke(new object[] { filesList }); }
                catch { /* try next */ }
            }
        }

        throw new InvalidOperationException(
            "Cannot construct InputFileChangeEventArgs. Available constructors: "
            + string.Join(", ", ctors.Select(c => string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name)))));
    }

    private static Task TriggerFileSelected(IRenderedComponent<Lumeo.FileUpload> cut, InputFileChangeEventArgs args)
    {
        var method = typeof(Lumeo.FileUpload)
            .GetMethod("HandleFileSelected",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        return (Task)method.Invoke(cut.Instance, new object[] { args })!;
    }

    // The disposed discriminator the fix introduced — guards every post-await
    // StateHasChanged dispatch in the upload pipeline.
    private static bool GetDisposedFlag(IRenderedComponent<Lumeo.FileUpload> cut)
    {
        var field = typeof(Lumeo.FileUpload)
            .GetField("_disposed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (bool)field.GetValue(cut.Instance)!;
    }
}
