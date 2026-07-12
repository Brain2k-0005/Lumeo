using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ThemeSwitcher;

/// <summary>
/// Every swatch/mode control is a native &lt;button @onclick&gt; wrapped in a Tooltip
/// AsChild slot — Enter/Space activation is free via the browser's default button
/// semantics, so .Click() exercises the exact handler a synthesized keydown would run
/// (ThemeSwitcherBehaviorTests already pins SetScheme/SetMode invocation through that
/// same mechanism). This file adds the one keyboard-specific angle not covered
/// elsewhere: Tab reaches every swatch and mode button in DOM order — there is no
/// roving-tabindex/arrow-key group here, each control keeps its own independent stop.
/// </summary>
public class ThemeSwitcherKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ThemeSwitcherKeyboardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.JSInterop.Setup<string>("themeManager.getMode").SetResult("system");
        _ctx.JSInterop.Setup<string>("themeManager.getScheme").SetResult("zinc");
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Every_Swatch_And_Mode_Button_Carries_No_Tabindex_Override()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        var buttons = cut.FindAll("button");
        // One button per available scheme swatch, plus Light/Dark/System mode buttons.
        Assert.True(buttons.Count >= 4);
        foreach (var button in buttons)
        {
            Assert.False(button.HasAttribute("tabindex"));
            Assert.False(button.HasAttribute("disabled"));
        }
    }

    [Fact]
    public void Mode_Buttons_Are_Reachable_By_Their_Accessible_Label_In_DOM_Order()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        var buttons = cut.FindAll("button");
        var labels = buttons.Select(b => b.GetAttribute("aria-label")).ToList();

        Assert.Contains("Light", labels);
        Assert.Contains("Dark", labels);
        Assert.Contains("System", labels);
        // Mode buttons render after the scheme swatches — Light before Dark before
        // System, matching source order (no reordering by active state).
        Assert.True(labels.IndexOf("Light") < labels.IndexOf("Dark"));
        Assert.True(labels.IndexOf("Dark") < labels.IndexOf("System"));
    }
}
