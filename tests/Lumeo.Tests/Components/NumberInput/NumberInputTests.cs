using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.NumberInput;

public class NumberInputTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NumberInputTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Input_And_Two_Buttons()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>();

        Assert.NotNull(cut.Find("input[type='number']"));
        var buttons = cut.FindAll("button");
        Assert.Equal(2, buttons.Count);
    }

    [Fact]
    public void Container_Has_InlineFlex_Class()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("inline-flex", cls);
        Assert.Contains("items-center", cls);
    }

    [Fact]
    public void Disabled_True_Disables_Input_And_Buttons()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Disabled, true));

        Assert.True(cut.Find("input").HasAttribute("disabled"));
        var buttons = cut.FindAll("button");
        Assert.All(buttons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Decrement_Button_Has_Minus_Aria_Label()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>();

        var decrementBtn = cut.Find("button[aria-label='Decrease']");
        Assert.NotNull(decrementBtn);
    }

    [Fact]
    public void Increment_Button_Has_Increase_Aria_Label()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>();

        var incrementBtn = cut.Find("button[aria-label='Increase']");
        Assert.NotNull(incrementBtn);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Class, "my-number-input"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-number-input", cls);
    }

    // --- Prefix ---

    [Fact]
    public void Prefix_Renders_Prefix_Text()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Prefix, "$")
            .Add(n => n.Value, 42.0));

        Assert.Contains("$", cut.Markup);
    }

    // --- Suffix ---

    [Fact]
    public void Suffix_Renders_Suffix_Text()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Suffix, "kg")
            .Add(n => n.Value, 10.0));

        Assert.Contains("kg", cut.Markup);
    }

    [Fact]
    public void Prefix_And_Suffix_Both_Render()
    {
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Prefix, "$")
            .Add(n => n.Suffix, "USD")
            .Add(n => n.Value, 99.0));

        Assert.Contains("$", cut.Markup);
        Assert.Contains("USD", cut.Markup);
    }

    // --- #176: clear to null + culture-aware parse ---

    [Fact]
    public void Clearing_Input_Sets_Value_To_Null()
    {
        double? value = 42;
        var changedToNull = false;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 42.0)
            .Add(n => n.ValueChanged, v => { value = v; changedToNull = v is null; }));

        cut.Find("input[type='number']").Change("");

        Assert.Null(value);
        Assert.True(changedToNull);
    }

    [Fact]
    public void Whitespace_Input_Sets_Value_To_Null()
    {
        double? value = 7;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 7.0)
            .Add(n => n.ValueChanged, v => value = v));

        cut.Find("input[type='number']").Change("   ");

        Assert.Null(value);
    }

    [Fact]
    public void Clearing_When_Already_Null_Does_Not_Refire()
    {
        var callCount = 0;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, (double?)null)
            .Add(n => n.ValueChanged, _ => callCount++));

        cut.Find("input[type='number']").Change("");

        Assert.Equal(0, callCount);
    }

    [Fact]
    public void Parses_Invariant_Decimal()
    {
        double? value = null;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.ValueChanged, v => value = v));

        cut.Find("input[type='number']").Change("3.5");

        Assert.Equal(3.5, value);
    }

    [Fact]
    public void Parses_Culture_Comma_Decimal_As_Fallback()
    {
        double? value = null;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Culture, new System.Globalization.CultureInfo("de-DE"))
            .Add(n => n.ValueChanged, v => value = v));

        // A de-DE user typing a comma decimal must round-trip to 1.5 (the native
        // numeric input reports invariant, but FormatType=Text / programmatic
        // input can deliver culture-formatted text).
        cut.Find("input[type='number']").Change("1,5");

        Assert.Equal(1.5, value);
    }

    [Fact]
    public void Parsed_Value_Is_Clamped_To_Max()
    {
        double? value = null;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Max, 10.0)
            .Add(n => n.ValueChanged, v => value = v));

        cut.Find("input[type='number']").Change("999");

        Assert.Equal(10, value);
    }

    // --- #156: fractional-Step increment must not accumulate FP drift when Precision is null ---

    [Fact]
    public void Increment_Fractional_Step_Without_Precision_Does_Not_Drift()
    {
        // 0.2 + 0.1 is 0.30000000000000004 in raw binary double. Without the fix the
        // stepped value is emitted (and printed) verbatim; with it, the result snaps
        // to Step's decimal scale -> exactly 0.3.
        double? value = null;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 0.2)
            .Add(n => n.Step, 0.1)
            .Add(n => n.ValueChanged, v => value = v));

        cut.Find("button[aria-label='Increase']").Click();

        Assert.Equal(0.3, value);
    }

    [Fact]
    public void Increment_Fractional_Step_Renders_Clean_DisplayValue()
    {
        // The native <input> prints DisplayValue (the raw double) directly, so the
        // drift is user-visible in the markup. After the fix the value attribute is
        // the clean "0.3", not "0.30000000000000004".
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 0.2)
            .Add(n => n.Step, 0.1));

        cut.Find("button[aria-label='Increase']").Click();

        var rendered = cut.Find("input[type='number']").GetAttribute("value");
        Assert.Equal("0.3", rendered);
        Assert.DoesNotContain("0.30000", rendered);
    }

    [Fact]
    public void Decrement_Fractional_Step_Without_Precision_Does_Not_Drift()
    {
        double? value = null;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 0.3)
            .Add(n => n.Step, 0.1)
            .Add(n => n.ValueChanged, v => value = v));

        cut.Find("button[aria-label='Decrease']").Click();

        Assert.Equal(0.2, value);
    }

    [Fact]
    public void Integer_Step_Increment_Still_Increments_By_One()
    {
        // Normal-path regression guard: an integer Step keeps whole-number behaviour
        // (StepDecimalDigits returns 0, so RoundStepped is a no-op).
        double? value = null;
        var cut = _ctx.Render<Lumeo.NumberInput>(p => p
            .Add(n => n.Value, 5.0)
            .Add(n => n.ValueChanged, v => value = v));

        cut.Find("button[aria-label='Increase']").Click();

        Assert.Equal(6, value);
    }
}
