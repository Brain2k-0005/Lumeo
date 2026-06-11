using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;

namespace Lumeo.Tests.Components.FileUpload;

/// <summary>
/// Regression tests for dropzone drag&amp;drop: HandleDrop used to be a no-op
/// while the InputFile sat off-screen (sr-only), so dropping a file onto the
/// dropzone did nothing — or navigated the tab away when it missed the input.
/// The InputFile is now stretched invisibly over the whole zone so the
/// browser's NATIVE file-input drop handling adds the files and raises
/// OnChange (bUnit can't simulate a real OS drag, so these tests pin the
/// markup contract plus the highlight state machine).
/// </summary>
public class FileUploadDropzoneTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileUploadDropzoneTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Dropzone_Input_Is_Stretched_Over_The_Zone()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>();

        var input = cut.Find("input[type=file]");
        var cls = input.GetAttribute("class") ?? "";
        Assert.Contains("absolute", cls);
        Assert.Contains("inset-0", cls);
        Assert.Contains("h-full", cls);
        Assert.Contains("w-full", cls);
        Assert.Contains("opacity-0", cls);
        Assert.Contains("cursor-pointer", cls);
        Assert.DoesNotContain("sr-only", cls);
    }

    [Fact]
    public void Dropzone_Label_Is_The_Positioning_Context_For_The_Input()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>();

        var labelClasses = (cut.Find("label").GetAttribute("class") ?? "").Split(' ');
        Assert.Contains("relative", labelClasses);
    }

    [Fact]
    public void Dropzone_Label_Still_Allows_DragOver()
    {
        // @ondragover:preventDefault marks the zone as a valid drop target so
        // the browser shows the copy cursor over the padding as well. (There is
        // deliberately NO @ondrop:preventDefault: Blazor applies the flag to
        // events bubbling from descendants, which would cancel the InputFile's
        // native drop handling.)
        var cut = _ctx.Render<Lumeo.FileUpload>();

        Assert.Contains("ondragover:preventdefault", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ondrop:preventdefault", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Button_Variant_Input_Keeps_SrOnly()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Variant, Lumeo.FileUpload.FileUploadVariant.Button));

        Assert.Contains("sr-only", cut.Find("input[type=file]").GetAttribute("class"));
    }

    [Fact]
    public void Avatar_Variant_Input_Keeps_SrOnly()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Variant, Lumeo.FileUpload.FileUploadVariant.Avatar));

        Assert.Contains("sr-only", cut.Find("input[type=file]").GetAttribute("class"));
    }

    // The idle dropzone contains "hover:border-primary", so check for the
    // standalone "border-primary" class token rather than a substring.
    private bool DropzoneIsHighlighted(IRenderedComponent<Lumeo.FileUpload> cut) =>
        (cut.Find("label").GetAttribute("class") ?? "").Split(' ').Contains("border-primary");

    [Fact]
    public void DragEnter_Highlights_And_Drop_Clears_The_Highlight()
    {
        var cut = _ctx.Render<Lumeo.FileUpload>();

        cut.Find("label").DragEnter(new DragEventArgs());
        Assert.True(DropzoneIsHighlighted(cut));

        cut.Find("label").Drop(new DragEventArgs());
        Assert.False(DropzoneIsHighlighted(cut));
    }

    [Fact]
    public void Nested_DragLeave_Keeps_The_Highlight_Until_The_Zone_Is_Left()
    {
        // Moving from the label onto the overlaid input fires dragenter at the
        // new target BEFORE dragleave at the old one (per the HTML DnD spec),
        // so a plain bool would flicker the highlight off while the pointer is
        // still inside the zone. The handlers count enter/leave pairs instead.
        var cut = _ctx.Render<Lumeo.FileUpload>();

        cut.Find("label").DragEnter(new DragEventArgs()); // enter the label
        cut.Find("label").DragEnter(new DragEventArgs()); // enter the overlaid input (bubbles)
        cut.Find("label").DragLeave(new DragEventArgs()); // leave the label
        Assert.True(DropzoneIsHighlighted(cut));

        cut.Find("label").DragLeave(new DragEventArgs()); // leave the zone entirely
        Assert.False(DropzoneIsHighlighted(cut));
    }
}
