using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Core;

public class NiceTicksTests
{
    [Fact]
    public void ZeroToSeven_Produces_Even_Step_Two_Ticks()
    {
        var ticks = L.NiceTicks.Compute(0, 7, targetCount: 5);

        Assert.Equal(new[] { 0.0, 2.0, 4.0, 6.0, 8.0 }, ticks);
    }

    [Fact]
    public void Tiny_Range_Below_One_Produces_Half_Millesimal_Steps()
    {
        // 0.001–0.003 is where a naive "count decimal places" tick generator
        // usually embarrasses itself with float noise (0.0025000000000000005
        // instead of 0.0025). Assert exact values.
        var ticks = L.NiceTicks.Compute(0.001, 0.003, targetCount: 5);

        Assert.Equal(new[] { 0.001, 0.0015, 0.002, 0.0025, 0.003 }, ticks);
    }

    [Fact]
    public void Large_Range_One_To_One_Million_Produces_Round_Ticks()
    {
        var ticks = L.NiceTicks.Compute(1, 1_000_000, targetCount: 5);

        Assert.Equal(new[] { 0.0, 200_000.0, 400_000.0, 600_000.0, 800_000.0, 1_000_000.0 }, ticks);
    }

    [Fact]
    public void Negative_Spanning_Range_Crosses_Zero_Cleanly()
    {
        var ticks = L.NiceTicks.Compute(-50, 30, targetCount: 5);

        Assert.Equal(new[] { -60.0, -40.0, -20.0, 0.0, 20.0, 40.0 }, ticks);
        Assert.Contains(0.0, ticks);
    }

    [Fact]
    public void Single_Point_Nonzero_Domain_Returns_Symmetric_Three_Ticks()
    {
        var ticks = L.NiceTicks.Compute(42, 42);

        Assert.Equal(new[] { 32.0, 42.0, 52.0 }, ticks);
    }

    [Fact]
    public void Single_Point_Zero_Domain_Returns_Minus_One_Zero_One()
    {
        var ticks = L.NiceTicks.Compute(0, 0);

        Assert.Equal(new[] { -1.0, 0.0, 1.0 }, ticks);
    }

    [Fact]
    public void Reversed_Min_Max_Is_Normalized()
    {
        var forward = L.NiceTicks.Compute(0, 7, 5);
        var reversed = L.NiceTicks.Compute(7, 0, 5);

        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void First_And_Last_Tick_Always_Cover_The_Domain()
    {
        var ticks = L.NiceTicks.Compute(3.2, 91.7, targetCount: 6);

        Assert.True(ticks[0] <= 3.2);
        Assert.True(ticks[^1] >= 91.7);
    }

    // --- Disable-check: predicted-vs-actual ---
    // NiceNumber(range, round:false) is the "size the overall span" step —
    // disabling its rounding-up branch (forcing niceFraction to always be 1)
    // would shrink the computed range below the actual data span, so the
    // final tick set would stop SHORT of covering [min,max]. Predicted: the
    // 0–7 case's last tick would land at 7 or below instead of 8.
    [Fact]
    public void DisableCheck_NiceNumber_Without_RoundUp_Branch_Would_Undercover_Domain()
    {
        static double BrokenNiceNumber(double range)
        {
            // Same as NiceTicks.NiceNumber(range, round:false) but with the
            // "round up to the next nice fraction" behavior disabled — always
            // picks 1×10^n regardless of how much bigger `range` is.
            var exponent = Math.Floor(Math.Log10(range));
            return 1 * Math.Pow(10, exponent);
        }

        var brokenRange = BrokenNiceNumber(7); // predicted: 1 (not 10)
        Assert.Equal(1.0, brokenRange);
        Assert.True(brokenRange < 7, "the disabled branch must under-cover the actual 0-7 span");

        // The real implementation must NOT reproduce this bug.
        var realRange = L.NiceTicks.NiceNumber(7, round: false);
        Assert.True(realRange >= 7);
    }
}
