using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Forms;
using System.IO;
using L = Lumeo;

namespace Lumeo.Tests.Components.FileUpload;

// ── Fake IBrowserFile (file-local; mirrors the other FileUpload test files) ─────
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
/// FileUpload's own keyboard-owned surface is entirely native &lt;button&gt;s (Start
/// upload, per-row Cancel/Retry/Remove) — Enter/Space activation is free via the
/// browser's default button semantics, and bUnit exercises the exact @onclick handler
/// that synthesized click runs (it cannot dispatch a keydown with no registered
/// handler). These tests pin that the Button-variant "Start upload" affordance actually
/// invokes the upload pipeline, that a pending item's Remove action is reachable and
/// works, and that the file-pick input precedes any per-file action button in DOM order
/// — the mechanism that puts "pick a file" before "act on a file" in the Tab sequence.
/// </summary>
public class FileUploadKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public FileUploadKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task Enter_Or_Space_On_Start_Upload_Invokes_The_Upload_Pipeline()
    {
        var uploaded = false;
        var cut = _ctx.Render<L.FileUpload>(p => p
            .Add(f => f.Variant, L.FileUpload.FileUploadVariant.Button)
            .Add(f => f.AutoUpload, false)
            .Add(f => f.OnUpload, (item, progress, ct) =>
            {
                uploaded = true;
                return Task.FromResult<string?>("ok");
            }));

        var args = CreateInputFileChangeEventArgs(new FakeBrowserFile { Name = "a.txt" });
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        // AutoUpload=false: the pending item renders a real <button>"Upload" the
        // browser activates on Enter/Space exactly like a mouse click.
        var startButton = cut.FindAll("button").First(b => b.TextContent.Contains("Upload"));
        await cut.InvokeAsync(() => startButton.Click());

        Assert.True(uploaded);
    }

    [Fact]
    public async Task Remove_Button_Is_Keyboard_Reachable_And_Removes_The_Item()
    {
        var removed = false;
        var cut = _ctx.Render<L.FileUpload>(p => p
            .Add(f => f.OnFileRemoved, (L.FileUpload.FileUploadItem _) => removed = true));

        var args = CreateInputFileChangeEventArgs(new FakeBrowserFile { Name = "a.txt" });
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        var removeButton = cut.Find("button[aria-label='Remove']");
        await cut.InvokeAsync(() => removeButton.Click());

        Assert.True(removed);
    }

    [Fact]
    public async Task File_Pick_Input_Precedes_Any_PerFile_Action_Button_In_DOM_Order()
    {
        var cut = _ctx.Render<L.FileUpload>();

        var args = CreateInputFileChangeEventArgs(new FakeBrowserFile { Name = "a.txt" });
        await cut.InvokeAsync(async () => await TriggerFileSelected(cut, args));
        cut.Render();

        var pickIndex = cut.Markup.IndexOf("type=\"file\"", StringComparison.Ordinal);
        var removeIndex = cut.Markup.IndexOf("aria-label=\"Remove\"", StringComparison.Ordinal);

        Assert.True(pickIndex >= 0 && removeIndex >= 0);
        Assert.True(pickIndex < removeIndex);
    }

    // ── Helpers (mirror FileUploadHighBugRegressionTests) ────────────────────
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
                return (InputFileChangeEventArgs)ctor.Invoke(new object[] { filesList });
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

        throw new InvalidOperationException("Cannot construct InputFileChangeEventArgs.");
    }

    private static Task TriggerFileSelected(IRenderedComponent<L.FileUpload> cut, InputFileChangeEventArgs args)
    {
        var method = typeof(L.FileUpload)
            .GetMethod("HandleFileSelected",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        return (Task)method.Invoke(cut.Instance, new object[] { args })!;
    }
}
