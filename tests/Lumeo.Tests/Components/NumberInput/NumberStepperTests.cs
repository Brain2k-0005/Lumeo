using Xunit;
using Lumeo;

namespace Lumeo.Tests.Components.NumberInput;

/// <summary>
/// Pure unit coverage for <see cref="NumberStepper"/> — the clamp / FP-cleanup / decimal-digit
/// helpers shared by <c>NumberInput</c> and the generic <c>Input</c>'s vertical steppers.
/// </summary>
public class NumberStepperTests
{
    // --- Clamp ---

    [Fact]
    public void Clamp_Below_Min_Returns_Min()
    {
        Assert.Equal(0.0, NumberStepper.Clamp(-5, 0, 10));
    }

    [Fact]
    public void Clamp_Above_Max_Returns_Max()
    {
        Assert.Equal(10.0, NumberStepper.Clamp(999, 0, 10));
    }

    [Fact]
    public void Clamp_Within_Bounds_Returns_Value_Unchanged()
    {
        Assert.Equal(5.0, NumberStepper.Clamp(5, 0, 10));
    }

    [Fact]
    public void Clamp_With_Null_Min_And_Max_Returns_Value_Unchanged()
    {
        Assert.Equal(1234.5, NumberStepper.Clamp(1234.5, null, null));
    }

    [Fact]
    public void Clamp_With_Only_Min_Set_Still_Bounds_Below()
    {
        Assert.Equal(2.0, NumberStepper.Clamp(1, 2, null));
    }

    [Fact]
    public void Clamp_With_Only_Max_Set_Still_Bounds_Above()
    {
        Assert.Equal(2.0, NumberStepper.Clamp(3, null, 2));
    }

    // --- RoundStepped (FP cleanup) ---

    [Fact]
    public void RoundStepped_Cleans_Up_0_1_Plus_0_2_Drift()
    {
        // 0.1 + 0.2 == 0.30000000000000004 in raw binary double.
        var raw = 0.1 + 0.2;
        var cleaned = NumberStepper.RoundStepped(raw, 0.1, null);
        Assert.Equal(0.3, cleaned);
    }

    [Fact]
    public void RoundStepped_Repeated_0_1_Steps_Stay_Clean()
    {
        double value = 0;
        for (var i = 0; i < 5; i++)
        {
            value = NumberStepper.RoundStepped(value + 0.1, 0.1, null);
        }
        Assert.Equal(0.5, value);
    }

    [Fact]
    public void RoundStepped_With_Precision_Set_Is_A_NoOp()
    {
        var raw = 0.1 + 0.2;
        // Precision set => caller (SetValue) already rounds; RoundStepped must not touch it.
        var result = NumberStepper.RoundStepped(raw, 0.1, 2);
        Assert.Equal(raw, result);
    }

    [Fact]
    public void RoundStepped_With_Integer_Step_Is_A_NoOp()
    {
        Assert.Equal(6.0, NumberStepper.RoundStepped(6.0, 1, null));
    }

    // --- StepDecimalDigits ---

    [Fact]
    public void StepDecimalDigits_Integer_Step_Is_Zero()
    {
        Assert.Equal(0, NumberStepper.StepDecimalDigits(1));
    }

    [Fact]
    public void StepDecimalDigits_Decimal_Step_Counts_Fractional_Digits()
    {
        Assert.Equal(1, NumberStepper.StepDecimalDigits(0.1));
        Assert.Equal(2, NumberStepper.StepDecimalDigits(0.25));
    }

    [Fact]
    public void StepDecimalDigits_Scientific_Notation_Step_Returns_Zero()
    {
        // Very small/large steps may round-trip via "R" as scientific notation (e.g. 1E-20);
        // StepDecimalDigits treats those as 0 fractional digits rather than mis-parsing them.
        Assert.Equal(0, NumberStepper.StepDecimalDigits(1e-20));
    }

    [Fact]
    public void StepDecimalDigits_Clamps_To_15()
    {
        // A step with more than 15 fractional digits (as far as double precision allows) must
        // not push Math.Round past its accepted 0..15 digits range.
        var digits = NumberStepper.StepDecimalDigits(0.1234567890123456789);
        Assert.InRange(digits, 0, 15);
    }
}
