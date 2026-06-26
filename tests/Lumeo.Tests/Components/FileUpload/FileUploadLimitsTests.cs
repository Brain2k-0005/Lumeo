using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.IO;

namespace Lumeo.Tests.Components.FileUpload;

// ── Fake IBrowserFile (file-local copy; FileUploadTests' one is file-scoped) ──
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
/// Coverage for FileUpload's <c>MaxFiles</c> cap and the <c>Accept</c> filter
/// (the private <c>IsAccepted</c> predicate, exercised via observable behaviour).
///
/// Selection is simulated the same way as <see cref="FileUploadTests"/>:
/// bUnit can't drive a real <c>&lt;InputFile&gt;</c> file picker, so we build an
/// <see cref="InputFileChangeEventArgs"/> via its internal ctor and invoke the
/// component's private <c>HandleFileSelected</c> directly, then read the private
/// <c>_uploadItems</c> list to assert per-file status. Both the <c>MaxFiles</c>
/// breach and the <c>Accept</c> rejection paths add the offending files as
/// <see cref="Lumeo.FileUpload.FileUploadStatus.Failed"/> items and fire
/// <c>OnFileRejected</c> — that is what these tests pin.
/// </summary>
public class FileUploadLimitsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileUploadLimitsTests()
    {
        // Registers Lumeo services and sets JSInterop to Loose mode (drag/drop
        // interop returns defaults). Matches existing FileUpload tests.
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ── MaxFiles ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Selecting MORE than MaxFiles in one go trips the count check: every
    /// candidate is added as Failed, OnFileRejected fires once per file, and a
    /// global error is shown. No file becomes Pending.
    /// </summary>
    [Fact]
    public async Task Selecting_More_Than_MaxFiles_Rejects_All_Candidates()
    {
        var rejected = new List<string>();

        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Multiple, true)
            .Add(b => b.MaxFiles, 2)
            .Add(b => b.OnFileRejected, EventCallback.Factory.Create<(IBrowserFile File, string Reason)>(
                this, t => rejected.Add(t.File.Name))));

        // 3 files > MaxFiles (2)
        var files = new IBrowserFile[]
        {
            new FakeBrowserFile { Name = "a.txt", Size = 10, ContentType = "text/plain" },
            new FakeBrowserFile { Name = "b.txt", Size = 10, ContentType = "text/plain" },
            new FakeBrowserFile { Name = "c.txt", Size = 10, ContentType = "text/plain" },
        };
        var args = CreateInputFileChangeEventArgs(files);

        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        var items = GetUploadItems(cut);
        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.Equal(Lumeo.FileUpload.FileUploadStatus.Failed, i.Status));
        // OnFileRejected fired once per candidate
        Assert.Equal(new[] { "a.txt", "b.txt", "c.txt" }, rejected);
        // Global error message surfaces the MaxFiles limit
        Assert.Contains("Maximum 2 files allowed.", cut.Markup);
    }

    /// <summary>
    /// MaxFiles counts ACROSS selections: two separate picks that each fit alone
    /// but together exceed MaxFiles must trip the cap on the second pick. The
    /// first (accepted) file stays Pending; the second is rejected.
    /// </summary>
    [Fact]
    public async Task MaxFiles_Counts_Existing_Items_Across_Selections()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Multiple, true)
            .Add(b => b.MaxFiles, 1));

        var first = CreateInputFileChangeEventArgs(
            new FakeBrowserFile { Name = "first.txt", Size = 10, ContentType = "text/plain" });
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, first));

        var second = CreateInputFileChangeEventArgs(
            new FakeBrowserFile { Name = "second.txt", Size = 10, ContentType = "text/plain" });
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, second));
        cut.Render();

        var items = GetUploadItems(cut);
        Assert.Equal(2, items.Count);

        var firstItem = items.Single(i => i.Name == "first.txt");
        var secondItem = items.Single(i => i.Name == "second.txt");

        // 1 existing (non-failed) + 1 new > MaxFiles(1) ⇒ second pick rejected,
        // first one remains pending.
        Assert.Equal(Lumeo.FileUpload.FileUploadStatus.Pending, firstItem.Status);
        Assert.Equal(Lumeo.FileUpload.FileUploadStatus.Failed, secondItem.Status);
    }

    /// <summary>
    /// A custom MaxFilesError overrides the default "Maximum N files allowed."
    /// message on both the rejected items and the global error banner.
    /// </summary>
    [Fact]
    public async Task Custom_MaxFilesError_Used_When_Cap_Exceeded()
    {
        const string custom = "Too many files, slow down.";

        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Multiple, true)
            .Add(b => b.MaxFiles, 1)
            .Add(b => b.MaxFilesError, custom));

        var args = CreateInputFileChangeEventArgs(
            new FakeBrowserFile { Name = "x.txt", Size = 10, ContentType = "text/plain" },
            new FakeBrowserFile { Name = "y.txt", Size = 10, ContentType = "text/plain" });

        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        Assert.Contains(custom, cut.Markup);
        var items = GetUploadItems(cut);
        Assert.All(items, i => Assert.Equal(custom, i.ErrorMessage));
    }

    // ── Accept filter (IsAccepted) ───────────────────────────────────────────

    /// <summary>
    /// With Accept restricting to images, a non-image file is rejected (Failed +
    /// OnFileRejected) while an image passes (Pending). Exercises the two
    /// IsAccepted branches that matter here: the "image/*" wildcard (matches by
    /// content-type prefix) vs. a mismatching content type.
    /// </summary>
    [Fact]
    public async Task Accept_Wildcard_Rejects_NonMatching_And_Passes_Matching()
    {
        var rejected = new List<string>();

        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Multiple, true)
            .Add(b => b.Accept, "image/*")
            .Add(b => b.OnFileRejected, EventCallback.Factory.Create<(IBrowserFile File, string Reason)>(
                this, t => rejected.Add(t.File.Name))));

        var args = CreateInputFileChangeEventArgs(
            new FakeBrowserFile { Name = "photo.png", Size = 10, ContentType = "image/png" },
            new FakeBrowserFile { Name = "notes.pdf", Size = 10, ContentType = "application/pdf" });

        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        var items = GetUploadItems(cut);
        var image = items.Single(i => i.Name == "photo.png");
        var pdf = items.Single(i => i.Name == "notes.pdf");

        Assert.Equal(Lumeo.FileUpload.FileUploadStatus.Pending, image.Status);
        Assert.Equal(Lumeo.FileUpload.FileUploadStatus.Failed, pdf.Status);
        Assert.NotNull(pdf.ErrorMessage);
        Assert.Contains("unsupported", pdf.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "notes.pdf" }, rejected);
    }

    /// <summary>
    /// Accept tokens beginning with "." match by filename extension
    /// (case-insensitively). A ".csv" file passes the ".csv,.txt" filter even
    /// though its content type is empty; a ".png" file is rejected.
    /// </summary>
    [Fact]
    public async Task Accept_Extension_Tokens_Match_By_Filename_CaseInsensitively()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Multiple, true)
            .Add(b => b.Accept, ".csv,.txt"));

        var args = CreateInputFileChangeEventArgs(
            // Uppercase extension + empty content type ⇒ must still match ".csv"
            new FakeBrowserFile { Name = "data.CSV", Size = 10, ContentType = "" },
            new FakeBrowserFile { Name = "image.png", Size = 10, ContentType = "image/png" });

        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        var items = GetUploadItems(cut);
        var csv = items.Single(i => i.Name == "data.CSV");
        var png = items.Single(i => i.Name == "image.png");

        Assert.Equal(Lumeo.FileUpload.FileUploadStatus.Pending, csv.Status);
        Assert.Equal(Lumeo.FileUpload.FileUploadStatus.Failed, png.Status);
    }

    // ── Helpers (mirror FileUploadTests' reflection-based simulation) ─────────

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
            $"Cannot construct InputFileChangeEventArgs. Available constructors: "
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
