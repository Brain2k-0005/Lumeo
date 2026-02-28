using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ThemeToggle;

public class ThemeToggleTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public ThemeToggleTests()
    {
        _ctx.AddLumeoServices();
        _ctx.JSInterop.Setup<string>("themeManager.getMode").SetResult("system");
        _ctx.JSInterop.Setup<string>("themeManager.getScheme").SetResult("orange");
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(false);
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Button()
    {
        var cut = _ctx.Render<L.ThemeToggle>();

        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void Button_Has_Toggle_Title()
    {
        var cut = _ctx.Render<L.ThemeToggle>();

        var button = cut.Find("button");
        Assert.Equal("Toggle theme", button.GetAttribute("title"));
    }

    [Fact]
    public void Button_Has_Inline_Flex_Class()
    {
        var cut = _ctx.Render<L.ThemeToggle>();

        var button = cut.Find("button");
        Assert.Contains("inline-flex", button.GetAttribute("class"));
    }

    [Fact]
    public void Button_Has_Rounded_Class()
    {
        var cut = _ctx.Render<L.ThemeToggle>();

        var button = cut.Find("button");
        Assert.Contains("rounded-md", button.GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.ThemeToggle>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "theme-toggle",
                ["aria-label"] = "Toggle dark mode"
            }));

        var button = cut.Find("button");
        Assert.Equal("theme-toggle", button.GetAttribute("data-testid"));
        Assert.Equal("Toggle dark mode", button.GetAttribute("aria-label"));
    }

    [Fact]
    public void Renders_Icon_Inside_Button()
    {
        var cut = _ctx.Render<L.ThemeToggle>();

        // SVG or icon element should be inside button
        var button = cut.Find("button");
        Assert.NotEmpty(button.InnerHtml);
    }
}
