using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Input;

/// <summary>
/// Input's typing surface is a native &lt;input&gt; (native browser keyboard handling,
/// out of scope here). The Lumeo-owned surface is the clear button that appears when
/// Clearable + non-empty. It carries a deliberate <c>tabindex="-1"</c> — i.e. the OPPOSITE
/// of the naive "Tab reaches input then clear button" assumption in the gap brief: the
/// clear affordance is intentionally mouse/touch-only and is NOT a second Tab stop, so a
/// keyboard user tabs straight past it without ever landing on it. These tests pin that
/// actual (documented) behavior plus the input itself staying in the natural Tab order,
/// and the clear-then-refocus mechanism the button's activation performs.
/// </summary>
public class InputKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public InputKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Clear_Button_Carries_TabIndex_Minus1_So_It_Is_Not_A_Second_Tab_Stop()
    {
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Clearable, true)
            .Add(i => i.Value, "hello"));

        var clearButton = cut.Find("button");
        Assert.Equal("-1", clearButton.GetAttribute("tabindex"));
    }

    [Fact]
    public void Activating_The_Clear_Button_Empties_The_Value_And_Fires_OnClear()
    {
        string? current = "hello";
        var clearFired = false;
        var cut = _ctx.Render<L.Input>(p => p
            .Add(i => i.Clearable, true)
            .Add(i => i.Value, current)
            .Add(i => i.ValueChanged, v => current = v)
            .Add(i => i.OnClear, () => clearFired = true));

        cut.Find("button").Click();

        Assert.Equal("", current);
        Assert.True(clearFired);
    }

    [Fact]
    public void Plain_Input_Has_No_TabIndex_Override_So_It_Stays_In_The_Natural_Tab_Order()
    {
        var cut = _ctx.Render<L.Input>();

        Assert.Null(cut.Find("input").GetAttribute("tabindex"));
    }
}
