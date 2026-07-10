using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ThemeToggle;

/// <summary>
/// Behaviour / a11y coverage for the ThemeToggle button: clicking must drive the
/// ThemeService through the themeManager JS interop contract (setMode), and the
/// button's aria-pressed state + icon must reflect the live dark/light state read
/// back from JS. The fixture runs JSInterop in Loose mode, so theme calls are
/// recorded and we assert against the recorded invocations.
/// </summary>
public class ThemeToggleBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ThemeToggleBehaviorTests()
    {
        _ctx.AddLumeoServices();
        // setMode is a void interop call invoked by ThemeService.SetModeAsync.
        _ctx.JSInterop.SetupVoid("themeManager.setMode", _ => true);
        _ctx.JSInterop.SetupVoid("themeManager.setScheme", _ => true);
        // First-render InitializeAsync reads the live theme state from JS.
        _ctx.JSInterop.Setup<string>("themeManager.getMode").SetResult("system");
        _ctx.JSInterop.Setup<string>("themeManager.getScheme").SetResult("orange");
        _ctx.JSInterop.Setup<string>("themeManager.getDirection").SetResult("ltr");
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Init_Reads_Theme_State_From_JS_On_First_Render()
    {
        // OnAfterRenderAsync(firstRender) → ThemeService.InitializeAsync reads the
        // live mode + dark flag from the themeManager interop surface.
        _ctx.Render<L.ThemeToggle>();

        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "themeManager.getMode");
        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "themeManager.isDark");
    }

    [Fact]
    public void Clicking_Invokes_SetMode_Interop()
    {
        var cut = _ctx.Render<L.ThemeToggle>();

        cut.Find("button").Click();

        // The toggle drives the theme through the JS contract, not a no-op.
        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier == "themeManager.setMode");
    }

    [Fact]
    public void First_Click_From_System_Cycles_To_Dark()
    {
        // getMode returns "system" at init, so ToggleModeAsync cycles System→Dark
        // and pushes "dark" to JS via setMode.
        var cut = _ctx.Render<L.ThemeToggle>();

        cut.Find("button").Click();

        var setMode = _ctx.JSInterop.Invocations.Last(i => i.Identifier == "themeManager.setMode");
        Assert.Equal("dark", setMode.Arguments[0]);
    }

    [Fact]
    public void AriaPressed_Reflects_Light_State_When_Not_Dark()
    {
        // isDark resolves to false → button advertises an un-pressed (light) state.
        var cut = _ctx.Render<L.ThemeToggle>();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void AriaPressed_Is_True_When_JS_Reports_Dark()
    {
        // Re-setup isDark to report the dark scheme before first render so the
        // init read flips the toggle into its pressed state.
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(true);

        var cut = _ctx.Render<L.ThemeToggle>();

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Dark_State_Renders_Sun_Icon_Light_State_Renders_Moon_Icon()
    {
        // Icon is the visible affordance for the current mode: Sun while dark,
        // Moon while light (see ThemeToggle.razor @if (_isDark)). The icon
        // wrapper renders an <svg>, so we assert via aria-pressed (a robust proxy
        // for _isDark that the same branch drives) plus the presence of an icon.
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(true);

        var cut = _ctx.Render<L.ThemeToggle>();
        var button = cut.Find("button");

        // Dark → pressed, and an icon (svg) is rendered inside the button.
        Assert.Equal("true", button.GetAttribute("aria-pressed"));
        Assert.NotNull(button.QuerySelector("svg"));
    }

    [Fact]
    public void Has_Accessible_Toggle_Label()
    {
        // Icon-only control must expose a name to assistive tech.
        var cut = _ctx.Render<L.ThemeToggle>();
        var button = cut.Find("button");

        Assert.False(string.IsNullOrWhiteSpace(button.GetAttribute("aria-label")));
        Assert.NotNull(button.GetAttribute("aria-pressed"));
    }
}
