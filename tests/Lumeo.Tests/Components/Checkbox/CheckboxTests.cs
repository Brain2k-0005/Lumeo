using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Checkbox;

public class CheckboxTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public CheckboxTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Button_With_Checkbox_Role()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>();

        var button = cut.Find("button");
        Assert.Equal("checkbox", button.GetAttribute("role"));
    }

    [Fact]
    public void Default_State_Is_Unchecked()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>();

        var button = cut.Find("button");
        Assert.Equal("false", button.GetAttribute("aria-checked"));
    }

    [Fact]
    public void Renders_Checked_State_Correctly()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.Checked, true));

        var button = cut.Find("button");
        Assert.Equal("true", button.GetAttribute("aria-checked"));
    }

    [Fact]
    public void Checked_State_Shows_Check_Icon()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.Checked, true));

        // When checked, a Blazicon (svg) should be rendered inside
        Assert.NotEmpty(cut.FindAll("svg"));
    }

    [Fact]
    public void Unchecked_State_Has_No_Check_Icon()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.Checked, false));

        Assert.Empty(cut.FindAll("svg"));
    }

    [Fact]
    public void Click_Toggles_From_Unchecked_To_Checked()
    {
        bool? callbackValue = null;
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.Checked, false)
            .Add(b => b.CheckedChanged, v => callbackValue = v));

        cut.Find("button").Click();

        Assert.True(callbackValue);
    }

    [Fact]
    public void Click_Toggles_From_Checked_To_Unchecked()
    {
        bool? callbackValue = null;
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.Checked, true)
            .Add(b => b.CheckedChanged, v => callbackValue = v));

        cut.Find("button").Click();

        Assert.False(callbackValue);
    }

    [Fact]
    public void Click_Invokes_CheckedChanged_Callback()
    {
        var callbackInvoked = false;
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.CheckedChanged, _ => callbackInvoked = true));

        cut.Find("button").Click();

        Assert.True(callbackInvoked);
    }

    [Fact]
    public void Disabled_Click_Does_Not_Toggle()
    {
        bool? callbackValue = null;
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.Checked, false)
            .Add(b => b.Disabled, true)
            .Add(b => b.CheckedChanged, v => callbackValue = v));

        cut.Find("button").Click();

        Assert.Null(callbackValue);
    }

    [Fact]
    public void Disabled_Attribute_Is_Set()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.Disabled, true));

        var button = cut.Find("button");
        Assert.NotNull(button.GetAttribute("disabled"));
    }

    [Fact]
    public void Aria_Checked_True_When_Checked_Is_True()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.Checked, true));

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-checked"));
    }

    [Fact]
    public void Aria_Checked_False_When_Checked_Is_False()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.Checked, false));

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-checked"));
    }

    [Fact]
    public void Checked_State_Adds_Bg_Primary_Class()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.Checked, true));

        Assert.Contains("bg-primary", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Unchecked_State_Adds_Bg_Background_Class()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.Checked, false));

        Assert.Contains("bg-background", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.Class, "my-custom-class"));

        Assert.Contains("my-custom-class", cut.Find("button").GetAttribute("class"));
        Assert.Contains("border-primary", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Checkbox>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-checkbox",
                ["aria-label"] = "Accept terms"
            }));

        var button = cut.Find("button");
        Assert.Equal("my-checkbox", button.GetAttribute("data-testid"));
        Assert.Equal("Accept terms", button.GetAttribute("aria-label"));
    }
}
