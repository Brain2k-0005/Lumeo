using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.InplaceEditor;

/// <summary>
/// Regression: Select mode had no keydown wiring, so Escape could not cancel
/// an edit (Text/Textarea modes could). Escape must discard the pending value
/// and exit edit mode without saving — including via the subsequent blur.
/// </summary>
public class InplaceEditorSelectCancelTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public InplaceEditorSelectCancelTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly List<string> Options = new() { "alpha", "beta", "gamma" };

    [Fact]
    public void Escape_In_Select_Mode_Cancels_Edit()
    {
        var saved = false;
        var cancelled = false;

        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(c => c.Value, "alpha")
            .Add(c => c.EditMode, L.InplaceEditor.InplaceEditMode.Select)
            .Add(c => c.SelectOptions, Options)
            .Add(c => c.ValueChanged, _ => saved = true)
            .Add(c => c.OnCancel, () => cancelled = true));

        cut.Find("[role='button']").Click();          // enter edit mode
        var select = cut.Find("select");
        select.Change("gamma");                       // pending (unsaved) pick
        select.KeyDown("Escape");                     // cancel

        Assert.True(cancelled);
        Assert.False(saved);
        // Back in display mode showing the original value.
        Assert.Empty(cut.FindAll("select"));
        Assert.Contains("alpha", cut.Markup);
        Assert.DoesNotContain("gamma", cut.Find("[role='button']").TextContent);
    }

    [Fact]
    public void Blur_After_Escape_Does_Not_Save()
    {
        var savedValues = new List<string?>();

        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(c => c.Value, "alpha")
            .Add(c => c.EditMode, L.InplaceEditor.InplaceEditMode.Select)
            .Add(c => c.SelectOptions, Options)
            .Add(c => c.SaveOnBlur, true)
            .Add(c => c.ValueChanged, v => savedValues.Add(v)));

        cut.Find("[role='button']").Click();
        var select = cut.Find("select");
        select.Change("beta");
        select.KeyDown("Escape");

        // The browser fires blur on the (now-removed) select right after
        // Escape; the OnBlur guard must see _editing == false and skip the
        // save. Re-entering edit mode and blurring without changes exercises
        // the same handler — no save from the cancelled "beta" pick.
        Assert.Empty(savedValues);

        cut.Find("[role='button']").Click();
        cut.Find("select").Blur();
        // SaveOnBlur commits the *current* (original) value — never "beta".
        Assert.DoesNotContain("beta", savedValues);
    }

    [Fact]
    public void Change_Then_Blur_Still_Saves_In_Select_Mode()
    {
        string? saved = null;

        var cut = _ctx.Render<L.InplaceEditor>(p => p
            .Add(c => c.Value, "alpha")
            .Add(c => c.EditMode, L.InplaceEditor.InplaceEditMode.Select)
            .Add(c => c.SelectOptions, Options)
            .Add(c => c.SaveOnBlur, true)
            .Add(c => c.ValueChanged, v => saved = v));

        cut.Find("[role='button']").Click();
        var select = cut.Find("select");
        select.Change("gamma");
        select.Blur();

        Assert.Equal("gamma", saved);
    }
}
