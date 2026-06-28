using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using L = Lumeo;

namespace Lumeo.Tests.Components.ThemeSwitcher;

/// <summary>
/// battle-wave3 regression coverage for <see cref="L.ThemeSwitcher"/>.
///
/// #21 (medium, keyboard-a11y) — "Selected theme/mode not exposed to assistive tech
/// (no aria-pressed/aria-checked/aria-current on toggle buttons)". The colour-scheme
/// swatch buttons and the Light/Dark/System mode buttons previously expressed the
/// active selection ONLY visually (GetSwatchClass scale-110 / border-foreground + the
/// Check glyph; GetModeButtonClass bg-background). A screen-reader user toggling between
/// schemes/modes therefore got no spoken selected-state. The fix binds
/// aria-pressed="true|false" to the active id/mode on both interactive buttons.
///
/// Fixture mirrors ThemeSwitcherBehaviorTests: JSInterop in Loose mode with the
/// read-side themeManager.* calls planned so InitializeAsync (run from
/// OnAfterRenderAsync on first render) resolves to scheme "orange" / mode "system".
/// </summary>
public class ThemeSwitcherBattleWave3Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ThemeSwitcherBattleWave3Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.JSInterop.Setup<string>("themeManager.getMode").SetResult("system");
        _ctx.JSInterop.Setup<string>("themeManager.getScheme").SetResult("orange");
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);
        _ctx.JSInterop.Setup<string>("themeManager.getDirection").SetResult("ltr");
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static AngleSharp.Dom.IElement ModeButton(IRenderedComponent<L.ThemeSwitcher> cut, string label)
        => cut.Find($"button[aria-label='{label}']");

    [Fact]
    public void Selected_Swatch_Exposes_AriaPressed_True_While_Others_Report_False()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        // Choose a known scheme so we control which swatch is active.
        var target = ThemeService.AvailableSchemes.First(s => s.Id == "violet");
        cut.Find($"button[aria-label='{target.DisplayName}']").Click();

        // The active swatch announces its selected state to assistive tech...
        var selected = cut.Find($"button[aria-label='{target.DisplayName}']");
        Assert.Equal("true", selected.GetAttribute("aria-pressed"));

        // ...and a non-selected swatch reports aria-pressed="false" (present, not absent),
        // so AT can tell the difference. Pre-fix neither attribute existed (null).
        var other = ThemeService.AvailableSchemes.First(s => s.Id == "blue");
        var otherBtn = cut.Find($"button[aria-label='{other.DisplayName}']");
        Assert.Equal("false", otherBtn.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Selected_Mode_Button_Exposes_AriaPressed_True_While_Others_Report_False()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        ModeButton(cut, "Dark").Click();

        // Active mode button announces aria-pressed="true"; the inactive ones "false".
        // Pre-fix the attribute was absent entirely, leaving the selection invisible to AT.
        Assert.Equal("true", ModeButton(cut, "Dark").GetAttribute("aria-pressed"));
        Assert.Equal("false", ModeButton(cut, "Light").GetAttribute("aria-pressed"));
        Assert.Equal("false", ModeButton(cut, "System").GetAttribute("aria-pressed"));
    }
}
