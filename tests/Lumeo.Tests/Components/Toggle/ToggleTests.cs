using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Toggle;

public class ToggleTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public ToggleTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Button_Element()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .AddChildContent("Bold"));

        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void Default_State_Is_Not_Pressed()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .AddChildContent("Bold"));

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Renders_Pressed_State_Correctly()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Pressed, true)
            .AddChildContent("Bold"));

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Renders_Child_Content()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .AddChildContent("Bold"));

        Assert.Equal("Bold", cut.Find("button").TextContent.Trim());
    }

    [Fact]
    public void Click_Toggles_From_Not_Pressed_To_Pressed()
    {
        bool? callbackValue = null;
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Pressed, false)
            .Add(b => b.PressedChanged, v => callbackValue = v)
            .AddChildContent("B"));

        cut.Find("button").Click();

        Assert.True(callbackValue);
    }

    [Fact]
    public void Click_Toggles_From_Pressed_To_Not_Pressed()
    {
        bool? callbackValue = null;
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Pressed, true)
            .Add(b => b.PressedChanged, v => callbackValue = v)
            .AddChildContent("B"));

        cut.Find("button").Click();

        Assert.False(callbackValue);
    }

    [Fact]
    public void Click_Invokes_PressedChanged_Callback()
    {
        var callbackInvoked = false;
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.PressedChanged, _ => callbackInvoked = true)
            .AddChildContent("B"));

        cut.Find("button").Click();

        Assert.True(callbackInvoked);
    }

    [Fact]
    public void Disabled_Click_Does_Not_Toggle()
    {
        bool? callbackValue = null;
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Pressed, false)
            .Add(b => b.Disabled, true)
            .Add(b => b.PressedChanged, v => callbackValue = v)
            .AddChildContent("B"));

        cut.Find("button").Click();

        Assert.Null(callbackValue);
    }

    [Fact]
    public void Disabled_Attribute_Is_Set()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Disabled, true)
            .AddChildContent("B"));

        Assert.NotNull(cut.Find("button").GetAttribute("disabled"));
    }

    [Fact]
    public void Aria_Pressed_True_When_Pressed_Is_True()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Pressed, true)
            .AddChildContent("B"));

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Aria_Pressed_False_When_Pressed_Is_False()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Pressed, false)
            .AddChildContent("B"));

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Default_Variant_Unpressed_Has_Transparent_Background()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Variant, Lumeo.Toggle.ToggleVariant.Default)
            .Add(b => b.Pressed, false)
            .AddChildContent("B"));

        Assert.Contains("bg-transparent", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Default_Variant_Pressed_Has_Accent_Background()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Variant, Lumeo.Toggle.ToggleVariant.Default)
            .Add(b => b.Pressed, true)
            .AddChildContent("B"));

        Assert.Contains("bg-accent", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Outline_Variant_Unpressed_Has_Border()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Variant, Lumeo.Toggle.ToggleVariant.Outline)
            .Add(b => b.Pressed, false)
            .AddChildContent("B"));

        var cls = cut.Find("button").GetAttribute("class");
        Assert.Contains("border", cls);
        Assert.Contains("border-input", cls);
    }

    [Fact]
    public void Outline_Variant_Pressed_Has_Border_And_Accent()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Variant, Lumeo.Toggle.ToggleVariant.Outline)
            .Add(b => b.Pressed, true)
            .AddChildContent("B"));

        var cls = cut.Find("button").GetAttribute("class");
        Assert.Contains("bg-accent", cls);
        Assert.Contains("border-input", cls);
    }

    [Theory]
    [InlineData(Lumeo.Toggle.ToggleSize.Default, "h-9")]
    [InlineData(Lumeo.Toggle.ToggleSize.Sm, "h-8")]
    [InlineData(Lumeo.Toggle.ToggleSize.Lg, "h-10")]
    public void Renders_Correct_Size_Classes(Lumeo.Toggle.ToggleSize size, string expectedClass)
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Size, size)
            .AddChildContent("B"));

        Assert.Contains(expectedClass, cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Default_Size_Has_Px_3_Padding()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Size, Lumeo.Toggle.ToggleSize.Default)
            .AddChildContent("B"));

        Assert.Contains("px-3", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Sm_Size_Has_Px_2_Padding()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Size, Lumeo.Toggle.ToggleSize.Sm)
            .AddChildContent("B"));

        Assert.Contains("px-2", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Lg_Size_Has_Px_4_Padding()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Size, Lumeo.Toggle.ToggleSize.Lg)
            .AddChildContent("B"));

        Assert.Contains("px-4", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.Class, "my-custom-class")
            .AddChildContent("B"));

        var cls = cut.Find("button").GetAttribute("class");
        Assert.Contains("my-custom-class", cls);
        Assert.Contains("inline-flex", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Toggle>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-toggle",
                ["aria-label"] = "Bold text"
            })
            .AddChildContent("B"));

        var button = cut.Find("button");
        Assert.Equal("my-toggle", button.GetAttribute("data-testid"));
        Assert.Equal("Bold text", button.GetAttribute("aria-label"));
    }
}
