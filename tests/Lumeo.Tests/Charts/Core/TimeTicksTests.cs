using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Core;

public class TimeTicksTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OneHourSpan_Selects_FiveMinute_Step()
    {
        var step = L.TimeTicks.SelectStep(Epoch, Epoch.AddHours(1), targetCount: 12);

        Assert.Equal(L.TimeTickUnit.Minute, step.Unit);
        Assert.Equal(5, step.Multiple);
    }

    [Fact]
    public void OneDaySpan_Selects_ThreeHour_Step()
    {
        var step = L.TimeTicks.SelectStep(Epoch, Epoch.AddDays(1), targetCount: 8);

        Assert.Equal(L.TimeTickUnit.Hour, step.Unit);
        Assert.Equal(3, step.Multiple);
    }

    [Fact]
    public void OneYearSpan_Selects_Month_Step()
    {
        var step = L.TimeTicks.SelectStep(Epoch, Epoch.AddYears(1), targetCount: 12);

        Assert.Equal(L.TimeTickUnit.Month, step.Unit);
        Assert.Equal(1, step.Multiple);
    }

    [Fact]
    public void Ticks_Align_To_Hour_Boundaries_Not_An_Arbitrary_Offset()
    {
        var start = Epoch.AddMinutes(37); // deliberately NOT on a boundary
        var end = start.AddHours(6);
        var ticks = L.TimeTicks.Compute(start, end, targetCount: 6);

        // Every generated tick after alignment should land on a whole hour.
        Assert.All(ticks, t => Assert.Equal(0, t.Minute));
    }

    [Fact]
    public void Ticks_Cover_The_Full_Requested_Span()
    {
        var end = Epoch.AddDays(3);
        var ticks = L.TimeTicks.Compute(Epoch, end, targetCount: 5);

        Assert.NotEmpty(ticks);
        Assert.True(ticks[0] <= Epoch, "first tick must not start after the domain begins");
        Assert.True(ticks[^1] >= end.AddHours(-24), "last tick must reach at least within one step of the domain end");
    }

    [Fact]
    public void Zero_Span_Domain_Returns_Single_Tick_Without_Throwing()
    {
        var ticks = L.TimeTicks.Compute(Epoch, Epoch, targetCount: 5);

        Assert.Single(ticks);
        Assert.Equal(Epoch, ticks[0]);
    }

    [Fact]
    public void Reversed_Start_End_Is_Normalized()
    {
        var later = Epoch.AddDays(2);
        var forward = L.TimeTicks.Compute(Epoch, later, 5);
        var reversed = L.TimeTicks.Compute(later, Epoch, 5);

        Assert.Equal(forward, reversed);
    }

    // --- Disable-check ---
    // If month/year advancement used a FIXED 30-day increment instead of
    // calendar-aware AddMonths, a 1-year span with a month step would produce
    // 12 ticks whose day-of-month drifts (Jan 1, Jan 31, Mar 2, ...) instead
    // of staying on the 1st every time. Predicted: drift after a few steps.
    [Fact]
    public void DisableCheck_Fixed_ThirtyDay_Increment_Would_Drift_Off_The_First()
    {
        var cur = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 2; i++) cur = cur.AddDays(30); // the broken alternative: Jan 1 -> Jan 31 -> Mar 2
        Assert.NotEqual(1, cur.Day); // predicted: drifted off the 1st

        // The real calendar-aware TimeTicks must NOT drift.
        var ticks = L.TimeTicks.Compute(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            targetCount: 3);
        Assert.All(ticks, t => Assert.Equal(1, t.Day));
    }
}
