using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Button;

public class ButtonTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public ButtonTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_With_Default_Variant_And_Size()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .AddChildContent("Click me"));

        var button = cut.Find("button");
        Assert.Contains("bg-primary", button.GetAttribute("class"));
        Assert.Contains("h-9 px-4 py-2", button.GetAttribute("class"));
        Assert.Equal("Click me", button.TextContent.Trim());
    }

    [Fact]
    public void Renders_Destructive_Variant()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Destructive)
            .AddChildContent("Delete"));

        var button = cut.Find("button");
        Assert.Contains("bg-destructive", button.GetAttribute("class"));
    }

    [Fact]
    public void Renders_Outline_Variant()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Outline)
            .AddChildContent("Outline"));

        var button = cut.Find("button");
        Assert.Contains("border-input", button.GetAttribute("class"));
    }

    [Fact]
    public void Renders_Secondary_Variant()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Secondary)
            .AddChildContent("Secondary"));

        var button = cut.Find("button");
        Assert.Contains("bg-secondary", button.GetAttribute("class"));
    }

    [Fact]
    public void Renders_Ghost_Variant()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Ghost)
            .AddChildContent("Ghost"));

        var button = cut.Find("button");
        Assert.Contains("hover:bg-accent", button.GetAttribute("class"));
    }

    [Fact]
    public void Renders_Link_Variant()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Variant, Lumeo.Button.ButtonVariant.Link)
            .AddChildContent("Link"));

        var button = cut.Find("button");
        Assert.Contains("underline-offset-4", button.GetAttribute("class"));
    }

    [Theory]
    [InlineData(Lumeo.Button.ButtonSize.Sm, "h-8")]
    [InlineData(Lumeo.Button.ButtonSize.Lg, "h-10")]
    [InlineData(Lumeo.Button.ButtonSize.Icon, "w-9")]
    public void Renders_Correct_Size_Classes(Lumeo.Button.ButtonSize size, string expectedClass)
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Size, size)
            .AddChildContent("Btn"));

        var button = cut.Find("button");
        Assert.Contains(expectedClass, button.GetAttribute("class"));
    }

    [Fact]
    public void Click_Event_Fires()
    {
        var clicked = false;
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.OnClick, _ => clicked = true)
            .AddChildContent("Click"));

        cut.Find("button").Click();
        Assert.True(clicked);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.Class, "my-custom-class")
            .AddChildContent("Styled"));

        var button = cut.Find("button");
        Assert.Contains("my-custom-class", button.GetAttribute("class"));
        Assert.Contains("inline-flex", button.GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Button>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-button",
                ["aria-label"] = "Close dialog"
            })
            .AddChildContent("X"));

        var button = cut.Find("button");
        Assert.Equal("my-button", button.GetAttribute("data-testid"));
        Assert.Equal("Close dialog", button.GetAttribute("aria-label"));
    }
}
