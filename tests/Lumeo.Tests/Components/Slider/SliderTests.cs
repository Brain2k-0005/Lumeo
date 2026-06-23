using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Slider;

public class SliderTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SliderTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

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

    // --- #186: range thumbs can't cross; min-steps-between ---

    [Fact]
    public void Range_Renders_Two_Inputs()
    {
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Value, 20)
            .Add(s => s.ValueEnd, 80));

        Assert.Equal(2, cut.FindAll("input[type='range']").Count);
    }

    [Fact]
    public void Start_Thumb_Cannot_Cross_End_Thumb()
    {
        double? start = null;
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Value, 20)
            .Add(s => s.ValueEnd, 50)
            .Add(s => s.ValueChanged, v => start = v));

        // Drag start past the end thumb — it must clamp at the end value.
        cut.FindAll("input[type='range']")[0].Input("70");

        Assert.Equal(50, start);
    }

    [Fact]
    public void End_Thumb_Cannot_Cross_Start_Thumb()
    {
        double? end = null;
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Value, 40)
            .Add(s => s.ValueEnd, 60)
            .Add(s => s.ValueEndChanged, v => end = v));

        // Drag end below the start thumb — it must clamp at the start value.
        cut.FindAll("input[type='range']")[1].Input("10");

        Assert.Equal(40, end);
    }

    [Fact]
    public void MinStepsBetweenThumbs_Keeps_A_Gap()
    {
        double? start = null;
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Step, 1)
            .Add(s => s.MinStepsBetweenThumbs, 10)
            .Add(s => s.Value, 20)
            .Add(s => s.ValueEnd, 50)
            .Add(s => s.ValueChanged, v => start = v));

        // Try to push start to 48 — with a 10-step (10-unit) gap it clamps to 40.
        cut.FindAll("input[type='range']")[0].Input("48");

        Assert.Equal(40, start);
    }

    [Fact]
    public void MinStepsBetweenThumbs_Clamps_End_Floor()
    {
        double? end = null;
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Step, 2)
            .Add(s => s.MinStepsBetweenThumbs, 5) // 5 * 2 = 10 unit gap
            .Add(s => s.Value, 30)
            .Add(s => s.ValueEnd, 60)
            .Add(s => s.ValueEndChanged, v => end = v));

        cut.FindAll("input[type='range']")[1].Input("32");

        Assert.Equal(40, end); // 30 + 10
    }

    [Fact]
    public void Range_Within_Bounds_Passes_Through_Unclamped()
    {
        double? start = null;
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Value, 20)
            .Add(s => s.ValueEnd, 80)
            .Add(s => s.ValueChanged, v => start = v));

        cut.FindAll("input[type='range']")[0].Input("35");

        Assert.Equal(35, start);
    }

    // --- a11y: focus ring (B6) + keyboard-triggered value tooltip (B7) ---

    [Fact]
    public void Slider_Input_Has_Focus_Visible_Ring()
    {
        // appearance-none strips the native focus outline; an explicit focus-visible
        // ring must replace it (WCAG 2.4.7).
        var cls = _ctx.Render<Lumeo.Slider>().Find("input[type='range']").GetAttribute("class") ?? "";
        Assert.Contains("focus-visible:ring-2", cls);
        Assert.Contains("focus-visible:ring-ring", cls);
    }

    [Fact]
    public void Slider_Value_Tooltip_Shows_On_Keyboard_Focus_Not_Only_Hover()
    {
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(s => s.ShowTooltip, true)
            .Add(s => s.Value, 42));

        Assert.Empty(cut.FindAll(".text-popover-foreground")); // hidden until interaction

        cut.Find("input[type='range']").Focus();               // keyboard focus, not pointer hover

        var tip = cut.Find(".text-popover-foreground");
        Assert.Contains("42", tip.TextContent);
    }

    [Fact]
    public void Range_Thumbs_Announce_The_Reachable_Range_Not_Full_Min_Max()
    {
        // Start=20, End=80, MinStepsBetweenThumbs=5 (Step=1) -> MinGap=5. The start
        // thumb can only reach End-5=75 and the end thumb only Start+5=25; the
        // announced aria range must match (WAI-ARIA multi-thumb), not the full 0..100.
        var cut = _ctx.Render<Lumeo.Slider>(p => p
            .Add(s => s.IsRange, true)
            .Add(s => s.Value, 20)
            .Add(s => s.ValueEnd, 80)
            .Add(s => s.MinStepsBetweenThumbs, 5));

        var inputs = cut.FindAll("input[type='range']");
        Assert.Equal("0", inputs[0].GetAttribute("aria-valuemin"));
        Assert.Equal("75", inputs[0].GetAttribute("aria-valuemax")); // start ceiling = End - MinGap
        Assert.Equal("25", inputs[1].GetAttribute("aria-valuemin")); // end floor = Start + MinGap
        Assert.Equal("100", inputs[1].GetAttribute("aria-valuemax"));
    }
}
