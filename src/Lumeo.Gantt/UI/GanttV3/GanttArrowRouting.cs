namespace Lumeo.GanttV3;

/// <summary>
/// Pure, static dependency-arrow geometry — a faithful 1:1 port of gantt-v2.js's
/// arrow-drawing block (<c>render()</c>'s trailing <c>tasks.forEach</c> loop,
/// gantt-v2.js lines 646-669). Read end-to-end before porting: v2 uses exactly
/// ONE routing formula for every dependency edge — there is no branch for
/// "target left of source", "same row", or any other relative geometry; the
/// elbow path and arrowhead are computed identically regardless of where the
/// target bar sits relative to the source bar. This port is therefore
/// intentionally unconditional too — see <see cref="ComputePathD"/>'s remarks
/// for what that means when the target is behind the source.
/// </summary>
internal static class GanttArrowRouting
{
    /// <summary>
    /// A dependency arrow's endpoint geometry as the router needs it: the bar's
    /// left edge, width, and row slot (row slot recovers the bar's vertical
    /// center via <see cref="GanttScale.BarTop"/> — the same value
    /// <see cref="GanttScale.BarGeometry"/> and a rendered <c>GanttBar</c> use).
    /// </summary>
    internal readonly record struct BarGeometry(double X, double Width, int RowIndex);

    /// <summary>
    /// SVG path <c>d</c> attribute for one dependency arrow from
    /// <paramref name="source"/> (the depended-upon task) to
    /// <paramref name="target"/> (the dependent task). Faithful port of:
    /// <code>
    /// const sx = source.x + source.w;
    /// const sy = source.y + BAR_HEIGHT / 2;
    /// const tx = target.x;
    /// const ty = target.y + BAR_HEIGHT / 2;
    /// const midX = sx + 12;
    /// const path = `M ${sx} ${sy} L ${midX} ${sy} L ${midX} ${ty} L ${tx - 4} ${ty}`;
    /// </code>
    /// (gantt-v2.js lines 654-659). Because v2 has no "target behind source"
    /// branch, a backward dependency (target's left edge is to the LEFT of
    /// <c>midX</c>) still routes through the same forward-projecting elbow —
    /// the path visually loops out to the right of the source bar before
    /// doubling back left to the target, exactly as v2 renders it. Likewise a
    /// same-row dependency (<c>source.RowIndex == target.RowIndex</c>) produces
    /// a path whose two elbow points share the same Y — a redundant-looking but
    /// harmless <c>L</c> segment of zero vertical extent, again matching v2
    /// verbatim rather than special-casing it away.
    /// </summary>
    internal static string ComputePathD(BarGeometry source, BarGeometry target, int barHeight)
    {
        var (sx, sy) = SourceEdge(source, barHeight);
        var (tx, ty) = TargetEdge(target, barHeight);
        var midX = sx + 12;

        return FormattableString.Invariant($"M {sx} {sy} L {midX} {sy} L {midX} {ty} L {tx - 4} {ty}");
    }

    /// <summary>
    /// SVG <c>points</c> attribute for one dependency arrow's arrowhead
    /// triangle, anchored at <paramref name="target"/>'s left edge. Faithful
    /// port of:
    /// <code>
    /// points: `${tx - 6},${ty - 4} ${tx},${ty} ${tx - 6},${ty + 4}`
    /// </code>
    /// (gantt-v2.js line 665).
    /// </summary>
    internal static string ComputeArrowheadPoints(BarGeometry target, int barHeight)
    {
        var (tx, ty) = TargetEdge(target, barHeight);

        return FormattableString.Invariant($"{tx - 6},{ty - 4} {tx},{ty} {tx - 6},{ty + 4}");
    }

    // sx = source.x + source.w; sy = source.y + BAR_HEIGHT / 2 (gantt-v2.js:654-655).
    // "source.y" in v2 is the bar's already-computed top (taskById's stored `y`,
    // itself `barY` — see GanttScale.BarTop's remarks), not the row's raw top.
    //
    // No "+ GanttScale.HeaderHeight" term here (Codex round 2, P1 #3 — removed;
    // this used to add it because GanttArrowLayer's <svg> was "absolute inset-0"
    // against the SAME outer canvas div the header rendered inside of, via
    // normal document flow, so its own coordinate origin sat at the top of the
    // HEADER rather than the top of the rows — see GanttTimeline.razor's own
    // remarks for the sticky-header restructure that moved the header OUTSIDE
    // this coordinate space entirely: the outer canvas div's origin now
    // directly aligns with row 0, matching a rendered GanttBar's own
    // RowIndex*RowHeight math with no offset needed on either side anymore).
    private static (double X, double Y) SourceEdge(BarGeometry source, int barHeight)
    {
        var sy = GanttScale.BarTop(source.RowIndex, barHeight) + barHeight / 2.0;
        return (source.X + source.Width, sy);
    }

    // tx = target.x; ty = target.y + BAR_HEIGHT / 2 (gantt-v2.js:656-657).
    private static (double X, double Y) TargetEdge(BarGeometry target, int barHeight)
    {
        var ty = GanttScale.BarTop(target.RowIndex, barHeight) + barHeight / 2.0;
        return (target.X, ty);
    }
}
