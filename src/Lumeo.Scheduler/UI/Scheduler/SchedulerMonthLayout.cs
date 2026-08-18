namespace Lumeo.SchedulerKernel;

/// <summary>
/// Lane assignment for a single calendar month-row (7 consecutive day cells): multi-day and
/// all-day events keep a stable, visually-continuous lane across the days they span, instead of
/// the uploaded demo's own per-cell independent top-3 truncation (spec §1.4, which explicitly
/// calls out the demo's shortcut as not acceptable for the first-party component — a multi-day
/// event's pill must not land in a different vertical slot on consecutive days).
/// </summary>
internal static class SchedulerMonthLayout
{
    /// <summary>Default max lanes shown per day cell before the "+N more" affordance kicks in — matches the demo's <c>slice(0,3)</c>.</summary>
    internal const int DefaultMaxVisibleLanes = 3;

    /// <summary>
    /// Assigns each event in <paramref name="events"/> the lowest lane index that doesn't
    /// conflict, on any day cell the event occupies within the row, with an event already
    /// assigned that lane. <paramref name="events"/> should already be clamped to the 7-day row
    /// starting at <paramref name="weekStart"/> (spec §1.4's own stated input contract) — this
    /// method does not itself clip a span that extends outside the row, beyond a defensive clamp
    /// (see <see cref="DayCellSpan"/>).
    /// </summary>
    /// <remarks>
    /// Algorithm (spec §1.4):
    /// <list type="number">
    /// <item>Sort: all-day/multi-day events first (row-start-day ascending, then span length
    /// descending — "wider things first", matching FullCalendar's own ordering so a long bar
    /// doesn't get bumped by an unstably-sorted short single-day item); single-day timed events
    /// last (start time ascending).</item>
    /// <item>Greedily assign each event, in that order, the lowest lane index not already
    /// occupied — by any other event's lane assignment — on ANY day cell the event itself spans.
    /// The "lane" axis is a single index shared across the whole row (not a per-day pixel
    /// offset), which is what keeps a continuous multi-day bar's per-day pieces lined up.</item>
    /// </list>
    /// A day-cell span, not raw <c>DateTime</c> overlap, is what "conflicts" is computed over —
    /// see <see cref="DayCellSpan"/> for exactly how a (StartDate, EndDate, AllDay) triple maps
    /// onto the row's 7 day-cell indices.
    /// </remarks>
    internal static IReadOnlyDictionary<string, int> PackRow(
        DateTime weekStart,
        IReadOnlyList<(string Id, DateTime StartDate, DateTime EndDate, bool AllDay)> events,
        int rowDays = 7)
    {
        var lanes = new Dictionary<string, int>();
        if (events.Count == 0) return lanes;

        var multi = new List<(string Id, (int Start, int EndExclusive) Span, DateTime StartDate)>();
        var single = new List<(string Id, (int Start, int EndExclusive) Span, DateTime StartDate)>();

        foreach (var e in events)
        {
            var span = DayCellSpan(weekStart, e.StartDate, e.EndDate, e.AllDay, rowDays);
            var isMultiOrAllDay = e.AllDay || (span.EndExclusive - span.Start) > 1;
            (isMultiOrAllDay ? multi : single).Add((e.Id, span, e.StartDate));
        }

        multi.Sort((a, b) =>
        {
            var byStart = a.Span.Start.CompareTo(b.Span.Start);
            if (byStart != 0) return byStart;
            var aLen = a.Span.EndExclusive - a.Span.Start;
            var bLen = b.Span.EndExclusive - b.Span.Start;
            return bLen.CompareTo(aLen); // span length descending
        });
        single.Sort((a, b) => a.StartDate.CompareTo(b.StartDate)); // start time ascending

        // laneOccupancy[lane] = set of day-cell indices already claimed by some event in that lane.
        var laneOccupancy = new List<HashSet<int>>();

        void Assign(string id, (int Start, int EndExclusive) span)
        {
            var lane = 0;
            while (true)
            {
                if (lane >= laneOccupancy.Count) laneOccupancy.Add(new HashSet<int>());

                var conflict = false;
                for (var d = span.Start; d < span.EndExclusive; d++)
                {
                    if (!laneOccupancy[lane].Contains(d)) continue;
                    conflict = true;
                    break;
                }
                if (!conflict) break;
                lane++;
            }

            for (var d = span.Start; d < span.EndExclusive; d++)
                laneOccupancy[lane].Add(d);
            lanes[id] = lane;
        }

        foreach (var item in multi) Assign(item.Id, item.Span);
        foreach (var item in single) Assign(item.Id, item.Span);

        return lanes;
    }

    /// <summary>
    /// Per-day-cell hidden count: <c>max(0, occupiedLanes(day) - maxVisibleLanes)</c> (spec
    /// §1.4) — how many events on that specific day cell fall past the visible-lane budget, for
    /// the "+N more" affordance. Reads lane occupancy directly, so a day crowded only because
    /// several SHORT single-day events land on it doesn't inflate the hidden count on other,
    /// less-crowded days in the same row.
    /// </summary>
    internal static IReadOnlyDictionary<DateTime, int> HiddenCounts(
        DateTime weekStart,
        IReadOnlyDictionary<string, int> lanes,
        IReadOnlyList<(string Id, DateTime StartDate, DateTime EndDate, bool AllDay)> events,
        int maxVisibleLanes = DefaultMaxVisibleLanes,
        int rowDays = 7)
    {
        var perDayLanes = new HashSet<int>[rowDays];
        for (var i = 0; i < rowDays; i++) perDayLanes[i] = new HashSet<int>();

        foreach (var e in events)
        {
            if (!lanes.TryGetValue(e.Id, out var lane)) continue;
            var span = DayCellSpan(weekStart, e.StartDate, e.EndDate, e.AllDay, rowDays);
            for (var d = span.Start; d < span.EndExclusive; d++)
                perDayLanes[d].Add(lane);
        }

        var result = new Dictionary<DateTime, int>();
        for (var i = 0; i < rowDays; i++)
        {
            var day = weekStart.Date.AddDays(i);
            result[day] = Math.Max(0, perDayLanes[i].Count - maxVisibleLanes);
        }
        return result;
    }

    /// <summary>
    /// Maps an event's (StartDate, EndDate, AllDay) onto the row's 7 day-cell indices [0, 7)
    /// relative to <paramref name="weekStart"/>, as a half-open [Start, EndExclusive) range.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>A single-day timed event (<c>allDay == false</c>, and EndDate falls on the same
    /// calendar date as StartDate) always occupies exactly the one day-cell it starts on —
    /// <c>[startIndex, startIndex + 1)</c>.</item>
    /// <item>An all-day or genuinely multi-day event follows <see cref="SchedulerEvent.End"/>'s
    /// exclusive-end convention: a midnight <c>EndDate</c> means the span does NOT touch that
    /// calendar day (e.g. Mon 22:00 -&gt; Wed 00:00 touches Mon and Tue only, not Wed); a
    /// non-midnight <c>EndDate</c> means it partially touches that day too.</item>
    /// <item>Both bounds are clamped to <c>[0, 7]</c> — the caller's event list is expected to
    /// already be clamped to the row (spec §1.4's stated input contract), so this clamp is a
    /// defensive fallback, not the primary clipping mechanism.</item>
    /// </list>
    /// </remarks>
    private static (int Start, int EndExclusive) DayCellSpan(DateTime weekStart, DateTime startDate, DateTime endDate, bool allDay, int rowDays = 7)
    {
        var rowStart = weekStart.Date;
        var startIndex = (int)(startDate.Date - rowStart).TotalDays;

        int endIndexExclusive;
        if (!allDay && endDate.Date == startDate.Date)
        {
            endIndexExclusive = startIndex + 1;
        }
        else
        {
            var lastTouchedDate = endDate.TimeOfDay == TimeSpan.Zero ? endDate.Date.AddDays(-1) : endDate.Date;
            var lastTouchedIndex = (int)(lastTouchedDate - rowStart).TotalDays;
            endIndexExclusive = Math.Max(startIndex + 1, lastTouchedIndex + 1);
        }

        // Against the ROW's own width, not a hard-coded seven. The time grid packs windows of
        // up to fourteen days through here, and clamping a day-eight start to 7 then asked
        // Math.Clamp for the range [8, 7] — which throws, so the whole view failed to render
        // (Codex review of PR #427). The last column is rowDays - 1, so a start may reach it and
        // an exclusive end may reach rowDays.
        var lastStart = Math.Max(0, rowDays - 1);
        var clampedStart = Math.Clamp(startIndex, 0, lastStart);
        var clampedEnd = Math.Clamp(endIndexExclusive, clampedStart + 1, Math.Max(clampedStart + 1, rowDays));
        return (clampedStart, clampedEnd);
    }
}
