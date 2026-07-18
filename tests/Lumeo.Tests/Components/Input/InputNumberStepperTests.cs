using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Input;

/// <summary>
/// Coverage for the generic Input's vertical ▲▼ steppers, rendered for <c>type="number"</c>
/// (detected case-insensitively through the attribute splat — Input has no typed Type param).
/// They replace the native browser spinner (always hidden for number inputs) and share the
/// NumberInput's clamp / FP-cleanup core via <see cref="Lumeo.NumberStepper"/>.
/// </summary>
public class InputNumberStepperTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public InputNumberStepperTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- (a) steppers render for both attribute-key casings ---

    [Fact]
    public void Steppers_Render_For_Uppercase_Type_Attribute()
    {
        // Mirrors how <Input Type="number" /> lands through the Razor compiler when the
        // caller uses PascalCase — verified empirically: AdditionalAttributes["Type"] = "number".
        var cut = _ctx.Render<L.Input>(p => p.AddUnmatched("Type", "number"));

        Assert.Equal(2, cut.FindAll("button").Count);
        Assert.NotNull(cut.Find("input[type='number']"));
    }

    [Fact]
    public void Steppers_Render_For_Lowercase_Type_Attribute()
    {
        var cut = _ctx.Render<L.Input>(p => p.AddUnmatched("type", "number"));

        Assert.Equal(2, cut.FindAll("button").Count);
        Assert.NotNull(cut.Find("input[type='number']"));
    }

    [Fact]
    public void Steppers_Render_For_Mixed_Case_Value()
    {
        var cut = _ctx.Render<L.Input>(p => p.AddUnmatched("type", "NuMbEr"));

        Assert.Equal(2, cut.FindAll("button").Count);
    }

    // --- (b) steppers do NOT render for other types ---

    [Fact]
    public void Steppers_Do_Not_Render_For_Text_Input()
    {
        var cut = _ctx.Render<L.Input>();
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void Steppers_Do_Not_Render_For_Search_Variant()
    {
        var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Variant, L.Input.InputVariant.Search));
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void Steppers_Do_Not_Render_For_File_Input()
    {
        var cut = _ctx.Render<L.Input>(p => p.AddUnmatched("type", "file"));
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void Steppers_Do_Not_Render_When_No_Type_Attribute()
    {
        var cut = _ctx.Render<L.Input>(p => p.Add(i => i.Value, "hello"));
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void Steppers_Do_Not_Render_For_Email_Type()
    {
        var cut = _ctx.Render<L.Input>(p => p.AddUnmatched("type", "email"));
        Assert.Empty(cut.FindAll("button"));
    }

    // --- (c) increment / decrement wiring ---

    [Fact]
    public void Increase_Button_Increments_Value_By_Default_Step_And_Fires_ValueChanged()
    {
        string? committed = null;
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .Add(i => i.Value, "5")
            .Add(i => i.ValueChanged, v => committed = v));

        cut.Find("button[aria-label='Increase']").Click();

        Assert.Equal("6", committed);
    }

    [Fact]
    public void Decrease_Button_Decrements_Value_By_Default_Step_And_Fires_ValueChanged()
    {
        string? committed = null;
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .Add(i => i.Value, "5")
            .Add(i => i.ValueChanged, v => committed = v));

        cut.Find("button[aria-label='Decrease']").Click();

        Assert.Equal("4", committed);
    }

    [Fact]
    public void Increase_On_Empty_Value_Treats_It_As_Zero()
    {
        string? committed = null;
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .Add(i => i.ValueChanged, v => committed = v));

        cut.Find("button[aria-label='Increase']").Click();

        Assert.Equal("1", committed);
    }

    [Fact]
    public void Increase_Respects_Custom_Step_Attribute()
    {
        string? committed = null;
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("step", "0.5")
            .Add(i => i.Value, "1")
            .Add(i => i.ValueChanged, v => committed = v));

        cut.Find("button[aria-label='Increase']").Click();

        Assert.Equal("1.5", committed);
    }

    [Fact]
    public void Fractional_Step_Repeated_Increment_Stays_Clean()
    {
        // Regression guard mirroring NumberInput's #156: 0.1 + 0.2 must not print as
        // 0.30000000000000004 through the shared NumberStepper FP cleanup.
        string? committed = null;
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("step", "0.1")
            .Add(i => i.Value, "0.2")
            .Add(i => i.ValueChanged, v => committed = v));

        cut.Find("button[aria-label='Increase']").Click();

        Assert.Equal("0.3", committed);
    }

    // --- (d) min/max/step from attributes; buttons disable at bounds ---

    [Fact]
    public void Increase_Clamps_To_Max_Attribute()
    {
        string? committed = null;
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("max", "10")
            .Add(i => i.Value, "9")
            .Add(i => i.ValueChanged, v => committed = v));

        cut.Find("button[aria-label='Increase']").Click();

        Assert.Equal("10", committed);
    }

    [Fact]
    public void Decrease_Clamps_To_Min_Attribute()
    {
        string? committed = null;
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("min", "0")
            .Add(i => i.Value, "1")
            .Add(i => i.ValueChanged, v => committed = v));

        cut.Find("button[aria-label='Decrease']").Click();

        Assert.Equal("0", committed);
    }

    [Fact]
    public void Increase_Falls_Back_To_Step_One_When_Step_Is_Zero()
    {
        // A step="0" must not freeze the button — native <input type="number"> treats a
        // non-positive step as the default 1 (CodeRabbit finding on #378).
        string? committed = null;
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("step", "0")
            .Add(i => i.Value, "5")
            .Add(i => i.ValueChanged, v => committed = v));

        cut.Find("button[aria-label='Increase']").Click();

        Assert.Equal("6", committed);
    }

    [Fact]
    public void Increase_Falls_Back_To_Step_One_When_Step_Is_Negative()
    {
        // A negative step must not reverse the control (Increase would otherwise decrement).
        string? committed = null;
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("step", "-2")
            .Add(i => i.Value, "5")
            .Add(i => i.ValueChanged, v => committed = v));

        cut.Find("button[aria-label='Increase']").Click();

        Assert.Equal("6", committed);
    }

    [Fact]
    public void Increase_Button_Disabled_At_Max()
    {
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("max", "10")
            .Add(i => i.Value, "10"));

        Assert.True(cut.Find("button[aria-label='Increase']").HasAttribute("disabled"));
        Assert.False(cut.Find("button[aria-label='Decrease']").HasAttribute("disabled"));
    }

    [Fact]
    public void Decrease_Button_Disabled_At_Min()
    {
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("min", "0")
            .Add(i => i.Value, "0"));

        Assert.True(cut.Find("button[aria-label='Decrease']").HasAttribute("disabled"));
        Assert.False(cut.Find("button[aria-label='Increase']").HasAttribute("disabled"));
    }

    [Fact]
    public void Buttons_Not_Disabled_At_Bounds_When_Value_Empty()
    {
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("min", "0")
            .AddUnmatched("max", "10"));

        Assert.False(cut.Find("button[aria-label='Increase']").HasAttribute("disabled"));
        Assert.False(cut.Find("button[aria-label='Decrease']").HasAttribute("disabled"));
    }

    [Fact]
    public void Both_Buttons_Disabled_When_Input_Disabled()
    {
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .Add(i => i.Disabled, true)
            .Add(i => i.Value, "5"));

        Assert.All(cut.FindAll("button"), b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Disabled_Increase_Click_Does_Not_Fire_ValueChanged()
    {
        var callCount = 0;
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .Add(i => i.Disabled, true)
            .Add(i => i.Value, "5")
            .Add(i => i.ValueChanged, _ => callCount++));

        // A disabled <button disabled> won't dispatch a click in a real browser; bUnit's
        // .Click() bypasses that, so ApplyStep's own Disabled guard is what's under test here.
        cut.Find("button[aria-label='Increase']").Click();

        Assert.Equal(0, callCount);
    }

    // --- (e) ShowStepButtons=false ---

    [Fact]
    public void ShowStepButtons_False_Renders_No_Buttons()
    {
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .Add(i => i.ShowStepButtons, false));

        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void ShowStepButtons_False_Still_Hides_Native_Spinner()
    {
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .Add(i => i.ShowStepButtons, false));

        var cls = cut.Find("input").GetAttribute("class");
        Assert.Contains("[appearance:textfield]", cls);
    }

    [Fact]
    public void ShowStepButtons_False_Still_Renders_A_Number_Input()
    {
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .Add(i => i.ShowStepButtons, false)
            .Add(i => i.Value, "3"));

        var input = cut.Find("input");
        Assert.Equal("number", input.GetAttribute("type"));
        Assert.Equal("3", input.GetAttribute("value"));
    }

    // --- (f) spinner-hiding appearance classes always present when number ---

    [Fact]
    public void Number_Input_Always_Carries_Spinner_Hiding_Classes()
    {
        var cut = _ctx.Render<L.Input>(p => p.AddUnmatched("type", "number"));

        var cls = cut.Find("input").GetAttribute("class");
        Assert.Contains("[appearance:textfield]", cls);
        Assert.Contains("[&::-webkit-inner-spin-button]:appearance-none", cls);
        Assert.Contains("[&::-webkit-outer-spin-button]:appearance-none", cls);
    }

    [Fact]
    public void Text_Input_Does_Not_Carry_Spinner_Hiding_Classes()
    {
        var cut = _ctx.Render<L.Input>();

        var cls = cut.Find("input").GetAttribute("class");
        Assert.DoesNotContain("[appearance:textfield]", cls);
    }

    // --- (g) a11y: localized aria-labels + tabindex=-1 ---

    [Fact]
    public void Buttons_Have_Localized_Aria_Labels()
    {
        var cut = _ctx.Render<L.Input>(p => p.AddUnmatched("type", "number"));

        Assert.NotNull(cut.Find("button[aria-label='Increase']"));
        Assert.NotNull(cut.Find("button[aria-label='Decrease']"));
    }

    [Fact]
    public void Buttons_Have_TabIndex_Minus_One()
    {
        var cut = _ctx.Render<L.Input>(p => p.AddUnmatched("type", "number"));

        foreach (var button in cut.FindAll("button"))
        {
            Assert.Equal("-1", button.GetAttribute("tabindex"));
        }
    }

    [Fact]
    public void Buttons_Are_Type_Button_So_They_Never_Submit_A_Form()
    {
        var cut = _ctx.Render<L.Input>(p => p.AddUnmatched("type", "number"));

        foreach (var button in cut.FindAll("button"))
        {
            Assert.Equal("button", button.GetAttribute("type"));
        }
    }

    // --- (h) splatted readonly / disabled — Codex P2 x2 ---
    // A number input can be made non-editable two ways the steppers previously ignored (they
    // only checked the typed Disabled parameter): `readonly` splatted via AdditionalAttributes
    // makes the <input> non-editable but left the ▲▼ buttons clickable and mutating _current;
    // `disabled` splatted the same way (instead of via the Disabled parameter) disabled the
    // <input> via @attributes but likewise left the buttons enabled.

    [Fact]
    public void Splatted_Readonly_Disables_Both_Stepper_Buttons()
    {
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("readonly", true)
            .Add(i => i.Value, "5"));

        Assert.All(cut.FindAll("button"), b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Splatted_Readonly_Makes_ApplyStep_A_NoOp()
    {
        var callCount = 0;
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("readonly", true)
            .Add(i => i.Value, "5")
            .Add(i => i.ValueChanged, _ => callCount++));

        cut.Find("button[aria-label='Increase']").Click();

        Assert.Equal(0, callCount);
    }

    [Fact]
    public void Splatted_Disabled_Disables_Both_Stepper_Buttons()
    {
        // Splatted (not via the typed Disabled parameter) — still disables the whole <input>
        // through @attributes, so the buttons must follow.
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("disabled", true)
            .Add(i => i.Value, "5"));

        Assert.All(cut.FindAll("button"), b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Splatted_Disabled_Makes_ApplyStep_A_NoOp()
    {
        var callCount = 0;
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("disabled", true)
            .Add(i => i.Value, "5")
            .Add(i => i.ValueChanged, _ => callCount++));

        cut.Find("button[aria-label='Increase']").Click();

        Assert.Equal(0, callCount);
    }

    [Fact]
    public void Splatted_Readonly_False_String_Leaves_Buttons_Enabled()
    {
        // HTML boolean-attribute semantics: an explicit "false" string means "not set" —
        // only presence / true / empty-string count as set.
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("readonly", "false")
            .Add(i => i.Value, "5"));

        Assert.All(cut.FindAll("button"), b => Assert.False(b.HasAttribute("disabled")));
    }

    // --- (i) invariant parsing of BOUND numeric min/max/step attributes — Codex P2 ---
    // A consumer may bind a numeric value directly (`min="@someDouble"`) instead of a string
    // literal — kv.Value then arrives as an actual double, not a string. Under a comma-decimal
    // culture, .ToString() on that double would render "0,5" and the invariant parse would
    // misread the comma as a thousands separator (-> 500 instead of 0.5).

    [Fact]
    public void Bound_Double_Step_Parses_Correctly_Under_Comma_Decimal_Culture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            string? committed = null;
            var cut = _ctx.Render<L.Input>(p => p
                .AddUnmatched("type", "number")
                .AddUnmatched("step", 0.5d)
                .Add(i => i.Value, "1")
                .Add(i => i.ValueChanged, v => committed = v));

            cut.Find("button[aria-label='Increase']").Click();

            Assert.Equal("1.5", committed);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Bound_Double_Min_Clamps_Correctly_Under_Comma_Decimal_Culture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            string? committed = null;
            var cut = _ctx.Render<L.Input>(p => p
                .AddUnmatched("type", "number")
                .AddUnmatched("min", 0.5d)
                .AddUnmatched("step", 0.1d)
                .Add(i => i.Value, "0.5")
                .Add(i => i.ValueChanged, v => committed = v));

            // At the (bound double) min already — Decrease must clamp right back to 0.5,
            // not misread "0,5" -> 500 and leave the field effectively unbounded downward.
            Assert.True(cut.Find("button[aria-label='Decrease']").HasAttribute("disabled"));

            cut.Find("button[aria-label='Increase']").Click();
            Assert.Equal("0.6", committed);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Bound_Int_Max_Parses_Correctly()
    {
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .AddUnmatched("max", 10)
            .Add(i => i.Value, "10"));

        Assert.True(cut.Find("button[aria-label='Increase']").HasAttribute("disabled"));
    }

    // --- (j) disabled opacity treatment — Codex P3 ---
    // Routing type="number" through the wrapper branch (Prefix/Suffix/Search/Clearable/Number
    // all share it) must not drop the reduced-opacity disabled look a plain disabled text Input
    // gets from CssClass's disabled:opacity-50.

    [Fact]
    public void Disabled_Number_Input_Has_Reduced_Opacity_Class()
    {
        var cut = _ctx.Render<L.Input>(p => p
            .AddUnmatched("type", "number")
            .Add(i => i.Disabled, true));

        var cls = cut.Find("input").GetAttribute("class");
        Assert.Contains("disabled:opacity-50", cls);
    }
}
