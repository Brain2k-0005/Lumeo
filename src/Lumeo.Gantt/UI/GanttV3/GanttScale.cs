using System.Globalization;

namespace Lumeo.GanttV3;

/// <summary>
/// The unit of a <see cref="GanttViewMode"/>'s date grid columns. Mirrors the
/// <c>unit</c> field of gantt-v2.js's <c>VIEW_MODES</c> table
/// (wwwroot/js/gantt-v2.js, lines 29-36).
/// </summary>
internal enum GanttScaleUnit
{
    Hour,
    Day,
    Month,
    Year,
}

/// <summary>
/// The upper (grouping) header row's label kind. Mirrors the
/// <c>headerFmt.upper</c> values in gantt-v2.js's <c>VIEW_MODES</c> table and the
/// <c>switch (cfg.headerFmt.upper)</c> in <c>render()</c> (gantt-v2.js lines 386-392).
/// <see cref="None"/> mirrors the empty-string upper format used by
/// <see cref="GanttViewMode.Year"/>, which renders no upper row at all — v2's
/// <c>if (upperText &amp;&amp; ...)</c> guard treats an empty string as falsy and skips it.
/// </summary>
internal enum GanttHeaderUpperKind
{
    /// <summary>No upper row (v2 <c>headerFmt.upper: ''</c>, <see cref="GanttViewMode.Year"/>).</summary>
    None,
    /// <summary>Full month name, e.g. "January" (v2 <c>'month'</c> -&gt; <c>fmtMonth</c>).</summary>
    Month,
    /// <summary>4-digit year, e.g. "2026" (v2 <c>'year'</c> -&gt; <c>fmtYear</c>).</summary>
    Year,
    /// <summary>"{month} {day}", e.g. "January 5" (v2 <c>'day'</c> -&gt; <c>`${fmtMonth(d)} ${d.getDate()}`</c>).</summary>
    Day,
}

/// <summary>
/// The lower (per-column) header row's label kind. Mirrors the
/// <c>headerFmt.lower</c> values in gantt-v2.js's <c>VIEW_MODES</c> table and the
/// <c>switch (cfg.headerFmt.lower)</c> in <c>render()</c> (gantt-v2.js lines 366-375).
/// </summary>
internal enum GanttHeaderLowerKind
{
    /// <summary>2-digit day-of-month, e.g. "05" (v2 <c>fmtDayNum</c>).</summary>
    DayNum,
    /// <summary>"{day}/{month}" (no leading zeros), e.g. "5/1" (v2 <c>`${d.getDate()}/${d.getMonth() + 1}`</c>).</summary>
    WeekRange,
    /// <summary>Short month name, e.g. "Jan" (v2 <c>fmtMonthShort</c>).</summary>
    MonthName,
    /// <summary>4-digit year, e.g. "2026" (v2 <c>fmtYear</c>).</summary>
    YearNum,
    /// <summary>"{hour}:00", 6-hour columns (v2 <c>time6h</c> — same rendering as <see cref="Time12h"/>).</summary>
    Time6h,
    /// <summary>"{hour}:00", 12-hour columns (v2 <c>time12h</c> — same rendering as <see cref="Time6h"/>).</summary>
    Time12h,
}

/// <summary>
/// Per-<see cref="GanttViewMode"/> scale configuration: column width, snap unit/step,
/// window padding, and the two header rows' label kinds. Value type mirroring one row
/// of gantt-v2.js's <c>VIEW_MODES</c> table (wwwroot/js/gantt-v2.js, lines 29-36) —
/// see <see cref="GanttScale.ViewModes"/> for the exact port of that table.
/// </summary>
internal readonly record struct GanttScaleConfig(
    int ColumnWidth,
    GanttScaleUnit Unit,
    int Step,
    int PadBefore,
    int PadAfter,
    GanttHeaderUpperKind HeaderUpper,
    GanttHeaderLowerKind HeaderLower);

/// <summary>
/// One run of identical, consecutive upper-row labels — e.g. every "Day" column
/// that falls in January collapses into a single "January" run spanning those
/// columns, instead of repeating the label per column. Mirrors v2's
/// <c>lastUpperLabel</c> de-duplication in <c>render()</c>'s header-label loop
/// (gantt-v2.js lines 351-404: <c>if (upperText &amp;&amp; upperText !== lastUpperLabel)</c>).
/// </summary>
/// <param name="StartIndex">Index into the date-units array (see <see cref="GanttScale.BuildDateUnits"/>) where the run starts.</param>
/// <param name="Span">Number of consecutive columns the run covers.</param>
/// <param name="Label">The upper-row text shared by every column in the run.</param>
internal readonly record struct GanttHeaderRun(int StartIndex, int Span, string Label);

/// <summary>
/// Pure, static date/pixel math for the Gantt v3 timeline — the behavioral
/// source is gantt-v2.js's <c>VIEW_MODES</c> table plus the <c>render()</c>
/// date-unit generation (lines 166-190), <c>dateToX</c>/<c>xToDate</c> closures
/// (lines 209-256), and the standalone <c>pixelsPerDay()</c> helper (lines
/// 727-734) used to snap drag deltas. Every method here is a faithful C# port
/// of that JS math — no Blazor/JS-interop dependency, so it is unit-testable
/// (and reusable by the v3 render tree, drag math, and parity harness) without
/// a browser.
///
/// TZ/DST safety: every method below operates purely on the <see cref="DateTime"/>
/// values it is given via calendar arithmetic (<c>AddHours</c>/<c>AddDays</c>/
/// <c>AddMonths</c>/<c>AddYears</c>) and calendar-field reads (<c>Year</c>/<c>Month</c>/
/// <c>Day</c>/<c>Hour</c>). None of it ever calls <c>ToLocalTime</c>, <c>ToUniversalTime</c>,
/// or touches <see cref="TimeZoneInfo"/> — so <see cref="DateTime.Kind"/> is irrelevant
/// to the results and no local-timezone DST transition (spring-forward/fall-back) can
/// perturb the math, unlike JS's <c>Date</c> arithmetic (v2's <c>addDays</c>/<c>addMonths</c>
/// construct calendar dates via the local-timezone-aware <c>Date</c> constructor and
/// setters). Callers/tests should still prefer <see cref="DateTimeKind.Utc"/>
/// consistently — not because the math needs it, but so a stray <c>ToLocalTime()</c>
/// introduced later would visibly change values instead of silently matching by
/// accident on a UTC-configured CI box.
/// </summary>
internal static class GanttScale
{
    /// <summary>Row height in pixels for a single task row. Mirrors v2's <c>ROW_HEIGHT</c> (gantt-v2.js line 38).</summary>
    internal const int RowHeight = 36;

    /// <summary>Default task-bar height in pixels. Mirrors v2's <c>DEFAULT_BAR_HEIGHT</c> (gantt-v2.js line 39).</summary>
    internal const int DefaultBarHeight = 22;

    /// <summary>Header block height in pixels (both rows combined). Mirrors v2's <c>HEADER_HEIGHT</c> (gantt-v2.js line 40).</summary>
    internal const int HeaderHeight = 56;

    /// <summary>
    /// Faithful port of gantt-v2.js's <c>VIEW_MODES</c> table (lines 29-36). Every
    /// field below is copied verbatim from the corresponding JS literal; see each
    /// <see cref="GanttScaleUnit"/>/<see cref="GanttHeaderUpperKind"/>/<see cref="GanttHeaderLowerKind"/>
    /// member for the v2 string it replaces.
    /// </summary>
    internal static readonly IReadOnlyDictionary<GanttViewMode, GanttScaleConfig> ViewModes =
        new Dictionary<GanttViewMode, GanttScaleConfig>
        {
            // QuarterDay: { columnWidth: 38, unit: 'hour', step: 6,  padBefore: 24, padAfter: 24, headerFmt: { upper: 'day',   lower: 'time6h' } },
            [GanttViewMode.QuarterDay] = new GanttScaleConfig(38, GanttScaleUnit.Hour, 6, 24, 24, GanttHeaderUpperKind.Day, GanttHeaderLowerKind.Time6h),
            // HalfDay:    { columnWidth: 38, unit: 'hour', step: 12, padBefore: 24, padAfter: 24, headerFmt: { upper: 'day',   lower: 'time12h' } },
            [GanttViewMode.HalfDay] = new GanttScaleConfig(38, GanttScaleUnit.Hour, 12, 24, 24, GanttHeaderUpperKind.Day, GanttHeaderLowerKind.Time12h),
            // Day:        { columnWidth: 38, unit: 'day',  step: 1,  padBefore: 60, padAfter: 60, headerFmt: { upper: 'month', lower: 'dayNum' } },
            [GanttViewMode.Day] = new GanttScaleConfig(38, GanttScaleUnit.Day, 1, 60, 60, GanttHeaderUpperKind.Month, GanttHeaderLowerKind.DayNum),
            // Week:       { columnWidth: 140, unit: 'day', step: 7,  padBefore: 16, padAfter: 16, headerFmt: { upper: 'month', lower: 'weekRange' } },
            [GanttViewMode.Week] = new GanttScaleConfig(140, GanttScaleUnit.Day, 7, 16, 16, GanttHeaderUpperKind.Month, GanttHeaderLowerKind.WeekRange),
            // Month:      { columnWidth: 120, unit: 'month', step: 1, padBefore: 12, padAfter: 12, headerFmt: { upper: 'year', lower: 'monthName' } },
            [GanttViewMode.Month] = new GanttScaleConfig(120, GanttScaleUnit.Month, 1, 12, 12, GanttHeaderUpperKind.Year, GanttHeaderLowerKind.MonthName),
            // Year:       { columnWidth: 120, unit: 'year', step: 1, padBefore: 4,  padAfter: 6,  headerFmt: { upper: '',     lower: 'yearNum' } },
            [GanttViewMode.Year] = new GanttScaleConfig(120, GanttScaleUnit.Year, 1, 4, 6, GanttHeaderUpperKind.None, GanttHeaderLowerKind.YearNum),
        };

    /// <summary>Looks up the <see cref="GanttScaleConfig"/> for a view mode.</summary>
    internal static GanttScaleConfig GetConfig(GanttViewMode mode) => ViewModes[mode];

    /// <summary>
    /// Builds the list of column-start dates covering <paramref name="rangeStart"/>..<paramref name="rangeEnd"/>
    /// (inclusive of both ends where the unit's step lands on them), one entry per
    /// rendered timeline column. Faithful port of the per-unit date-unit generation
    /// in v2's <c>render()</c> (gantt-v2.js lines 166-190) — <paramref name="rangeStart"/>/
    /// <paramref name="rangeEnd"/> here correspond to v2's already-padded
    /// <c>startDate</c>/<c>endDate</c> locals (padding itself is the windowing caller's
    /// concern — GanttState/GanttTimeline in later tasks — not this pure helper's).
    /// </summary>
    internal static IReadOnlyList<DateTime> BuildDateUnits(GanttViewMode mode, DateTime rangeStart, DateTime rangeEnd)
    {
        var cfg = GetConfig(mode);
        var units = new List<DateTime>();

        switch (cfg.Unit)
        {
            case GanttScaleUnit.Day:
            {
                // totalDays = dayDiff(startDate, endDate) + 1; totalColumns = ceil(totalDays / step)
                var totalDays = (int)Math.Round((rangeEnd - rangeStart).TotalDays) + 1;
                var totalColumns = (int)Math.Ceiling(totalDays / (double)cfg.Step);
                for (var i = 0; i < totalColumns; i++)
                    units.Add(rangeStart.AddDays(i * cfg.Step));
                break;
            }
            case GanttScaleUnit.Month:
            {
                // for (let d = startDate; d <= endDate; d = addMonths(d, 1))
                for (var d = rangeStart; d <= rangeEnd; d = d.AddMonths(cfg.Step))
                    units.Add(d);
                break;
            }
            case GanttScaleUnit.Year:
            {
                // for (let y = startYear; y <= endYear; y++) dateUnits.push(new Date(y, 0, 1));
                for (var y = rangeStart.Year; y <= rangeEnd.Year; y += cfg.Step)
                    units.Add(new DateTime(y, 1, 1, 0, 0, 0, rangeStart.Kind));
                break;
            }
            case GanttScaleUnit.Hour:
            {
                // const totalHours = (endDate - startDate) / 3_600_000; for (i = 0; i < totalHours; i += step)
                var totalHours = (rangeEnd - rangeStart).TotalHours;
                for (var i = 0.0; i < totalHours; i += cfg.Step)
                    units.Add(rangeStart.AddHours(i));
                break;
            }
        }

        return units;
    }

    /// <summary>
    /// Maps a date to its pixel X offset relative to <paramref name="origin"/> (the
    /// first date unit — v2's <c>dateUnits[0]</c>). Faithful port of v2's <c>dateToX</c>
    /// closure (gantt-v2.js lines 209-230).
    /// </summary>
    internal static double DateToPixel(GanttViewMode mode, DateTime origin, DateTime date)
    {
        var cfg = GetConfig(mode);
        return cfg.Unit switch
        {
            // if (cfg.unit === 'day') { const days = dayDiff(dateUnits[0], d); return (days / cfg.step) * colW; }
            GanttScaleUnit.Day => ((date.Date - origin.Date).TotalDays / cfg.Step) * cfg.ColumnWidth,
            // if (cfg.unit === 'month') { months = (d.Year-o.Year)*12 + (d.Month-o.Month); dayFraction = (d.getDate()-1)/30; return (months+dayFraction)*colW; }
            GanttScaleUnit.Month => (((date.Year - origin.Year) * 12 + (date.Month - origin.Month)) + (date.Day - 1) / 30.0) * cfg.ColumnWidth,
            // if (cfg.unit === 'year') { years = d.Year-o.Year; dayFraction = (d.getMonth()*30 + d.getDate())/365; return (years+dayFraction)*colW; }
            GanttScaleUnit.Year => ((date.Year - origin.Year) + ((date.Month - 1) * 30 + date.Day) / 365.0) * cfg.ColumnWidth,
            // if (cfg.unit === 'hour') { hours = (d - dateUnits[0]) / 3_600_000; return (hours / cfg.step) * colW; }
            GanttScaleUnit.Hour => ((date - origin).TotalHours / cfg.Step) * cfg.ColumnWidth,
            _ => 0,
        };
    }

    /// <summary>
    /// Maps a pixel X offset (relative to <paramref name="origin"/>) back to a date.
    /// Faithful port of v2's <c>xToDate</c> closure (gantt-v2.js lines 232-252).
    /// Rounds to the nearest whole unit exactly as v2's <c>Math.round</c> does (v2 has
    /// no fractional-unit dates); ties round away from zero, which only matters at an
    /// exact half-unit pixel offset — not a case v2 itself needs to disambiguate either.
    /// </summary>
    internal static DateTime PixelToDate(GanttViewMode mode, DateTime origin, double pixel)
    {
        var cfg = GetConfig(mode);
        return cfg.Unit switch
        {
            // if (cfg.unit === 'day') { days = Math.round((x/colW)*step); return addDays(dateUnits[0], days); }
            GanttScaleUnit.Day => origin.AddDays(RoundToInt((pixel / cfg.ColumnWidth) * cfg.Step)),
            // if (cfg.unit === 'month') { months = Math.round(x/colW); return addMonths(dateUnits[0], months); }
            GanttScaleUnit.Month => origin.AddMonths(RoundToInt(pixel / cfg.ColumnWidth)),
            // if (cfg.unit === 'year') { years = Math.round(x/colW); return new Date(dateUnits[0].getFullYear()+years, 0, 1); }
            GanttScaleUnit.Year => new DateTime(origin.Year + RoundToInt(pixel / cfg.ColumnWidth), 1, 1, 0, 0, 0, origin.Kind),
            // if (cfg.unit === 'hour') { hours = Math.round((x/colW)*step); d.setHours(d.getHours()+hours); return d; }
            GanttScaleUnit.Hour => origin.AddHours(RoundToInt((pixel / cfg.ColumnWidth) * cfg.Step)),
            _ => origin,
        };
    }

    private static int RoundToInt(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Pixels-per-calendar-day for the given view mode — used to snap a raw drag
    /// pixel delta to whole-day increments regardless of column granularity (e.g.
    /// Week's 140px columns represent 7 days, so 1 day = 20px). Faithful port of
    /// v2's standalone <c>pixelsPerDay()</c> helper (gantt-v2.js lines 727-734),
    /// used by <c>commitDrag</c> to convert a mouse-move delta into whole days.
    /// </summary>
    internal static double PixelsPerDay(GanttViewMode mode)
    {
        var cfg = GetConfig(mode);
        return cfg.Unit switch
        {
            GanttScaleUnit.Day => cfg.ColumnWidth / (double)cfg.Step,       // Day:1, Week:20
            GanttScaleUnit.Hour => (cfg.ColumnWidth * 24.0) / cfg.Step,
            GanttScaleUnit.Month => cfg.ColumnWidth / 30.0,
            GanttScaleUnit.Year => cfg.ColumnWidth / 365.0,
            _ => cfg.ColumnWidth,
        };
    }

    /// <summary>
    /// The lower (per-column) header row's label for every date unit, in order.
    /// Faithful port of the <c>switch (cfg.headerFmt.lower)</c> block in v2's
    /// <c>render()</c> (gantt-v2.js lines 366-375).
    /// </summary>
    internal static IReadOnlyList<string> LowerLabels(GanttViewMode mode, IReadOnlyList<DateTime> units)
    {
        var cfg = GetConfig(mode);
        var labels = new string[units.Count];
        for (var i = 0; i < units.Count; i++)
            labels[i] = LowerLabel(cfg.HeaderLower, units[i]);
        return labels;
    }

    private static string LowerLabel(GanttHeaderLowerKind kind, DateTime d) => kind switch
    {
        // case 'dayNum': lowerText = fmtDayNum(d); -> String(d.getDate()).padStart(2, '0')
        GanttHeaderLowerKind.DayNum => d.Day.ToString("D2", CultureInfo.InvariantCulture),
        // case 'weekRange': lowerText = `${d.getDate()}/${d.getMonth() + 1}`;
        GanttHeaderLowerKind.WeekRange => $"{d.Day}/{d.Month}",
        // case 'monthName': lowerText = fmtMonthShort(d);
        GanttHeaderLowerKind.MonthName => d.ToString("MMM", CultureInfo.InvariantCulture),
        // case 'yearNum': lowerText = fmtYear(d);
        GanttHeaderLowerKind.YearNum => d.Year.ToString(CultureInfo.InvariantCulture),
        // case 'time6h': case 'time12h': lowerText = `${d.getHours()}:00`;
        GanttHeaderLowerKind.Time6h or GanttHeaderLowerKind.Time12h => $"{d.Hour}:00",
        _ => string.Empty,
    };

    /// <summary>
    /// The upper (grouping) header row's label runs — consecutive date units that
    /// share the same upper-row text collapse into one <see cref="GanttHeaderRun"/>.
    /// Empty upper text (<see cref="GanttHeaderUpperKind.None"/>, i.e.
    /// <see cref="GanttViewMode.Year"/>) never starts a run, mirroring v2's
    /// <c>if (upperText &amp;&amp; upperText !== lastUpperLabel)</c> guard (gantt-v2.js
    /// lines 386-404) — an empty string is falsy in JS and is never emitted.
    /// </summary>
    internal static IReadOnlyList<GanttHeaderRun> UpperRuns(GanttViewMode mode, IReadOnlyList<DateTime> units)
    {
        var cfg = GetConfig(mode);
        var runs = new List<GanttHeaderRun>();
        if (cfg.HeaderUpper == GanttHeaderUpperKind.None) return runs;

        var runStart = -1;
        string? runLabel = null;
        for (var i = 0; i < units.Count; i++)
        {
            var label = UpperLabel(cfg.HeaderUpper, units[i]);
            if (label != runLabel)
            {
                if (runStart >= 0) runs.Add(new GanttHeaderRun(runStart, i - runStart, runLabel!));
                runStart = i;
                runLabel = label;
            }
        }
        if (runStart >= 0) runs.Add(new GanttHeaderRun(runStart, units.Count - runStart, runLabel!));
        return runs;
    }

    private static string UpperLabel(GanttHeaderUpperKind kind, DateTime d) => kind switch
    {
        // case 'month': upperText = fmtMonth(d); -> d.toLocaleString(undefined, { month: 'long' })
        GanttHeaderUpperKind.Month => d.ToString("MMMM", CultureInfo.InvariantCulture),
        // case 'year': upperText = fmtYear(d);
        GanttHeaderUpperKind.Year => d.Year.ToString(CultureInfo.InvariantCulture),
        // case 'day': upperText = `${fmtMonth(d)} ${d.getDate()}`;
        GanttHeaderUpperKind.Day => $"{d.ToString("MMMM", CultureInfo.InvariantCulture)} {d.Day}",
        _ => string.Empty,
    };
}
