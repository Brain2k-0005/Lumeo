using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.RichTextEditor;

/// <summary>
/// #320 — close StarterKit-exposure gaps in the toolbar. Inline code and
/// blockquote are core TipTap StarterKit marks/nodes whose commands + active
/// states were already wired, but the buttons only showed on the Full preset.
/// They now appear on Standard too; Minimal stays deliberately lean.
/// </summary>
public class EditorToolbarPresetTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public EditorToolbarPresetTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.EditorToolbar> RenderToolbar(L.EditorToolbarPreset preset)
        => _ctx.Render<L.EditorToolbar>(p => p.Add(c => c.Preset, preset));

    [Fact]
    public void Standard_Preset_Exposes_Inline_Code()
    {
        var cut = RenderToolbar(L.EditorToolbarPreset.Standard);
        Assert.NotNull(cut.Find("[aria-label='Inline code']"));
    }

    [Fact]
    public void Standard_Preset_Exposes_Blockquote()
    {
        var cut = RenderToolbar(L.EditorToolbarPreset.Standard);
        Assert.NotNull(cut.Find("[aria-label='Quote']"));
    }

    [Fact]
    public void Minimal_Preset_Stays_Lean_No_Code_Or_Quote()
    {
        var cut = RenderToolbar(L.EditorToolbarPreset.Minimal);
        Assert.Empty(cut.FindAll("[aria-label='Inline code']"));
        Assert.Empty(cut.FindAll("[aria-label='Quote']"));
    }

    [Fact]
    public void Full_Preset_Still_Has_Code_Quote_And_CodeBlock()
    {
        var cut = RenderToolbar(L.EditorToolbarPreset.Full);
        Assert.NotNull(cut.Find("[aria-label='Inline code']"));
        Assert.NotNull(cut.Find("[aria-label='Quote']"));
        Assert.NotNull(cut.Find("[aria-label='Code block']"));
    }

    [Fact]
    public void Toolbar_Buttons_Have_AriaPressed_For_Active_State()
    {
        // The active-state plumbing is what lets these toggle buttons announce
        // pressed/unpressed — assert it's present on the code button.
        var cut = RenderToolbar(L.EditorToolbarPreset.Standard);
        var code = cut.Find("[aria-label='Inline code']");
        Assert.Equal("false", code.GetAttribute("aria-pressed"));
    }
}
