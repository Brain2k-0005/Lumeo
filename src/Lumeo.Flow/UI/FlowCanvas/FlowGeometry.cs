using System.Globalization;
using System.Text;

namespace Lumeo;

/// <summary>
/// Pure flow-canvas math: coordinate transforms, snapping, fit-view and the four edge path
/// generators. No DOM, no state — every member is a function of its arguments.
/// </summary>
/// <remarks>
/// <b>Lockstep with <c>flow.js</c>.</b> The engine rewrites edge paths live while a node is
/// dragged, with its own JavaScript port of <see cref="GetEdgePath"/>; the next Blazor render then
/// writes the path this class produces. The two must agree to the character or an edge visibly
/// twitches on drop. <c>FlowGeometryLockstepTests</c> compares this class against a table the JS
/// functions produce (<c>tests/js/flow-geometry-table.mjs</c>) — change both sides together and
/// regenerate the table.
/// </remarks>
public static class FlowGeometry
{
    /// <summary>Size used for a node that has neither a fixed size nor a measurement yet.</summary>
    public const double DefaultNodeWidth = 150;

    /// <summary>Size used for a node that has neither a fixed size nor a measurement yet.</summary>
    public const double DefaultNodeHeight = 40;

    /// <summary>The factor <c>ZoomInAsync</c>/<c>ZoomOutAsync</c> multiply/divide by.</summary>
    public const double ZoomStep = 1.2;

    /// <summary>How far a step/smooth-step route runs straight out of a handle before turning.</summary>
    public const double StepOffset = 20;

    /// <summary>Corner radius of a smooth-step route.</summary>
    public const double SmoothStepRadius = 5;

    /// <summary>Control-point curvature for a Bézier whose target lies behind its source.</summary>
    public const double BezierCurvature = 0.25;

    // ── Coordinate spaces ────────────────────────────────────────────────

    /// <summary>
    /// Pane-local pixels (relative to the pane's top-left corner) → flow coordinates:
    /// <c>(local - viewport.XY) / viewport.Zoom</c>.
    /// </summary>
    public static FlowPoint ScreenToFlow(double x, double y, FlowViewport viewport)
    {
        var zoom = viewport.Zoom > 0 ? viewport.Zoom : 1;
        return new FlowPoint((x - viewport.X) / zoom, (y - viewport.Y) / zoom);
    }

    /// <summary>Flow coordinates → pane-local pixels: <c>flow * viewport.Zoom + viewport.XY</c>.</summary>
    public static FlowPoint FlowToScreen(double x, double y, FlowViewport viewport)
    {
        var zoom = viewport.Zoom > 0 ? viewport.Zoom : 1;
        return new FlowPoint(x * zoom + viewport.X, y * zoom + viewport.Y);
    }

    /// <summary>Clamps <paramref name="zoom"/> into <c>[min, max]</c> (a max below min collapses to min).</summary>
    public static double ClampZoom(double zoom, double min, double max)
    {
        if (max < min) max = min;
        if (double.IsNaN(zoom)) return min;
        return Math.Min(max, Math.Max(min, zoom));
    }

    /// <summary>
    /// Re-scales <paramref name="viewport"/> to <paramref name="zoom"/> while keeping the flow point
    /// under the pane-local anchor exactly where it is — the pointer-anchored wheel zoom.
    /// </summary>
    public static FlowViewport ZoomAt(FlowViewport viewport, double zoom, double anchorX, double anchorY)
    {
        var p = ScreenToFlow(anchorX, anchorY, viewport);
        return new FlowViewport(anchorX - p.X * zoom, anchorY - p.Y * zoom, zoom);
    }

    /// <summary>
    /// A viewport that shows flow point (<paramref name="x"/>, <paramref name="y"/>) at the centre of
    /// a pane of the given size.
    /// </summary>
    public static FlowViewport CenterOn(double x, double y, double zoom, double paneWidth, double paneHeight)
        => new(paneWidth / 2 - x * zoom, paneHeight / 2 - y * zoom, zoom);

    // ── Snapping ─────────────────────────────────────────────────────────

    /// <summary>
    /// Rounds <paramref name="value"/> to the nearest multiple of <paramref name="grid"/> (ties round
    /// up, like JavaScript's <c>Math.round</c> — the engine snaps with the same rule). A grid of 0 or
    /// less returns the value unchanged.
    /// </summary>
    public static double Snap(double value, double grid)
        => grid > 0 ? Math.Floor(value / grid + 0.5) * grid : value;

    /// <summary>Snaps both axes of a point.</summary>
    public static FlowPoint Snap(FlowPoint point, double gridX, double gridY)
        => new(Snap(point.X, gridX), Snap(point.Y, gridY));

    // ── Bounds and fit-view ──────────────────────────────────────────────

    /// <summary>The union of <paramref name="rects"/>, or <c>null</c> when there are none.</summary>
    public static FlowRect? GetBounds(IEnumerable<FlowRect> rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        var any = false;
        foreach (var r in rects)
        {
            any = true;
            minX = Math.Min(minX, r.X);
            minY = Math.Min(minY, r.Y);
            maxX = Math.Max(maxX, r.X + r.Width);
            maxY = Math.Max(maxY, r.Y + r.Height);
        }
        return any ? new FlowRect(minX, minY, maxX - minX, maxY - minY) : null;
    }

    /// <summary>
    /// The viewport that fits every rect into a pane of the given size, centred.
    /// <paramref name="padding"/> is a fraction of the bounds (0.1 = 10% breathing room in total);
    /// the zoom is clamped to <c>[minZoom, maxZoom]</c>. Returns <c>null</c> when there is nothing to
    /// fit or the pane has no size yet.
    /// </summary>
    public static FlowViewport? FitView(IEnumerable<FlowRect> rects, double paneWidth, double paneHeight,
        double padding, double minZoom, double maxZoom)
        => FitView(rects, paneWidth, paneHeight, padding, minZoom, maxZoom, null);

    /// <summary>
    /// LU-08: like the plain <see cref="FitView(IEnumerable{FlowRect}, double, double, double, double, double)"/>,
    /// but when the zoom needed to show every rect would fall below <paramref name="minZoom"/>,
    /// centres on <paramref name="anchor"/> at <paramref name="minZoom"/> instead of centring on the
    /// whole (still clamped, so still partly off-screen) bounds — "start readable" rather than "start
    /// complete". Falls back to the plain fit when <paramref name="anchor"/> is <c>null</c> or the fit
    /// does not need clamping. Returns <c>null</c> under the same conditions as the plain overload.
    /// </summary>
    public static FlowViewport? FitView(IEnumerable<FlowRect> rects, double paneWidth, double paneHeight,
        double padding, double minZoom, double maxZoom, FlowRect? anchor)
        => FitView(rects, paneWidth, paneHeight, padding, minZoom, maxZoom, anchor, FlowAnchorAlign.Center);

    /// <summary>
    /// LU-19: like the anchor overload above, with control over WHERE <paramref name="anchor"/> lands
    /// once it triggers the clamp. <see cref="FlowAnchorAlign.Center"/> keeps the exact prior
    /// behaviour (delegates to <see cref="CenterOn"/>); <see cref="FlowAnchorAlign.Start"/>/
    /// <see cref="FlowAnchorAlign.End"/> instead call <see cref="AnchorAlignedViewport"/> — see there
    /// for the placement rule and the <paramref name="rtl"/> flip.
    /// </summary>
    public static FlowViewport? FitView(IEnumerable<FlowRect> rects, double paneWidth, double paneHeight,
        double padding, double minZoom, double maxZoom, FlowRect? anchor, FlowAnchorAlign anchorAlign, bool rtl = false)
    {
        var bounds = GetBounds(rects);
        if (bounds is not { } b || !(paneWidth > 0) || !(paneHeight > 0)) return null;
        var pad = padding > 0 ? padding : 0;
        var bw = Math.Max(b.Width, 1);
        var bh = Math.Max(b.Height, 1);
        var required = Math.Min(paneWidth / (bw * (1 + pad)), paneHeight / (bh * (1 + pad)));
        if (anchor is { } a && required < minZoom)
        {
            var anchorZoom = ClampZoom(minZoom, minZoom, maxZoom);
            return AnchorAlignedViewport(a, anchorZoom, paneWidth, paneHeight, pad, anchorAlign, rtl);
        }
        var zoom = ClampZoom(required, minZoom, maxZoom);
        var cx = b.X + b.Width / 2;
        var cy = b.Y + b.Height / 2;
        return CenterOn(cx, cy, zoom, paneWidth, paneHeight);
    }

    /// <summary>
    /// LU-19: the viewport that shows <paramref name="anchor"/> at <paramref name="zoom"/> aligned per
    /// <paramref name="align"/> instead of centred. <see cref="FlowAnchorAlign.Center"/> is exactly
    /// <see cref="CenterOn"/> on the anchor's own centre. <see cref="FlowAnchorAlign.Start"/>/
    /// <see cref="FlowAnchorAlign.End"/> put the anchor's leading/trailing edge <paramref name="padding"/>
    /// (the SAME fraction <c>FitView</c> uses for its zoom margin, here read as a fraction of the pane's
    /// own width/height) in from the pane's matching edge, on BOTH axes — the smallest API that reads
    /// right for a LeftToRight tree (only the horizontal edge matters) and a TopToBottom one (only the
    /// vertical edge does); the axis that does not matter for a given tree shape just lands at the same
    /// padding, which is inert for a tree already close to centred on it. <paramref name="rtl"/> flips
    /// which physical horizontal edge "leading" means (right in RTL) — vertical is unaffected, there is
    /// no RTL concept top-to-bottom.
    /// </summary>
    public static FlowViewport AnchorAlignedViewport(FlowRect anchor, double zoom, double paneWidth, double paneHeight,
        double padding, FlowAnchorAlign align, bool rtl = false)
    {
        if (align == FlowAnchorAlign.Center)
            return CenterOn(anchor.X + anchor.Width / 2, anchor.Y + anchor.Height / 2, zoom, paneWidth, paneHeight);

        var padX = padding * paneWidth;
        var padY = padding * paneHeight;
        var startIsRight = rtl; // in RTL, the LOGICAL leading edge is the physical right edge.

        double edgeX, targetX;
        if (align == FlowAnchorAlign.Start)
        {
            edgeX = startIsRight ? anchor.Right : anchor.X;
            targetX = startIsRight ? paneWidth - padX : padX;
        }
        else
        {
            edgeX = startIsRight ? anchor.X : anchor.Right;
            targetX = startIsRight ? padX : paneWidth - padX;
        }

        var edgeY = align == FlowAnchorAlign.Start ? anchor.Y : anchor.Bottom;
        var targetY = align == FlowAnchorAlign.Start ? padY : paneHeight - padY;

        return new FlowViewport(targetX - edgeX * zoom, targetY - edgeY * zoom, zoom);
    }

    // ── Helper lines (phase 4) ──────────────────────────────────────────────

    /// <summary>
    /// Candidate alignment guides for a node being dragged: compares <paramref name="moving"/>'s
    /// left/centre/right and top/middle/bottom against every rect in <paramref name="others"/>
    /// (typically every OTHER node), and returns the single closest match per axis that is within
    /// <paramref name="threshold"/> flow units, plus the position <paramref name="moving"/> should
    /// snap to on that axis. A pure function — <c>flow.js</c> ports it for the live drag; this is
    /// what the bUnit tests exercise directly.
    /// </summary>
    public static FlowHelperLineResult ComputeHelperLines(FlowRect moving, IEnumerable<FlowRect> others, double threshold)
    {
        ArgumentNullException.ThrowIfNull(others);
        if (!(threshold > 0)) return new FlowHelperLineResult(null, null, Array.Empty<FlowHelperLine>());

        double? bestVLine = null, bestHLine = null, snapX = null, snapY = null;
        var bestVDist = double.PositiveInfinity;
        var bestHDist = double.PositiveInfinity;

        var movingXs = new[] { moving.X, moving.X + moving.Width / 2, moving.Right };
        var movingYs = new[] { moving.Y, moving.Y + moving.Height / 2, moving.Bottom };

        foreach (var other in others)
        {
            foreach (var ox in new[] { other.X, other.X + other.Width / 2, other.Right })
            {
                foreach (var mx in movingXs)
                {
                    var d = Math.Abs(mx - ox);
                    if (d > threshold || d >= bestVDist) continue;
                    bestVDist = d;
                    bestVLine = ox;
                    snapX = moving.X + (ox - mx);
                }
            }
            foreach (var oy in new[] { other.Y, other.Y + other.Height / 2, other.Bottom })
            {
                foreach (var my in movingYs)
                {
                    var d = Math.Abs(my - oy);
                    if (d > threshold || d >= bestHDist) continue;
                    bestHDist = d;
                    bestHLine = oy;
                    snapY = moving.Y + (oy - my);
                }
            }
        }

        var lines = new List<FlowHelperLine>(2);
        if (bestVLine is { } vx) lines.Add(new FlowHelperLine(vx, FlowHelperLineAxis.Vertical));
        if (bestHLine is { } hy) lines.Add(new FlowHelperLine(hy, FlowHelperLineAxis.Horizontal));
        return new FlowHelperLineResult(snapX, snapY, lines);
    }

    // ── Sub-flows (phase 5) ──────────────────────────────────────────────

    /// <summary>
    /// Every node's top-left corner in ABSOLUTE flow coordinates. A node with a
    /// <see cref="FlowNode.ParentId"/> stores its <see cref="FlowNode.X"/>/<see cref="FlowNode.Y"/>
    /// relative to its parent's top-left corner; this resolves the whole chain. A missing parent or a
    /// parent cycle counts as top-level (its own X/Y). A duplicate id keeps its first occurrence.
    /// </summary>
    public static IReadOnlyDictionary<string, FlowPoint> GetAbsolutePositions(IReadOnlyList<FlowNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var hierarchy = new FlowHierarchy(nodes);
        var result = new Dictionary<string, FlowPoint>(StringComparer.Ordinal);
        foreach (var n in nodes)
        {
            if (n is null || string.IsNullOrEmpty(n.Id) || result.ContainsKey(n.Id)) continue;
            result[n.Id] = hierarchy.AbsoluteOf(n);
        }
        return result;
    }

    /// <summary>
    /// Clamps a child's RELATIVE top-left corner so a <paramref name="width"/> × <paramref name="height"/>
    /// box stays inside a parent of <paramref name="parentWidth"/> × <paramref name="parentHeight"/>
    /// (<see cref="FlowExtent.Parent"/>). A child larger than its parent pins to the parent's top-left.
    /// </summary>
    public static FlowPoint ClampToParent(double x, double y, double width, double height, double parentWidth, double parentHeight)
    {
        var maxX = Math.Max(0, parentWidth - width);
        var maxY = Math.Max(0, parentHeight - height);
        return new FlowPoint(Math.Clamp(x, 0, maxX), Math.Clamp(y, 0, maxY));
    }

    // ── Handles ──────────────────────────────────────────────────────────

    /// <summary>
    /// The default anchor on a node's side — the midpoint of that edge. Used when the node renders
    /// no matching <see cref="FlowHandle"/> (or it has not been measured yet).
    /// </summary>
    public static FlowPoint GetHandleAnchor(FlowRect node, FlowPosition position) => position switch
    {
        FlowPosition.Left => new(node.X, node.Y + node.Height / 2),
        FlowPosition.Right => new(node.X + node.Width, node.Y + node.Height / 2),
        FlowPosition.Top => new(node.X + node.Width / 2, node.Y),
        _ => new(node.X + node.Width / 2, node.Y + node.Height),
    };

    // ── Edge paths ───────────────────────────────────────────────────────

    /// <summary>Dispatches to the generator for <paramref name="type"/>.</summary>
    public static FlowEdgePath GetEdgePath(FlowEdgeType type,
        double sourceX, double sourceY, FlowPosition sourcePosition,
        double targetX, double targetY, FlowPosition targetPosition) => type switch
    {
        FlowEdgeType.Straight => GetStraightPath(sourceX, sourceY, targetX, targetY),
        FlowEdgeType.Step => GetStepPath(sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition),
        FlowEdgeType.SmoothStep => GetSmoothStepPath(sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition),
        _ => GetBezierPath(sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition),
    };

    /// <summary>A straight line; the label sits at its midpoint.</summary>
    public static FlowEdgePath GetStraightPath(double sourceX, double sourceY, double targetX, double targetY)
    {
        var d = "M" + Fmt(sourceX) + "," + Fmt(sourceY) + " L" + Fmt(targetX) + "," + Fmt(targetY);
        return new FlowEdgePath(d, (sourceX + targetX) / 2, (sourceY + targetY) / 2);
    }

    /// <summary>
    /// A cubic Bézier whose control points run out of each anchor along its handle's direction —
    /// half the anchor distance when the other end lies ahead, a curvature-scaled loop when it lies
    /// behind. The label sits on the curve at t = 0.5.
    /// </summary>
    public static FlowEdgePath GetBezierPath(double sourceX, double sourceY, FlowPosition sourcePosition,
        double targetX, double targetY, FlowPosition targetPosition)
    {
        var (c1x, c1y) = ControlPoint(sourcePosition, sourceX, sourceY, targetX, targetY);
        var (c2x, c2y) = ControlPoint(targetPosition, targetX, targetY, sourceX, sourceY);
        var d = "M" + Fmt(sourceX) + "," + Fmt(sourceY)
              + " C" + Fmt(c1x) + "," + Fmt(c1y)
              + " " + Fmt(c2x) + "," + Fmt(c2y)
              + " " + Fmt(targetX) + "," + Fmt(targetY);
        var lx = sourceX * 0.125 + c1x * 0.375 + c2x * 0.375 + targetX * 0.125;
        var ly = sourceY * 0.125 + c1y * 0.375 + c2y * 0.375 + targetY * 0.125;
        return new FlowEdgePath(d, lx, ly);
    }

    /// <summary>An orthogonal route with sharp corners (a smooth-step with radius 0).</summary>
    public static FlowEdgePath GetStepPath(double sourceX, double sourceY, FlowPosition sourcePosition,
        double targetX, double targetY, FlowPosition targetPosition)
        => GetSmoothStepPath(sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition, 0, StepOffset);

    /// <summary>
    /// An orthogonal route: straight out of the source handle by <paramref name="offset"/>, across,
    /// and straight into the target handle, every corner rounded by up to
    /// <paramref name="borderRadius"/>. The label sits halfway along the route.
    /// </summary>
    public static FlowEdgePath GetSmoothStepPath(double sourceX, double sourceY, FlowPosition sourcePosition,
        double targetX, double targetY, FlowPosition targetPosition,
        double borderRadius = SmoothStepRadius, double offset = StepOffset)
    {
        var pts = StepPoints(sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition, offset);

        var sb = new StringBuilder();
        sb.Append('M').Append(Fmt(pts[0].X)).Append(',').Append(Fmt(pts[0].Y));
        for (var i = 1; i < pts.Count - 1; i++)
        {
            var a = pts[i - 1];
            var b = pts[i];
            var c = pts[i + 1];
            var lab = Dist(a, b);
            var lbc = Dist(b, c);
            var r = Math.Min(borderRadius, Math.Min(lab / 2, lbc / 2));
            if (r > 0 && lab > 0 && lbc > 0)
            {
                var inX = b.X - (b.X - a.X) / lab * r;
                var inY = b.Y - (b.Y - a.Y) / lab * r;
                var outX = b.X + (c.X - b.X) / lbc * r;
                var outY = b.Y + (c.Y - b.Y) / lbc * r;
                sb.Append(" L").Append(Fmt(inX)).Append(',').Append(Fmt(inY));
                sb.Append(" Q").Append(Fmt(b.X)).Append(',').Append(Fmt(b.Y));
                sb.Append(' ').Append(Fmt(outX)).Append(',').Append(Fmt(outY));
            }
            else
            {
                sb.Append(" L").Append(Fmt(b.X)).Append(',').Append(Fmt(b.Y));
            }
        }
        var last = pts[^1];
        sb.Append(" L").Append(Fmt(last.X)).Append(',').Append(Fmt(last.Y));

        var (lx, ly) = PolylineMidpoint(pts);
        return new FlowEdgePath(sb.ToString(), lx, ly);
    }

    /// <summary>
    /// The corner points of an orthogonal route (source anchor, offset point, turns, target offset
    /// point, target anchor), with duplicates and straight-through points removed.
    /// </summary>
    internal static List<FlowPoint> StepPoints(double sx, double sy, FlowPosition sPos,
        double tx, double ty, FlowPosition tPos, double offset)
    {
        var (sdx, sdy) = Direction(sPos);
        var (tdx, tdy) = Direction(tPos);
        var s0 = new FlowPoint(sx, sy);
        var t0 = new FlowPoint(tx, ty);
        var s1 = new FlowPoint(sx + sdx * offset, sy + sdy * offset);
        var t1 = new FlowPoint(tx + tdx * offset, ty + tdy * offset);
        var sH = sPos is FlowPosition.Left or FlowPosition.Right;
        var tH = tPos is FlowPosition.Left or FlowPosition.Right;

        var raw = new List<FlowPoint> { s0, s1 };
        if (sH && tH)
        {
            if (sPos == tPos)
            {
                // Both handles face the same way: run out to whichever offset point is further along.
                var ex = sdx > 0 ? Math.Max(s1.X, t1.X) : Math.Min(s1.X, t1.X);
                raw.Add(new FlowPoint(ex, s1.Y));
                raw.Add(new FlowPoint(ex, t1.Y));
            }
            else if (sdx * (t1.X - s1.X) >= 0)
            {
                var mx = (s1.X + t1.X) / 2;
                raw.Add(new FlowPoint(mx, s1.Y));
                raw.Add(new FlowPoint(mx, t1.Y));
            }
            else
            {
                // Target lies behind the source: cross over halfway between the two rows instead.
                var my = (s1.Y + t1.Y) / 2;
                raw.Add(new FlowPoint(s1.X, my));
                raw.Add(new FlowPoint(t1.X, my));
            }
        }
        else if (!sH && !tH)
        {
            if (sPos == tPos)
            {
                var ey = sdy > 0 ? Math.Max(s1.Y, t1.Y) : Math.Min(s1.Y, t1.Y);
                raw.Add(new FlowPoint(s1.X, ey));
                raw.Add(new FlowPoint(t1.X, ey));
            }
            else if (sdy * (t1.Y - s1.Y) >= 0)
            {
                var my = (s1.Y + t1.Y) / 2;
                raw.Add(new FlowPoint(s1.X, my));
                raw.Add(new FlowPoint(t1.X, my));
            }
            else
            {
                var mx = (s1.X + t1.X) / 2;
                raw.Add(new FlowPoint(mx, s1.Y));
                raw.Add(new FlowPoint(mx, t1.Y));
            }
        }
        else if (sH)
        {
            raw.Add(new FlowPoint(t1.X, s1.Y));
        }
        else
        {
            raw.Add(new FlowPoint(s1.X, t1.Y));
        }
        raw.Add(t1);
        raw.Add(t0);

        // Drop repeated points, then points a straight segment merely passes through (same
        // direction on both sides). A reversal is kept: removing it would change the route.
        var dedup = new List<FlowPoint>(raw.Count);
        foreach (var p in raw)
        {
            if (dedup.Count > 0 && Same(dedup[^1], p)) continue;
            dedup.Add(p);
        }
        var result = new List<FlowPoint>(dedup.Count);
        for (var i = 0; i < dedup.Count; i++)
        {
            if (i > 0 && i < dedup.Count - 1)
            {
                var a = result[^1];
                var b = dedup[i];
                var c = dedup[i + 1];
                var cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                var dot = (b.X - a.X) * (c.X - b.X) + (b.Y - a.Y) * (c.Y - b.Y);
                if (Math.Abs(cross) < 1e-9 && dot >= 0) continue;
            }
            result.Add(dedup[i]);
        }
        return result;
    }

    private static (double X, double Y) PolylineMidpoint(List<FlowPoint> pts)
    {
        var total = 0.0;
        for (var i = 1; i < pts.Count; i++) total += Dist(pts[i - 1], pts[i]);
        if (total <= 0) return (pts[0].X, pts[0].Y);
        var half = total / 2;
        var walked = 0.0;
        for (var i = 1; i < pts.Count; i++)
        {
            var seg = Dist(pts[i - 1], pts[i]);
            if (walked + seg >= half && seg > 0)
            {
                var t = (half - walked) / seg;
                return (pts[i - 1].X + (pts[i].X - pts[i - 1].X) * t, pts[i - 1].Y + (pts[i].Y - pts[i - 1].Y) * t);
            }
            walked += seg;
        }
        return (pts[^1].X, pts[^1].Y);
    }

    private static (double X, double Y) ControlPoint(FlowPosition position, double x1, double y1, double x2, double y2) => position switch
    {
        FlowPosition.Left => (x1 - ControlOffset(x1 - x2), y1),
        FlowPosition.Right => (x1 + ControlOffset(x2 - x1), y1),
        FlowPosition.Top => (x1, y1 - ControlOffset(y1 - y2)),
        _ => (x1, y1 + ControlOffset(y2 - y1)),
    };

    private static double ControlOffset(double distance)
        => distance >= 0 ? 0.5 * distance : BezierCurvature * 25 * Math.Sqrt(-distance);

    private static (double X, double Y) Direction(FlowPosition position) => position switch
    {
        FlowPosition.Left => (-1, 0),
        FlowPosition.Right => (1, 0),
        FlowPosition.Top => (0, -1),
        _ => (0, 1),
    };

    private static double Dist(FlowPoint a, FlowPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool Same(FlowPoint a, FlowPoint b) => Math.Abs(a.X - b.X) < 1e-9 && Math.Abs(a.Y - b.Y) < 1e-9;

    /// <summary>
    /// Formats a coordinate the way <c>flow.js</c> does: rounded to 3 decimals with
    /// <c>floor(v * 1000 + 0.5) / 1000</c> (JavaScript's <c>Math.round</c>), culture-invariant, shortest
    /// round-trip digits, and never "-0".
    /// </summary>
    internal static string Fmt(double value)
    {
        if (!double.IsFinite(value)) return "0";
        var r = Math.Floor(value * 1000 + 0.5) / 1000;
        if (r == 0) r = 0; // normalises -0
        return r.ToString("R", CultureInfo.InvariantCulture);
    }

    internal static string FormatPosition(FlowPosition position) => position switch
    {
        FlowPosition.Left => "left",
        FlowPosition.Right => "right",
        FlowPosition.Top => "top",
        _ => "bottom",
    };

    internal static FlowPosition? ParsePosition(string? value) => value switch
    {
        "left" => FlowPosition.Left,
        "right" => FlowPosition.Right,
        "top" => FlowPosition.Top,
        "bottom" => FlowPosition.Bottom,
        _ => null,
    };

    internal static string FormatEdgeType(FlowEdgeType type) => type switch
    {
        FlowEdgeType.SmoothStep => "smoothstep",
        FlowEdgeType.Step => "step",
        FlowEdgeType.Straight => "straight",
        _ => "bezier",
    };
}
