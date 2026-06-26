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
/// Regression coverage for the MEDIUM state-on-data-change battle-test bug
/// (n=34): "MaxConcurrentUploads change is ignored after the first upload
/// (latched semaphore)".
///
/// The orchestrator built its concurrency gate with
/// <c>_concurrencySemaphore ??= new SemaphoreSlim(MaxConcurrentUploads, ...)</c>.
/// A SemaphoreSlim's capacity is fixed at construction, and the <c>??=</c>
/// latched the instance forever — so after the first upload, raising (or
/// lowering) <see cref="Lumeo.FileUpload.MaxConcurrentUploads"/> at runtime had
/// no effect: subsequent batches kept the original concurrency limit.
///
/// The fix tracks the value the live semaphore was built with
/// (<c>_concurrencySemaphoreCount</c>) and rebuilds the semaphore when
/// MaxConcurrentUploads changes while nothing is in flight.
/// </summary>
public class FileUploadConcurrencyRegressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileUploadConcurrencyRegressionTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    /// <summary>
    /// Run a first batch with MaxConcurrentUploads=1 (builds the semaphore with a
    /// capacity of 1), then re-render with MaxConcurrentUploads=3 and run a second
    /// batch. Under the latched-semaphore bug the gate stayed at 1; with the fix
    /// the semaphore is rebuilt so the new limit (3) takes effect.
    ///
    /// The MECHANISM is asserted: the value the live semaphore was built with
    /// (_concurrencySemaphoreCount) tracks the updated parameter, and a gated
    /// second batch is observed running 3 uploads concurrently.
    /// </summary>
    [Fact]
    public async Task MaxConcurrentUploads_Change_Is_Applied_To_Later_Batches()
    {
        // Gate each upload so we can observe how many run at once. OnUpload parks on
        // a per-batch release TCS while recording the live concurrency peak.
        var live = 0;
        var peak = 0;
        var sync = new object();
        var release = new TaskCompletionSource();

        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Multiple, true)
            .Add(b => b.AutoUpload, true)
            .Add(b => b.MaxConcurrentUploads, 1)
            .Add(b => b.OnUpload, async (item, progress, ct) =>
            {
                int now;
                lock (sync) { now = ++live; if (now > peak) peak = now; }
                await release.Task;
                lock (sync) { live--; }
                progress.Report(100);
                return $"https://example.com/{item.Name}";
            }));

        // ── First batch with limit = 1. Let it complete so nothing is in flight. ──
        var batchA = CreateInputFileChangeEventArgs(
            new FakeBrowserFile { Name = "a.txt", Size = 10, ContentType = "text/plain" });
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, batchA));
        release.SetResult();                       // release the single upload
        await Task.Delay(200);
        cut.Render();

        // The semaphore was built with the original limit of 1.
        Assert.Equal(1, GetSemaphoreCount(cut));

        // ── Raise the limit to 3 via a re-render (NOT SetParametersAndRender). ──
        cut.Render(p => p.Add(b => b.MaxConcurrentUploads, 3));

        // ── Second batch of 3 files, gated so they pile up concurrently. ──
        lock (sync) { peak = 0; }
        var release2 = new TaskCompletionSource();
        release = release2;

        var batchB = CreateInputFileChangeEventArgs(
            new FakeBrowserFile { Name = "b1.txt", Size = 10, ContentType = "text/plain" },
            new FakeBrowserFile { Name = "b2.txt", Size = 10, ContentType = "text/plain" },
            new FakeBrowserFile { Name = "b3.txt", Size = 10, ContentType = "text/plain" });
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, batchB));
        await Task.Delay(300);                     // give all three time to start

        // Mechanism: the live semaphore was rebuilt with the NEW limit. Under the
        // latched-semaphore bug this would still read 1.
        Assert.Equal(3, GetSemaphoreCount(cut));

        // Functional confirmation: all three uploads ran at the same time.
        int observedPeak;
        lock (sync) { observedPeak = peak; }
        Assert.Equal(3, observedPeak);

        release2.SetResult();                       // let the batch drain
        await Task.Delay(200);
        cut.Render();
    }

    // ── Helpers (mirror FileUploadHighBugRegressionTests) ─────────────────────

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

    // The value the live concurrency semaphore was built with. This is the
    // discriminator the fix introduced — it tracks MaxConcurrentUploads instead of
    // being latched at the first-batch value.
    private static int GetSemaphoreCount(IRenderedComponent<Lumeo.FileUpload> cut)
    {
        var field = typeof(Lumeo.FileUpload)
            .GetField("_concurrencySemaphoreCount",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (int)field.GetValue(cut.Instance)!;
    }
}
