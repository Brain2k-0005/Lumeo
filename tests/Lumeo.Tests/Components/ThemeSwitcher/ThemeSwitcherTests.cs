using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using L = Lumeo;

namespace Lumeo.Tests.Components.ThemeSwitcher;

public class ThemeSwitcherTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public ThemeSwitcherTests()
    {
        _ctx.AddLumeoServices();
        // Return default values for JS theme calls
        _ctx.JSInterop.SetupVoid("themeManager.setMode", _ => true);
        _ctx.JSInterop.SetupVoid("themeManager.setScheme", _ => true);
        _ctx.JSInterop.Setup<string>("themeManager.getMode").SetResult("system");
        _ctx.JSInterop.Setup<string>("themeManager.getScheme").SetResult("orange");
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Root_Div()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Renders_Mode_Buttons()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        var buttons = cut.FindAll("button");
        // At least 3 mode buttons (Light, Dark, System)
        Assert.True(buttons.Count >= 3);
    }

    [Fact]
    public void Renders_Scheme_Swatches_For_All_Available_Schemes()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        var buttons = cut.FindAll("button");
        // Should have buttons for each color scheme + 3 mode buttons
        Assert.True(buttons.Count >= ThemeService.AvailableSchemes.Count + 3);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>(p => p
            .Add(b => b.Class, "custom-switcher"));

        var div = cut.Find("div");
        Assert.Contains("custom-switcher", div.GetAttribute("class"));
        Assert.Contains("space-y-4", div.GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "theme-switcher"
            }));

        Assert.Equal("theme-switcher", cut.Find("div").GetAttribute("data-testid"));
    }

    [Fact]
    public void Swatch_Buttons_Have_Style_Attribute()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        // Color scheme buttons have background-color styles
        var swatches = cut.FindAll("button[style]");
        Assert.NotEmpty(swatches);
    }

    [Fact]
    public void Mode_Buttons_Have_Title_Attributes()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        Assert.NotNull(cut.Find("button[title='Light']"));
        Assert.NotNull(cut.Find("button[title='Dark']"));
        Assert.NotNull(cut.Find("button[title='System']"));
    }

    [Fact]
    public void Null_Class_Does_Not_Add_Extra_Space()
    {
        var cut = _ctx.Render<L.ThemeSwitcher>();

        var div = cut.Find("div");
        var cls = div.GetAttribute("class");
        Assert.DoesNotContain("  ", cls);
        Assert.False(cls!.EndsWith(" "));
    }
}
