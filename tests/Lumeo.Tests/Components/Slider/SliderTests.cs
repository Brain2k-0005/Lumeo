using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Slider;

public class SliderTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public SliderTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Range_Input()
    {
        var cut = _ctx.Render<Lumeo.Slider>();

        var input = cut.Find("input[type='range']");
        Assert.NotNull(input);
    }

    [Fact]
    public void Default_Min_Is_Zero()
    {
        var cut = _ctx.Render<Lumeo.Slider>();

        var input = cut.Find("input[type='range']");
        Assert.Equal("0", input.GetAttribute("min"));
    }

    [Fact]
    public void Default_Max_Is_100()
    {
        var cut = _ctx.Render<Lumeo.Slider>();

        var input = cut.Find("input[type='range']");
        Assert.Equal("100", input.GetAttribute("max"));
    }

    [Fact]
    public void Default_Step_Is_1()
    {
        var cut = _ctx.Render<Lumeo.Slider>();

        var input = cut.Find("input[type='range']");
        Assert.Equal("1", input.GetAttribute("step"));
    }

    [Fact]
    public void Custom_Min_Is_Rendered()
    {
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(b => b.Min, 10.0));

        var input = cut.Find("input[type='range']");
        Assert.Equal("10", input.GetAttribute("min"));
    }

    [Fact]
    public void Custom_Max_Is_Rendered()
    {
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(b => b.Max, 50.0));

        var input = cut.Find("input[type='range']");
        Assert.Equal("50", input.GetAttribute("max"));
    }

    [Fact]
    public void Custom_Step_Is_Rendered()
    {
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(b => b.Step, 5.0));

        var input = cut.Find("input[type='range']");
        Assert.Equal("5", input.GetAttribute("step"));
    }

    [Fact]
    public void Value_Is_Rendered_On_Input()
    {
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(b => b.Value, 42.0));

        var input = cut.Find("input[type='range']");
        Assert.Equal("42", input.GetAttribute("value"));
    }

    [Fact]
    public void Input_Event_Invokes_ValueChanged_Callback()
    {
        double? receivedValue = null;
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(b => b.Value, 0.0)
            .Add(b => b.ValueChanged, v => receivedValue = v));

        cut.Find("input[type='range']").Input("75");

        Assert.Equal(75.0, receivedValue);
    }

    [Fact]
    public void Input_Event_Updates_Value()
    {
        double capturedValue = -1;
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(b => b.Value, 0.0)
            .Add(b => b.ValueChanged, v => capturedValue = v));

        cut.Find("input[type='range']").Input("30");

        Assert.Equal(30.0, capturedValue);
    }

    [Fact]
    public void Disabled_Attribute_Is_Applied_When_True()
    {
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(b => b.Disabled, true));

        var input = cut.Find("input[type='range']");
        Assert.True(input.HasAttribute("disabled"));
    }

    [Fact]
    public void Disabled_Attribute_Not_Present_When_False()
    {
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(b => b.Disabled, false));

        var input = cut.Find("input[type='range']");
        Assert.False(input.HasAttribute("disabled"));
    }

    [Fact]
    public void Wrapper_Div_Has_Base_Classes()
    {
        var cut = _ctx.Render<Lumeo.Slider>();

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("relative", cls);
        Assert.Contains("w-full", cls);
    }

    [Fact]
    public void Custom_Class_Appended_To_Wrapper()
    {
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(b => b.Class, "my-slider"));

        var div = cut.Find("div");
        Assert.Contains("my-slider", div.GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forwarded_To_Wrapper()
    {
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "slider-wrap"
            }));

        var div = cut.Find("div");
        Assert.Equal("slider-wrap", div.GetAttribute("data-testid"));
    }

    [Fact]
    public void Input_Has_Accent_Primary_Class()
    {
        var cut = _ctx.Render<Lumeo.Slider>();

        var input = cut.Find("input[type='range']");
        Assert.Contains("accent-primary", input.GetAttribute("class"));
    }
}
