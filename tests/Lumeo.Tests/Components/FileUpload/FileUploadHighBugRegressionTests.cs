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
/// Regression coverage for the two HIGH-severity FileUpload battle-test bugs:
///
/// (n=2) OnAllUploaded silently never fired when two selection batches overlap.
///   The orchestrator returned early on any superseded batch id, so the LAST
///   batch to finish (whose id had advanced) skipped the terminal check and the
///   callback never ran. Fixed by decoupling the all-done check from the batch
///   id and firing once per round of pending work.
///
/// (n=3) Retry button shown (and re-upload allowed) for size/type/count
///   rejections when the error text is localized or custom. The Retry gate
///   matched English substrings ("exceeds"/"unsupported") in ErrorMessage, so a
///   custom MaxFileSizeError/FileTypeError defeated it. Fixed with an explicit
///   IsValidationRejection discriminator on FileUploadItem.
/// </summary>
public class FileUploadHighBugRegressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileUploadHighBugRegressionTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ── n=2: OnAllUploaded fires even when two batches overlap ────────────────

    /// <summary>
    /// Two AutoUpload selections whose uploads overlap in time. The first
    /// selection's batch finishes LAST (it has the slower upload) — under the old
    /// code its batch id had been superseded by the second selection, so the
    /// terminal check was skipped and OnAllUploaded never fired. With the fix the
    /// callback fires exactly once, after every item is terminal, and reports both
    /// files.
    /// </summary>
    [Fact]
    public async Task OnAllUploaded_Fires_When_Two_Selection_Batches_Overlap()
    {
        var allUploadedCalls = new List<int>(); // records the item count per call

        // Gate the FIRST file so it can be released only after the second batch
        // has already started and (quickly) completed — guaranteeing the batch ids
        // overlap and that the first batch is the last to reach the terminal check.
        var releaseFirst = new TaskCompletionSource();
        var firstStarted = new TaskCompletionSource();

        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Multiple, true)
            .Add(b => b.AutoUpload, true)
            .Add(b => b.OnUpload, async (item, progress, ct) =>
            {
                if (item.Name == "slow.txt")
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task;
                }
                progress.Report(100);
                return $"https://example.com/{item.Name}";
            })
            .Add(b => b.OnAllUploaded, EventCallback.Factory.Create<IReadOnlyList<Lumeo.FileUpload.FileUploadItem>>(
                this, items => allUploadedCalls.Add(items.Count))));

        // Batch A — the slow file. Upload starts and parks on releaseFirst.
        var batchA = CreateInputFileChangeEventArgs(
            new FakeBrowserFile { Name = "slow.txt", Size = 10, ContentType = "text/plain" });
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, batchA));
        await Task.WhenAny(firstStarted.Task, Task.Delay(2000));

        // Batch B — a fast file selected WHILE batch A is still in flight. This
        // bumps _currentBatchId, superseding batch A. Batch B completes promptly.
        var batchB = CreateInputFileChangeEventArgs(
            new FakeBrowserFile { Name = "fast.txt", Size = 10, ContentType = "text/plain" });
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, batchB));
        await Task.Delay(200);

        // Now let batch A finish. It is the LAST batch to reach the terminal check
        // and its id is stale — the old code returned early here and never fired.
        releaseFirst.SetResult();
        await Task.Delay(400);
        cut.Render();

        // Fixed behaviour: OnAllUploaded fired exactly once, with both files, after
        // every item reached a terminal (Succeeded) state.
        Assert.Single(allUploadedCalls);
        Assert.Equal(2, allUploadedCalls[0]);

        var items = GetUploadItems(cut);
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(Lumeo.FileUpload.FileUploadStatus.Succeeded, i.Status));
    }

    // ── n=3: Retry not offered for validation rejections (custom error text) ──

    /// <summary>
    /// A file rejected for size with a CUSTOM MaxFileSizeError (no English
    /// "exceeds" substring). The old gate string-matched ErrorMessage, so the
    /// Retry button leaked through for custom/localized text. The fix keys the
    /// gate on IsValidationRejection, so NO Retry button is rendered — only the
    /// Remove button — even though OnUpload is set.
    /// </summary>
    [Fact]
    public async Task Retry_Not_Shown_For_SizeRejection_With_Custom_Error_Text()
    {
        const string custom = "Datei ist zu groß."; // localized, no "exceeds"

        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.MaxFileSize, 1024)
            .Add(b => b.MaxFileSizeError, custom)
            // OnUpload set => the Retry gate's `OnUpload is not null` part is true,
            // so only the IsValidationRejection discriminator can suppress Retry.
            .Add(b => b.OnUpload, (item, progress, ct) => Task.FromResult<string?>("ok")));

        var big = new FakeBrowserFile { Name = "big.bin", Size = 4096, ContentType = "application/octet-stream" };
        var args = CreateInputFileChangeEventArgs(big);

        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        var item = Assert.Single(GetUploadItems(cut));
        Assert.Equal(Lumeo.FileUpload.FileUploadStatus.Failed, item.Status);
        Assert.True(item.IsValidationRejection);
        Assert.Equal(custom, item.ErrorMessage);

        // No Retry affordance for a validation rejection, even with localized text.
        Assert.Empty(cut.FindAll("button[aria-label='Retry']"));
        // The Remove action is still offered.
        Assert.NotEmpty(cut.FindAll("button[aria-label='Remove']"));
    }

    /// <summary>
    /// A type rejection with a custom FileTypeError (no English "unsupported"
    /// substring) likewise gets no Retry button.
    /// </summary>
    [Fact]
    public async Task Retry_Not_Shown_For_TypeRejection_With_Custom_Error_Text()
    {
        const string custom = "Dateityp nicht erlaubt."; // no "unsupported"

        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Accept, "image/*")
            .Add(b => b.FileTypeError, custom)
            .Add(b => b.OnUpload, (item, progress, ct) => Task.FromResult<string?>("ok")));

        var pdf = new FakeBrowserFile { Name = "doc.pdf", Size = 10, ContentType = "application/pdf" };
        var args = CreateInputFileChangeEventArgs(pdf);

        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        var item = Assert.Single(GetUploadItems(cut));
        Assert.True(item.IsValidationRejection);
        Assert.Empty(cut.FindAll("button[aria-label='Retry']"));
    }

    /// <summary>
    /// Sanity counter-case: a genuine OnUpload FAILURE (not a validation
    /// rejection) still offers Retry. This pins that the fix narrowed the gate to
    /// validation rejections only and did not suppress retry for real upload
    /// failures.
    /// </summary>
    [Fact]
    public async Task Retry_Still_Shown_For_Genuine_Upload_Failure()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.AutoUpload, true)
            .Add(b => b.OnUpload, (item, progress, ct) =>
                throw new InvalidOperationException("server exploded")));

        var ok = new FakeBrowserFile { Name = "ok.txt", Size = 10, ContentType = "text/plain" };
        var args = CreateInputFileChangeEventArgs(ok);

        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        await Task.Delay(300);
        cut.Render();

        var item = Assert.Single(GetUploadItems(cut));
        Assert.Equal(Lumeo.FileUpload.FileUploadStatus.Failed, item.Status);
        Assert.False(item.IsValidationRejection); // a real upload failure
        Assert.NotEmpty(cut.FindAll("button[aria-label='Retry']"));
    }

    // ── Helpers (mirror FileUploadTests/FileUploadLimitsTests) ────────────────

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

    private static List<Lumeo.FileUpload.FileUploadItem> GetUploadItems(IRenderedComponent<Lumeo.FileUpload> cut)
    {
        var field = typeof(Lumeo.FileUpload)
            .GetField("_uploadItems",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (List<Lumeo.FileUpload.FileUploadItem>)field.GetValue(cut.Instance)!;
    }
}
