namespace Lumeo;

/// <summary>
/// Pure stacking/grouping/domain math shared by every category-based native
/// Cartesian type (Line, Area, Bar, Mixed, Waterfall) — kept out of
/// <c>CartesianChartHost</c>'s Razor markup so it stays a plain, deterministic,
/// unit-testable function set (mirroring how the Wave-0 core itself is "almost
/// entirely pure functions"). None of this touches ECharts/JS; it only decides
/// WHAT to draw, in domain units — <c>CartesianChartHost</c> maps the result
/// through a scale to pixels.
/// </summary>
internal static class NativeCartesianLayout
{
    /// <summary>One series' resolved band/stack geometry at a given category index.</summary>
    public readonly record struct StackedExtent(double Bottom, double Top);

    /// <summary>
    /// Computes the cumulative stacked (bottom,top) for every Stacked=true
    /// series of the given <paramref name="kind"/>, in list order — same
    /// "running total, series-order-dependent" semantics as ECharts'
    /// <c>stack:"total"</c>. Non-stacked series of the same kind are returned
    /// with Bottom=0,Top=value (independent bars/areas, no stacking).
    /// A null value at an index contributes 0 to the running stack AND
    /// produces a null-equivalent (Bottom==Top==running total at that point,
    /// i.e. an invisible zero-height segment) for its own row — callers skip
    /// rendering at indices where the SOURCE value was null.
    /// </summary>
    public static Dictionary<int, StackedExtent[]> ComputeStackedExtents(
        IReadOnlyList<NativeCartesianSeries> series, NativeCartesianSeriesKind kind, int categoryCount)
    {
        var result = new Dictionary<int, StackedExtent[]>();
        var runningTotal = new double[categoryCount];

        for (var s = 0; s < series.Count; s++)
        {
            var ser = series[s];
            if (ser.Kind != kind) continue;

            var extents = new StackedExtent[categoryCount];
            for (var c = 0; c < categoryCount; c++)
            {
                var raw = c < ser.Values.Count ? ser.Values[c] : null;
                var v = raw ?? 0;

                if (ser.Stacked)
                {
                    var bottom = runningTotal[c];
                    var top = bottom + v;
                    extents[c] = new StackedExtent(bottom, top);
                    runningTotal[c] = top;
                }
                else
                {
                    extents[c] = new StackedExtent(0, v);
                }
            }
            result[s] = extents;
        }
        return result;
    }

    /// <summary>Bar-slot assignment: series sharing a stack collapse into ONE
    /// slot (one full-width-in-that-slot bar per category, layered); every
    /// non-stacked Bar series gets its own slot, divided evenly across the
    /// category band. Returns (slotIndex, slotCount) per series index (Bar
    /// series only — non-Bar series aren't present in the result).</summary>
    public static Dictionary<int, (int SlotIndex, int SlotCount)> ComputeBarSlots(
        IReadOnlyList<NativeCartesianSeries> series)
    {
        var result = new Dictionary<int, (int, int)>();
        var hasStackedSlot = false;
        var nonStackedIndices = new List<int>();

        for (var i = 0; i < series.Count; i++)
        {
            if (series[i].Kind != NativeCartesianSeriesKind.Bar) continue;
            if (series[i].Stacked) hasStackedSlot = true;
            else nonStackedIndices.Add(i);
        }

        var slotCount = (hasStackedSlot ? 1 : 0) + nonStackedIndices.Count;
        if (slotCount == 0) return result;

        var nextSlot = 0;
        var stackedSlot = -1;
        if (hasStackedSlot) stackedSlot = nextSlot++;

        for (var i = 0; i < series.Count; i++)
        {
            if (series[i].Kind != NativeCartesianSeriesKind.Bar) continue;
            if (series[i].Stacked)
                result[i] = (stackedSlot, slotCount);
        }
        foreach (var i in nonStackedIndices)
            result[i] = (nextSlot++, slotCount);

        return result;
    }

    /// <summary>
    /// Combined value-axis domain (min,max) across every series assigned to
    /// <paramref name="yAxisIndex"/>, accounting for stacking (a stacked
    /// group's extent is its cumulative top/bottom, not each series' own
    /// raw min/max) and for negative-spanning stacks (Waterfall). Returns
    /// (0,1) for an empty input so callers never divide by a zero-span
    /// domain from no data.
    /// </summary>
    public static (double Min, double Max) ComputeYDomain(
        IReadOnlyList<NativeCartesianSeries> series, int categoryCount, int yAxisIndex)
    {
        var relevant = series.Where(s => s.YAxisIndex == yAxisIndex).ToList();
        if (relevant.Count == 0) return (0, 1);

        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        var touched = false;

        foreach (var kind in new[] { NativeCartesianSeriesKind.Bar, NativeCartesianSeriesKind.Area, NativeCartesianSeriesKind.Line })
        {
            var extents = ComputeStackedExtents(relevant, kind, categoryCount);
            foreach (var (seriesIdx, arr) in extents)
            {
                var ser = relevant[seriesIdx];
                for (var c = 0; c < categoryCount; c++)
                {
                    if (c >= ser.Values.Count || ser.Values[c] is null) continue;
                    touched = true;
                    if (arr[c].Bottom < min) min = arr[c].Bottom;
                    if (arr[c].Bottom > max) max = arr[c].Bottom;
                    if (arr[c].Top < min) min = arr[c].Top;
                    if (arr[c].Top > max) max = arr[c].Top;
                }
            }
        }

        if (!touched) return (0, 1);
        // Matches the legacy wrapper's own behavior: none of Line/Bar/Area/
        // Mixed/Waterfall's EChartAxis set `scale:true`, so ECharts' default
        // (`scale:false`) forces 0 into the value-axis range on every one of
        // these types today — replicated here, not a native deviation.
        if (min > 0) min = 0;
        if (max < 0) max = 0;
        if (min == max) max = min + 1;
        return (min, max);
    }
}
