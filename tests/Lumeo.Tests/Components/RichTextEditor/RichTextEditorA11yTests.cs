using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.RichTextEditor;

/// <summary>
/// Battle-wave-2 #61 (keyboard-a11y, medium): the editable surface lacked
/// <c>role=textbox</c> / <c>aria-multiline</c> and was not programmatically tied
/// to its Label, helper, or error text, and <c>aria-required</c>/<c>aria-invalid</c>
/// sat on the presentational wrapper instead of the control.
///
/// bUnit cannot mount the real TipTap contenteditable (the JS module is a loose-mode
/// no-op in the headless DOM), so these tests assert the OBSERVABLE rendered markup:
/// the content-host <c>div</c> (<c>.lumeo-rte-content</c>) now carries the textbox
/// role + aria associations, and the Label / helper / error elements expose stable
/// ids those associations resolve to. No real DOM focus is asserted.
/// </summary>
public class RichTextEditorA11yTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public RichTextEditorA11yTests()
    {
        _ctx.AddLumeoServices();
        // RTE imports its own JS module; keep it loose so init is a graceful no-op.
        _ctx.JSInterop.SetupModule("./_content/Lumeo.Editor/js/rich-text-editor.js")
            .Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // The JS-mounted contenteditable lives as a child of this host; the host carries
    // the same a11y contract in the static markup so it is assertable without TipTap.
    private static IElement Editable(IRenderedComponent<L.RichTextEditor> cut)
        => cut.Find(".lumeo-rte-content");

    [Fact]
    public void Editable_surface_is_a_multiline_textbox()
    {
        var cut = _ctx.Render<L.RichTextEditor>();

        var editable = Editable(cut);
        Assert.Equal("textbox", editable.GetAttribute("role"));
        Assert.Equal("true", editable.GetAttribute("aria-multiline"));
    }

    [Fact]
    public void Required_and_invalid_live_on_the_editable_surface_not_the_wrapper()
    {
        var cut = _ctx.Render<L.RichTextEditor>(p => p
            .Add(e => e.Required, true)
            .Add(e => e.Invalid, true));

        var editable = Editable(cut);
        Assert.Equal("true", editable.GetAttribute("aria-required"));
        Assert.Equal("true", editable.GetAttribute("aria-invalid"));

        // The presentational border/ring wrapper must no longer carry the control
        // state (it is not the thing AT receives as the textbox).
        var wrapper = cut.Find("div.rounded-lg");
        Assert.False(wrapper.HasAttribute("aria-required"));
        Assert.False(wrapper.HasAttribute("aria-invalid"));
    }

    [Fact]
    public void Editable_surface_is_labelled_by_the_rendered_label()
    {
        var cut = _ctx.Render<L.RichTextEditor>(p => p
            .Add(e => e.Label, "Notes"));

        var labelId = cut.Find("label").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(labelId));

        var labelledBy = Editable(cut).GetAttribute("aria-labelledby");
        Assert.Equal(labelId, labelledBy);
    }

    [Fact]
    public void Editable_surface_is_described_by_the_helper_text()
    {
        var cut = _ctx.Render<L.RichTextEditor>(p => p
            .Add(e => e.HelperText, "Markdown supported"));

        var helperId = cut.Find("p.text-muted-foreground").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(helperId));
        Assert.Equal(helperId, Editable(cut).GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Editable_surface_is_described_by_the_error_text_when_invalid()
    {
        var cut = _ctx.Render<L.RichTextEditor>(p => p
            .Add(e => e.Invalid, true)
            .Add(e => e.ErrorText, "Required"));

        var errorId = cut.Find("p.text-destructive").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(errorId));
        Assert.Equal(errorId, Editable(cut).GetAttribute("aria-describedby"));
    }

    [Fact]
    public void No_label_means_no_dangling_aria_labelledby()
    {
        // Without a Label (and outside a FormField) there is nothing to point at,
        // so the attribute must be omitted rather than emitted empty.
        var cut = _ctx.Render<L.RichTextEditor>();
        Assert.False(Editable(cut).HasAttribute("aria-labelledby"));
    }
}
