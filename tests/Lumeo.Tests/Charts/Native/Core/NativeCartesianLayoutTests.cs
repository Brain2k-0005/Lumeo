using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native.Core;

/// <summary>Covers <see cref="L.NativeCartesianLayout"/> — the pure stacking/
/// grouping/domain math shared by every category-based native Cartesian type.
/// Each test predicts a concrete value that would be wrong if the stacking
/// or grouping logic were disabled (e.g. reverted to "always independent"),
/// per this task's rigor standard.</summary>
public class NativeCartesianLayoutTests
{
    private static L.NativeCartesianSeries Bar(string name, bool stacked, params double?[] values) => new()
    {
        Name = name,
        Kind = L.NativeCartesianSeriesKind.Bar,
        Values = values,
        Stacked = stacked,
    };

    [Fact]
    public void Stacked_Bars_Accumulate_In_Series_Order()
    {
        // Series A: [10, 20]; Series B (stacked): [5, 5]
        // Disable-check: if stacking were disabled (independent Bottom=0 for both),
        // B's extents would be (0,5)/(0,5) instead of (10,15)/(20,25).
        var series = new List<L.NativeCartesianSeries>
        {
            Bar("A", stacked: true, 10, 20),
            Bar("B", stacked: true, 5, 5),
        };

        var extents = L.NativeCartesianLayout.ComputeStackedExtents(series, L.NativeCartesianSeriesKind.Bar, 2);

        Assert.Equal(0, extents[0][0].Bottom);
        Assert.Equal(10, extents[0][0].Top);
        Assert.Equal(10, extents[1][0].Bottom);
        Assert.Equal(15, extents[1][0].Top);   // WRONG value if stacking disabled: 5

        Assert.Equal(0, extents[0][1].Bottom);
        Assert.Equal(20, extents[0][1].Top);
        Assert.Equal(20, extents[1][1].Bottom);
        Assert.Equal(25, extents[1][1].Top);   // WRONG value if stacking disabled: 5
    }

    [Fact]
    public void NonStacked_Bars_Are_Independent_From_Zero()
    {
        var series = new List<L.NativeCartesianSeries> { Bar("A", stacked: false, 10), Bar("B", stacked: false, 30) };
        var extents = L.NativeCartesianLayout.ComputeStackedExtents(series, L.NativeCartesianSeriesKind.Bar, 1);

        Assert.Equal((0, 10), (extents[0][0].Bottom, extents[0][0].Top));
        Assert.Equal((0, 30), (extents[1][0].Bottom, extents[1][0].Top));
    }

    [Fact]
    public void Null_Value_Contributes_Zero_To_The_Running_Stack()
    {
        // A[0] = null (gap), B[0] = 7, stacked. B must still land at (0,7) — a
        // gap doesn't leave a "hole" offset in the stack for the series after it.
        var series = new List<L.NativeCartesianSeries> { Bar("A", stacked: true, (double?)null), Bar("B", stacked: true, 7) };
        var extents = L.NativeCartesianLayout.ComputeStackedExtents(series, L.NativeCartesianSeriesKind.Bar, 1);

        Assert.Equal((0, 0), (extents[0][0].Bottom, extents[0][0].Top));
        Assert.Equal((0, 7), (extents[1][0].Bottom, extents[1][0].Top));
    }

    [Fact]
    public void Waterfall_Style_Negative_Step_Produces_A_Descending_Band()
    {
        // Mirrors NativeWaterfallChart's own floor+delta pair for a single
        // negative step from running total 100 down to 60 (delta = -40).
        var floor = Bar("floor", stacked: true, 60);   // baseVal = running + value = 100 + (-40) = 60
        var delta = Bar("delta", stacked: true, 40);    // |value|
        var extents = L.NativeCartesianLayout.ComputeStackedExtents(new List<L.NativeCartesianSeries> { floor, delta }, L.NativeCartesianSeriesKind.Bar, 1);

        Assert.Equal((0.0, 60.0), (extents[0][0].Bottom, extents[0][0].Top));
        Assert.Equal((60.0, 100.0), (extents[1][0].Bottom, extents[1][0].Top)); // spans [60,100] — the correct visual band
    }

    [Fact]
    public void ComputeBarSlots_Groups_Stacked_Series_Into_One_Shared_Slot()
    {
        var series = new List<L.NativeCartesianSeries>
        {
            Bar("A", stacked: true, 1),
            Bar("B", stacked: true, 2),
            Bar("C", stacked: false, 3),
        };

        var slots = L.NativeCartesianLayout.ComputeBarSlots(series);

        // A and B share slot 0 (both stacked); C gets its own slot.
        // Disable-check: if grouping were disabled (every series its own slot),
        // slotCount would be 3 and A's slotIndex would differ from B's.
        Assert.Equal(2, slots[0].SlotCount);
        Assert.Equal(slots[0].SlotIndex, slots[1].SlotIndex);
        Assert.NotEqual(slots[0].SlotIndex, slots[2].SlotIndex);
    }

    [Fact]
    public void ComputeBarSlots_Gives_Each_NonStacked_Series_Its_Own_Slot()
    {
        var series = new List<L.NativeCartesianSeries> { Bar("A", false, 1), Bar("B", false, 2), Bar("C", false, 3) };
        var slots = L.NativeCartesianLayout.ComputeBarSlots(series);

        Assert.Equal(3, slots[0].SlotCount);
        var indices = new[] { slots[0].SlotIndex, slots[1].SlotIndex, slots[2].SlotIndex };
        Assert.Equal(new[] { 0, 1, 2 }, indices.OrderBy(i => i));
    }

    [Fact]
    public void ComputeYDomain_Includes_Zero_Baseline_For_Positive_Only_Data()
    {
        // Matches the legacy wrapper's ECharts default (scale:false forces 0 into
        // the range). Disable-check: without the zero-forcing, min would be 10.
        var series = new List<L.NativeCartesianSeries> { Bar("A", false, 10, 50) };
        var (min, max) = L.NativeCartesianLayout.ComputeYDomain(series, 2, 0);

        Assert.Equal(0, min);
        Assert.Equal(50, max);
    }

    [Fact]
    public void ComputeYDomain_Includes_Zero_Baseline_For_Negative_Only_Data()
    {
        var series = new List<L.NativeCartesianSeries> { Bar("A", false, -30, -10) };
        var (min, max) = L.NativeCartesianLayout.ComputeYDomain(series, 2, 0);

        Assert.Equal(-30, min);
        Assert.Equal(0, max);
    }

    [Fact]
    public void ComputeYDomain_Uses_Stacked_Extent_Not_Individual_Series_Max()
    {
        // Two stacked series of 40 each: the domain must reach 80 (the stacked
        // top), not 40 (either series' own raw max) — the exact bug a
        // stacking-unaware domain calc would introduce.
        var series = new List<L.NativeCartesianSeries> { Bar("A", true, 40), Bar("B", true, 40) };
        var (_, max) = L.NativeCartesianLayout.ComputeYDomain(series, 1, 0);

        Assert.Equal(80, max);
    }

    [Fact]
    public void ComputeYDomain_Separates_By_YAxisIndex()
    {
        var series = new List<L.NativeCartesianSeries>
        {
            new() { Name = "primary", Kind = L.NativeCartesianSeriesKind.Bar, Values = new double?[] { 10 }, YAxisIndex = 0 },
            new() { Name = "secondary", Kind = L.NativeCartesianSeriesKind.Line, Values = new double?[] { 1000 }, YAxisIndex = 1 },
        };

        var (_, primaryMax) = L.NativeCartesianLayout.ComputeYDomain(series, 1, 0);
        var (_, secondaryMax) = L.NativeCartesianLayout.ComputeYDomain(series, 1, 1);

        Assert.Equal(10, primaryMax);     // unaffected by the secondary axis' huge value
        Assert.Equal(1000, secondaryMax);
    }

    [Fact]
    public void ComputeYDomain_Empty_Input_Returns_Zero_To_One_Fallback()
    {
        var (min, max) = L.NativeCartesianLayout.ComputeYDomain(new List<L.NativeCartesianSeries>(), 0, 0);
        Assert.Equal(0, min);
        Assert.Equal(1, max);
    }
}
