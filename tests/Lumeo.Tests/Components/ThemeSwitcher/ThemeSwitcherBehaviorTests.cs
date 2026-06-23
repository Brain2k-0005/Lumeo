using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using L = Lumeo;

namespace Lumeo.Tests.Components.ThemeSwitcher;

/// <summary>
/// Behaviour / interop tests for <see cref="L.ThemeSwitcher"/>.
///
/// The switcher renders one swatch button per <see cref="ThemeService.AvailableSchemes"/>
/// entry plus three mode buttons (Light / Dark / System). Choosing an option must:
///   - invoke the matching theme interop (themeManager.setScheme / themeManager.setMode),
///     verified via the loose-mode JS-interop spy, and
///   - reflect the chosen scheme/mode in the selected visual state (the active swatch gets
///     the "scale-110" / "border-foreground" treatment + a Check glyph; the active mode
///     button gets the "bg-background" treatment).
///
/// The fixture registers JS interop in LOOSE mode, so the themeManager.* calls made during
/// OnAfterRenderAsync (InitializeAsync) and on click are recorded rather than failing.
/// </summary>
public class ThemeSwitcherBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ThemeSwitcherBehaviorTests()
    {
        _ctx.AddLumeoServices();
        // Provide return values for the read-side interop invoked by InitializeAsync
        // (runs in OnAfterRenderAsync on first render). Set-side calls are void and
        // simply recorded by the loose-mode spy.
        _ctx.JSInterop.Setup<string>("themeManager.getMode").SetResult("system");
        _ctx.JSInterop.Setup<string>("themeManager.getScheme").SetResult("orange");
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);
        _ctx.JSInterop.Setup<string>("themeManager.getDirection").SetResult("ltr");
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // The last three buttons are always the mode toggle (Light, Dark, System);
    // everything before them is a colour swatch.
    private static IReadOnlyList<AngleSharp.Dom.IElement> SwatchButtons(IRenderedComponent<L.ThemeSwitcher> cut)
    {
        var buttons = cut.FindAll("button");
        return buttons.Take(buttons.Count - 3).ToList();
    }

    private static AngleSharp.Dom.IElement ModeButton(IRenderedComponent<L.ThemeSwitcher> cut, string title)
        => cut.Find($"button[title='{title}']");

    [Fact]
    public void Choosing_A_Scheme_Invokes_SetScheme_Interop_With_That_SchemeId()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        // Pick a scheme that differs from the initialised one ("orange").
        var target = ThemeService.AvailableSchemes.First(s => s.Id == "violet");
        var swatch = cut.Find($"button[title='{target.DisplayName}']");
        swatch.Click();

        // The set-scheme interop fired with the chosen id as its sole argument.
        var invocation = _ctx.JSInterop.VerifyInvoke("themeManager.setScheme");
        Assert.Equal("violet", invocation.Arguments[0]);
    }

    [Fact]
    public void Choosing_A_Scheme_Marks_That_Swatch_As_Selected()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        var target = ThemeService.AvailableSchemes.First(s => s.Id == "green");
        var swatch = cut.Find($"button[title='{target.DisplayName}']");
        swatch.Click();

        // Re-query after the click-driven re-render.
        var selected = cut.Find($"button[title='{target.DisplayName}']");
        var cls = selected.GetAttribute("class") ?? string.Empty;
        Assert.Contains("scale-110", cls);
        Assert.Contains("border-foreground", cls);

        // Exactly one swatch is selected at a time, and it carries the Check glyph.
        var selectedSwatches = SwatchButtons(cut)
            .Where(b => (b.GetAttribute("class") ?? string.Empty).Contains("scale-110"))
            .ToList();
        Assert.Single(selectedSwatches);
        Assert.Equal(target.DisplayName, selectedSwatches[0].GetAttribute("title"));
        Assert.NotEmpty(selectedSwatches[0].QuerySelectorAll("svg"));
    }

    [Fact]
    public void Choosing_A_Different_Scheme_Moves_The_Selection_Off_The_Previous_One()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        var first = ThemeService.AvailableSchemes.First(s => s.Id == "blue");
        cut.Find($"button[title='{first.DisplayName}']").Click();

        var second = ThemeService.AvailableSchemes.First(s => s.Id == "rose");
        cut.Find($"button[title='{second.DisplayName}']").Click();

        var firstClass = cut.Find($"button[title='{first.DisplayName}']").GetAttribute("class") ?? string.Empty;
        var secondClass = cut.Find($"button[title='{second.DisplayName}']").GetAttribute("class") ?? string.Empty;

        Assert.DoesNotContain("scale-110", firstClass);
        Assert.Contains("scale-110", secondClass);
    }

    [Fact]
    public void Choosing_Dark_Mode_Invokes_SetMode_Interop_With_Dark()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        ModeButton(cut, "Dark").Click();

        var invocation = _ctx.JSInterop.VerifyInvoke("themeManager.setMode");
        Assert.Equal("dark", invocation.Arguments[0]);
    }

    [Fact]
    public void Choosing_Light_Mode_Marks_The_Light_Button_As_Selected()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        ModeButton(cut, "Light").Click();

        var lightClass = ModeButton(cut, "Light").GetAttribute("class") ?? string.Empty;
        var darkClass = ModeButton(cut, "Dark").GetAttribute("class") ?? string.Empty;
        var systemClass = ModeButton(cut, "System").GetAttribute("class") ?? string.Empty;

        // Active mode button gets the raised "bg-background" treatment; the others don't.
        Assert.Contains("bg-background", lightClass);
        Assert.DoesNotContain("bg-background", darkClass);
        Assert.DoesNotContain("bg-background", systemClass);
    }

    [Fact]
    public void Mode_And_Scheme_Selections_Are_Independent()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        // Choose a scheme, then a mode — each selection must hold simultaneously.
        var scheme = ThemeService.AvailableSchemes.First(s => s.Id == "teal");
        cut.Find($"button[title='{scheme.DisplayName}']").Click();
        ModeButton(cut, "Dark").Click();

        var schemeClass = cut.Find($"button[title='{scheme.DisplayName}']").GetAttribute("class") ?? string.Empty;
        var darkClass = ModeButton(cut, "Dark").GetAttribute("class") ?? string.Empty;

        Assert.Contains("scale-110", schemeClass);   // scheme still selected
        Assert.Contains("bg-background", darkClass);  // dark mode now selected

        // Both interop channels were exercised with the chosen values.
        Assert.Equal("teal", _ctx.JSInterop.VerifyInvoke("themeManager.setScheme").Arguments[0]);
        Assert.Equal("dark", _ctx.JSInterop.VerifyInvoke("themeManager.setMode").Arguments[0]);
    }
}
