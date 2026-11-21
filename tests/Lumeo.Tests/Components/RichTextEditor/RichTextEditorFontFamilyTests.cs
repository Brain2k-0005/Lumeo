using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services.Interop;
using L = Lumeo;

namespace Lumeo.Tests.Components.RichTextEditor;

/// <summary>
/// #554 — font-family dropdown on the RichTextEditor toolbar. Covers the Fonts
/// parameter, which presets get the dropdown, the command sent to JS, and the active
/// font reflected back from the editor.
/// </summary>
public class RichTextEditorFontFamilyTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Editor/js/rich-text-editor.js";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
        _module.Setup<string>("rte.init", _ => true).SetResult("rte-instance-1");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.EditorToolbar> RenderToolbar(
        L.EditorToolbarPreset preset,
        IReadOnlyList<L.EditorFont>? fonts = null,
        RichTextActiveState? active = null,
        Action<(string, object?)>? onCommand = null)
        => _ctx.Render<L.EditorToolbar>(p =>
        {
            p.Add(c => c.Preset, preset);
            if (fonts is not null) p.Add(c => c.Fonts, fonts);
            if (active is not null) p.Add(c => c.Active, active);
            if (onCommand is not null)
                p.Add(c => c.OnCommand, Microsoft.AspNetCore.Components.EventCallback.Factory
                    .Create<(string command, object? arg)>(this, c => onCommand(c)));
        });

    [Fact]
    public void Full_preset_renders_font_dropdown()
        => Assert.NotNull(RenderToolbar(L.EditorToolbarPreset.Full).Find("[role='combobox'][aria-label='Font family']"));

    [Theory]
    [InlineData(L.EditorToolbarPreset.Minimal)]
    [InlineData(L.EditorToolbarPreset.Standard)]
    public void Lean_presets_do_not_render_font_dropdown(L.EditorToolbarPreset preset)
        => Assert.Empty(RenderToolbar(preset).FindAll("[aria-label='Font family']"));

    [Fact]
    public void Empty_fonts_list_hides_dropdown()
        => Assert.Empty(RenderToolbar(L.EditorToolbarPreset.Full, fonts: Array.Empty<L.EditorFont>())
            .FindAll("[aria-label='Font family']"));

    [Fact]
    public void Default_font_list_is_used_when_Fonts_is_null_and_starts_with_clear_entry()
    {
        Assert.True(L.EditorFonts.Default[0].IsDefault);
        Assert.Contains(L.EditorFonts.Default, f => f.Label == "Georgia" && f.Value == "Georgia, serif");

        var cut = RenderToolbar(L.EditorToolbarPreset.Full);
        cut.Find("[role='combobox']").Click();
        Assert.Equal(L.EditorFonts.Default.Count, cut.FindAll("button[role='option']").Count);
    }

    [Fact]
    public void Custom_fonts_are_listed_and_styled_with_their_family()
    {
        var fonts = new[] { new L.EditorFont("Default"), new L.EditorFont("Inter", "\"Inter\", sans-serif") };
        var cut = RenderToolbar(L.EditorToolbarPreset.Full, fonts);
        cut.Find("[role='combobox']").Click();

        var options = cut.FindAll("button[role='option']");
        Assert.Equal(2, options.Count);
        Assert.Contains("Inter", options[1].TextContent);
        Assert.Contains("font-family: \"Inter\", sans-serif", options[1].InnerHtml.Replace("&quot;", "\""));
    }

    [Fact]
    public void Picking_a_font_raises_setFontFamily_with_its_css_value()
    {
        (string, object?)? got = null;
        var cut = RenderToolbar(L.EditorToolbarPreset.Full, onCommand: c => got = c);
        cut.Find("[role='combobox']").Click();
        var idx = L.EditorFonts.Default.ToList().FindIndex(f => f.Label == "Georgia");
        cut.FindAll("button[role='option']")[idx].Click();

        Assert.Equal("setFontFamily", got!.Value.Item1);
        Assert.Equal("Georgia, serif", got.Value.Item2);
    }

    [Fact]
    public void Picking_Default_raises_setFontFamily_with_empty_value_to_clear()
    {
        (string, object?)? got = null;
        var active = new RichTextActiveState { FontFamily = "Georgia, serif" };
        var cut = RenderToolbar(L.EditorToolbarPreset.Full, active: active, onCommand: c => got = c);
        cut.Find("[role='combobox']").Click();
        cut.FindAll("button[role='option']")[0].Click();

        Assert.Equal("setFontFamily", got!.Value.Item1);
        Assert.Equal(string.Empty, got.Value.Item2);
    }

    [Fact]
    public void Active_font_is_shown_on_trigger_even_when_browser_normalised_the_quotes()
    {
        // The browser re-serialises font-family (e.g. adds double quotes); the dropdown
        // must still resolve it to the configured entry.
        var active = new RichTextActiveState { FontFamily = "\"Times New Roman\", Times, serif" };
        var cut = RenderToolbar(L.EditorToolbarPreset.Full, active: active);
        Assert.Contains("Times New Roman", cut.Find("[role='combobox']").TextContent);
    }

    [Fact]
    public void Editor_forwards_Fonts_and_dispatches_setFontFamily_to_the_module()
    {
        var fonts = new[] { new L.EditorFont("Default"), new L.EditorFont("Serif", "serif") };
        var cut = _ctx.Render<L.RichTextEditor>(p => p
            .Add(c => c.Toolbar, L.EditorToolbarPreset.Full)
            .Add(c => c.Fonts, fonts));

        cut.Find("[role='combobox'][aria-label='Font family']").Click();
        cut.FindAll("button[role='option']")[1].Click();

        Assert.Contains(_module.Invocations, i =>
            i.Identifier == "rte.command" && i.Arguments.Contains("setFontFamily") && i.Arguments.Contains("serif"));
    }
}
