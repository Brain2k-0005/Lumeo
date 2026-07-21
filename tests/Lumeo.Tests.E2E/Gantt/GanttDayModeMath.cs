using System.Globalization;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// Independent re-derivation of the Day-view-mode pixel math shared by v2
/// (<c>gantt-v2.js</c>'s <c>dateToX</c>/bar-geometry inline computation) and v3
/// (<c>Lumeo.GanttV3.GanttScale</c>, which is <c>internal</c> and not
/// referenceable from this project). Deliberately NOT a copy-paste of either —
/// written directly from the two source files' documented formulas (gantt-v2.js
/// lines 209-230, 455, 508-512, 654-659) so the parity harness has a THIRD,
/// ground-truth value to compare v2's and v3's rendered output against, not
/// just against each other (a shared bug in both renderers would otherwise be
/// invisible to a v2-vs-v3-only comparison).
/// </summary>
internal static class GanttDayModeMath
{
    internal const int ColumnWidth = 38;
    internal const int PadDays = 60;
    internal const int RowHeightPx = 36;
    internal const int DefaultBarHeightPx = 22;
    internal const int HeaderHeightPx = 56;

    /// <summary>gantt-v2.js: <c>startDate = addDays(minDate, -cfg.padBefore * cfg.step)</c> (padBefore=60, step=1 for Day).</summary>
    internal static DateTime Origin(DateTime minTaskStart) => minTaskStart.Date.AddDays(-PadDays);

    /// <summary>gantt-v2.js's <c>dateToX</c> for <c>unit === 'day'</c>: <c>(dayDiff(origin, d) / step) * colW</c>.</summary>
    internal static double DateToX(DateTime origin, DateTime date) => (date.Date - origin.Date).TotalDays * ColumnWidth;

    /// <summary>
    /// gantt-v2.js's regular-bar branch (lines 508-512): <c>x1 = dateToX(start); x2 = dateToX(end+1day); w = max(8, x2-x1)</c>.
    /// Milestone branch (lines 461-463): bounding-box left = <c>dateToX(start) + colW/2 - barHeight/2</c>, width = barHeight.
    /// </summary>
    internal static (double X, double Width) BarGeometry(DateTime origin, DateTime start, DateTime end, bool isMilestone, int barHeight = DefaultBarHeightPx)
    {
        if (isMilestone)
        {
            var center = DateToX(origin, start) + ColumnWidth / 2.0;
            var half = barHeight / 2.0;
            return (center - half, barHeight);
        }

        var x1 = DateToX(origin, start);
        var x2 = DateToX(origin, end.AddDays(1));
        return (x1, Math.Max(8, x2 - x1));
    }

    /// <summary>gantt-v2.js line 455: <c>barY = rowY + (ROW_HEIGHT - BAR_HEIGHT) / 2</c>, relative to the row canvas (HEADER_HEIGHT excluded — the caller's own concern).</summary>
    internal static double BarTop(int rowIndex, int barHeight = DefaultBarHeightPx) =>
        (rowIndex * RowHeightPx) + (RowHeightPx - barHeight) / 2.0;

    /// <summary>
    /// gantt-v2.js's arrow block (lines 654-659): <c>sx=source.x+source.w; sy=source.y+H/2; tx=target.x; ty=target.y+H/2; midX=sx+12</c>,
    /// path <c>M sx sy L midX sy L midX ty L (tx-4) ty</c>. Returns the 4 path points in order.
    /// "source.y"/"target.y" in v2 is the bar's already-computed <c>barY</c>
    /// (gantt-v2.js line 455), which — unlike this file's own <see cref="BarTop"/>
    /// — DOES include HEADER_HEIGHT (v2 is one flat SVG canvas; there is no
    /// split header/row coordinate space to omit it for), so it's added here
    /// explicitly rather than folded into <see cref="BarTop"/> itself (which
    /// intentionally mirrors v3's GanttScale.BarTop, excluding it — see that
    /// method's own doc comment).
    ///
    /// <paramref name="includeHeaderHeight"/> (Codex round 2, P1 #3): v2's own
    /// geometry ALWAYS includes it (pass <c>true</c>, the default — preserves
    /// every existing v2 caller unchanged); v3's <c>GanttArrowRouting.SourceEdge</c>/
    /// <c>TargetEdge</c> DROPPED the header offset as part of the sticky-header
    /// restructure (the header no longer lives inside the same outer canvas div
    /// GanttArrowLayer's SVG is positioned against — see that type's own
    /// remarks), so a v3 ground-truth comparison must pass <c>false</c> here or
    /// it would compare v3's (correctly, now header-less) rendered arrows
    /// against a still-includes-the-header expectation.
    /// </summary>
    internal static (double X, double Y)[] ArrowPath((double X, double Width, int RowIndex) source, (double X, double Width, int RowIndex) target, int barHeight = DefaultBarHeightPx, bool includeHeaderHeight = true)
    {
        var headerOffset = includeHeaderHeight ? HeaderHeightPx : 0;
        var sy = headerOffset + BarTop(source.RowIndex, barHeight) + barHeight / 2.0;
        var sx = source.X + source.Width;
        var ty = headerOffset + BarTop(target.RowIndex, barHeight) + barHeight / 2.0;
        var tx = target.X;
        var midX = sx + 12;
        return new[] { (sx, sy), (midX, sy), (midX, ty), (tx - 4, ty) };
    }

    /// <summary>
    /// Parses an SVG/CSS path-ish string of the exact shape
    /// <c>M x0 y0 L x1 y1 L x2 y2 L x3 y3</c> (both v2's <c>path[d]</c> attribute and
    /// v3's <c>GanttArrowRouting.ComputePathD</c> output use precisely this format —
    /// see each's own remarks) into its 4 coordinate pairs.
    /// </summary>
    internal static (double X, double Y)[] ParsePathD(string d)
    {
        var tokens = d.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t != "M" && t != "L")
            .Select(t => double.Parse(t, CultureInfo.InvariantCulture))
            .ToArray();
        var points = new (double X, double Y)[tokens.Length / 2];
        for (var i = 0; i < points.Length; i++)
            points[i] = (tokens[i * 2], tokens[i * 2 + 1]);
        return points;
    }
}
