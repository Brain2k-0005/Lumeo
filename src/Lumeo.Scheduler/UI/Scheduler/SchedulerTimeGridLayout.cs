namespace Lumeo.SchedulerKernel;

/// <summary>
/// Cluster + greedy column packing for overlapping timed events within a single day's time
/// grid (Week/Day view). Faithful formalization of the uploaded demo's <c>layoutTimed()</c> —
/// spec §1.3, which this class implements algorithm-for-algorithm (see <see cref="Pack"/>'s own
/// remarks for the step-by-step correspondence).
/// </summary>
internal static class SchedulerTimeGridLayout
{
    /// <summary>
    /// Packs same-day timed events into (column, columnsInCluster) slots so overlapping events
    /// render side-by-side instead of stacking. Input events are (Id, StartMinute, EndMinute)
    /// triples (minutes since midnight; <c>StartMinute &lt; EndMinute</c> is assumed — a
    /// zero-duration event, <c>StartMinute == EndMinute</c>, is handled without throwing but
    /// never overlaps anything, see remarks). Output is one (Id, Column, ColumnsInCluster) triple
    /// per input event, in the same processing order the algorithm sorts them into (start
    /// ascending, end descending) rather than the caller's original order — callers that need the
    /// original order should re-project by Id.
    /// </summary>
    /// <remarks>
    /// Algorithm (spec §1.3), reproduced 1:1 from the uploaded demo's <c>layoutTimed()</c>:
    /// <list type="number">
    /// <item>Sort by StartMinute ascending, ties broken by EndMinute descending (longer events
    /// first — demo: <c>a.startMin - b.startMin || b.endMin - a.endMin</c>).</item>
    /// <item>Walk the sorted list maintaining a running cluster envelope (<c>clusterEnd</c>): if
    /// an event's StartMinute is at or past <c>clusterEnd</c>, flush the current cluster and
    /// start a new one; otherwise the event joins the current cluster and <c>clusterEnd</c>
    /// extends to <c>max(clusterEnd, event.EndMinute)</c> even if THIS event doesn't overlap the
    /// cluster's first event — only the running envelope (transitive overlap). This is what makes
    /// a 3-event "staircase" (each overlapping only its immediate neighbor) still get 3 columns,
    /// not 2 — a naive pairwise-overlap check would wrongly collapse it to 2.</item>
    /// <item>Within its cluster, each event is assigned the lowest column index whose most
    /// recently placed occupant's end time does not exceed (is not strictly greater than) the
    /// event's own start time — <c>end == start</c> at a column boundary is NOT overlap (open
    /// interval), matching the same flush condition in step 2.</item>
    /// <item><c>ColumnsInCluster</c> for every event in a cluster is <c>max(column) + 1</c> across
    /// that cluster, computed once every event in it has been assigned a column.</item>
    /// </list>
    /// Downstream note: <c>ColumnsInCluster</c> returned here is
    /// always &gt;= 1 (even for a lone zero-duration event), so a caller computing
    /// <c>width = 100% / ColumnsInCluster</c> never divides by zero — the zero-duration case
    /// needs no special-casing in <see cref="Pack"/> itself, only downstream renderers need to
    /// avoid rendering a literal 0px-tall chip, which is a view-layer concern, not this method's.
    /// </remarks>
    internal static IReadOnlyList<(string Id, int Column, int ColumnsInCluster)> Pack(
        IReadOnlyList<(string Id, int StartMinute, int EndMinute)> events)
    {
        if (events.Count == 0) return Array.Empty<(string, int, int)>();

        var sorted = events
            .OrderBy(e => e.StartMinute)
            .ThenByDescending(e => e.EndMinute)
            .ToArray();

        var n = sorted.Length;
        var column = new int[n];
        var clusterId = new int[n];
        var clusterEnd = int.MinValue;
        var currentClusterId = -1;

        // Column -> end time of the last event placed in that column, scoped to the CURRENT
        // cluster only (reset on every flush).
        var columnEnds = new Dictionary<int, int>();

        for (var i = 0; i < n; i++)
        {
            var ev = sorted[i];

            if (i == 0 || ev.StartMinute >= clusterEnd)
            {
                // Flush: start a new cluster. Open interval at the boundary — StartMinute equal
                // to (not just past) clusterEnd does NOT extend the previous cluster.
                currentClusterId++;
                columnEnds.Clear();
                clusterEnd = ev.EndMinute;
            }
            else
            {
                // Joins the current cluster via the running (transitive) envelope, even if this
                // event doesn't overlap the cluster's very first event.
                clusterEnd = Math.Max(clusterEnd, ev.EndMinute);
            }

            clusterId[i] = currentClusterId;

            var col = 0;
            while (columnEnds.TryGetValue(col, out var occupantEnd) && occupantEnd > ev.StartMinute)
                col++;

            column[i] = col;
            columnEnds[col] = ev.EndMinute;
        }

        var maxColumnPerCluster = new Dictionary<int, int>();
        for (var i = 0; i < n; i++)
        {
            var candidate = column[i] + 1;
            if (!maxColumnPerCluster.TryGetValue(clusterId[i], out var current) || candidate > current)
                maxColumnPerCluster[clusterId[i]] = candidate;
        }

        var result = new (string Id, int Column, int ColumnsInCluster)[n];
        for (var i = 0; i < n; i++)
            result[i] = (sorted[i].Id, column[i], maxColumnPerCluster[clusterId[i]]);

        return result;
    }
}
