using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using System.IO;

namespace Lumeo.Tests.Components.FileUpload;

/// <summary>
/// Regression tests for the <see cref="Lumeo.FileUpload.ShowFileList"/> parameter and
/// the public <see cref="Lumeo.FileUpload.Reset"/> method, introduced for immediate-upload
/// flows where the consumer renders its own file list and the built-in remove-X card
/// would linger misleadingly.
/// </summary>
public class FileUploadShowFileListResetTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileUploadShowFileListResetTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ── ShowFileList=false ───────────────────────────────────────────────────

    /// <summary>
    /// When ShowFileList=false, no internal file card is rendered after selection,
    /// but OnFilesSelected still fires — proving the event pipeline is unaffected.
    /// </summary>
    [Fact]
    public async Task ShowFileList_False_HidesCard_ButSelectionEventFires()
    {
        var selectionFired = 0;

        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.ShowFileList, false)
            .Add(b => b.OnFilesSelected, EventCallback.Factory.Create<InputFileChangeEventArgs>(
                this, _ => selectionFired++)));

        var fakeFile = MakeFile("photo.png");
        var args = MakeArgs(fakeFile);
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        // The selection event must have fired.
        Assert.Equal(1, selectionFired);

        // The internal upload item must exist in component state (upload pipeline is live).
        var items = GetUploadItems(cut);
        Assert.Single(items);
        Assert.Equal("photo.png", items[0].Name);

        // But no file card should be present in the rendered DOM.
        // The card row has a distinctive border class; assert it is absent.
        var cardRows = cut.FindAll("[class*='border-border']");
        Assert.Empty(cardRows);
    }

    /// <summary>
    /// Default behavior is unchanged: ShowFileList=true (implicit) renders the card.
    /// </summary>
    [Fact]
    public async Task ShowFileList_Default_True_RendersCard()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>();

        var fakeFile = MakeFile("report.pdf");
        var args = MakeArgs(fakeFile);
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        // File name must appear in the card.
        Assert.Contains("report.pdf", cut.Markup);
        // And the card border element must exist.
        Assert.NotEmpty(cut.FindAll("[class*='border-border']"));
    }

    // ── Reset() ─────────────────────────────────────────────────────────────

    /// <summary>
    /// After Reset(), _uploadItems is empty and _inputFileKey has incremented,
    /// confirming the native input will be remounted for re-pick.
    /// </summary>
    [Fact]
    public async Task Reset_ClearsUploadItems_And_IncrementsInputKey()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>();

        var fakeFile = MakeFile("doc.docx");
        var args = MakeArgs(fakeFile);
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        Assert.Single(GetUploadItems(cut));
        var keyBefore = GetInputFileKey(cut);

        // Reset via the public API.
        await cut.InvokeAsync(async () => await cut.Instance.Reset());
        cut.Render();

        Assert.Empty(GetUploadItems(cut));
        Assert.True(GetInputFileKey(cut) > keyBefore, "Reset() must increment _inputFileKey so the native input remounts.");
    }

    /// <summary>
    /// After Reset(), a subsequent HandleFileSelected with the same file name fires
    /// OnFilesSelected again and adds a new upload item — simulating the re-pick
    /// scenario that would be blocked without the DOM reset.
    /// </summary>
    [Fact]
    public async Task Reset_AllowsSubsequentSelectionEventToFire()
    {
        var selectionCount = 0;

        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.OnFilesSelected, EventCallback.Factory.Create<InputFileChangeEventArgs>(
                this, _ => selectionCount++)));

        var fakeFile = MakeFile("avatar.jpg");
        var args = MakeArgs(fakeFile);

        // First pick.
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();
        Assert.Equal(1, selectionCount);

        // Reset and pick again (same file name).
        await cut.InvokeAsync(async () => await cut.Instance.Reset());
        Assert.Empty(GetUploadItems(cut));

        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        Assert.Equal(2, selectionCount);
        Assert.Single(GetUploadItems(cut));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static FakeBrowserFileForReset MakeFile(string name) =>
        new() { Name = name };

    private static InputFileChangeEventArgs MakeArgs(IBrowserFile file)
    {
        var allFlags = System.Reflection.BindingFlags.NonPublic
                     | System.Reflection.BindingFlags.Public
                     | System.Reflection.BindingFlags.Instance;
        var filesList = (IReadOnlyList<IBrowserFile>)new List<IBrowserFile> { file }.AsReadOnly();
        foreach (var ctor in typeof(InputFileChangeEventArgs).GetConstructors(allFlags))
        {
            var ps = ctor.GetParameters();
            if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(filesList.GetType()))
                return (InputFileChangeEventArgs)ctor.Invoke(new object[] { filesList });
        }
        foreach (var ctor in typeof(InputFileChangeEventArgs).GetConstructors(allFlags))
        {
            if (ctor.GetParameters().Length == 1)
            {
                try { return (InputFileChangeEventArgs)ctor.Invoke(new object[] { filesList }); }
                catch { /* try next */ }
            }
        }
        throw new InvalidOperationException("Cannot construct InputFileChangeEventArgs.");
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

    private static int GetInputFileKey(IRenderedComponent<Lumeo.FileUpload> cut)
    {
        var field = typeof(Lumeo.FileUpload)
            .GetField("_inputFileKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (int)field.GetValue(cut.Instance)!;
    }
}

internal sealed class FakeBrowserFileForReset : IBrowserFile
{
    public string Name { get; init; } = "test.txt";
    public DateTimeOffset LastModified { get; init; } = DateTimeOffset.UtcNow;
    public long Size { get; init; } = 100;
    public string ContentType { get; init; } = "text/plain";

    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        => new MemoryStream(new byte[Size]);
}
