using Lumeo.GanttV3;
using Xunit;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Fixture-coordinate regression tests for <see cref="GanttArrowRouting"/> —
/// the C# port of gantt-v2.js's dependency-arrow drawing block (<c>render()</c>'s
/// trailing <c>tasks.forEach</c> loop, gantt-v2.js lines 646-669). Every expected
/// value below is hand-derived by literally executing that JS block on paper:
/// <code>
/// sx = source.x + source.w
/// sy = BarTop(source.RowIndex, barHeight) + barHeight / 2   // source.y + BAR_HEIGHT/2
/// tx = target.x
/// ty = BarTop(target.RowIndex, barHeight) + barHeight / 2   // target.y + BAR_HEIGHT/2
/// midX = sx + 12
/// path      = "M {sx} {sy} L {midX} {sy} L {midX} {ty} L {tx-4} {ty}"
/// arrowhead = "{tx-6},{ty-4} {tx},{ty} {tx-6},{ty+4}"
/// </code>
/// where BarTop(rowIndex, barHeight) = rowIndex * GanttScale.RowHeight (36) +
/// (GanttScale.RowHeight - barHeight) / 2 — see GanttScale.BarTop.
///
/// v2 has NO special case for "target left of source" or "same row" — one
/// formula for every case (verified by reading render()'s dependency loop
/// end-to-end; see GanttArrowRouting's own remarks) — so the fixtures below
/// deliberately include a backward arrow and a same-row arrow, asserting the
/// (visually redundant but faithful) output v2 itself would produce, not a
/// "smarter" alternative.
/// </summary>
public class GanttArrowRoutingTests
{
    // ── Forward arrow, adjacent rows, default bar height (22) ───────────────
    //
    // source: X=0, Width=100, RowIndex=0, barHeight=22
    //   BarTop(0,22) = 0*36 + (36-22)/2 = 7          -> sy = 7 + 11 = 18
    //   sx = 0 + 100 = 100                            -> midX = 100 + 12 = 112
    // target: X=150, RowIndex=1
    //   BarTop(1,22) = 1*36 + 7 = 43                  -> ty = 43 + 11 = 54
    //   tx = 150                                       -> tx-4 = 146
    [Fact]
    public void ComputePathD_Forward_Arrow_Adjacent_Rows_Matches_Hand_Derived_Path()
    {
        var source = new GanttArrowRouting.BarGeometry(0, 100, 0);
        var target = new GanttArrowRouting.BarGeometry(150, 60, 1);

        var pathD = GanttArrowRouting.ComputePathD(source, target, barHeight: 22);

        Assert.Equal("M 100 18 L 112 18 L 112 54 L 146 54", pathD);
    }

    [Fact]
    public void ComputeArrowheadPoints_Forward_Arrow_Adjacent_Rows_Matches_Hand_Derived_Points()
    {
        var target = new GanttArrowRouting.BarGeometry(150, 60, 1);

        var points = GanttArrowRouting.ComputeArrowheadPoints(target, barHeight: 22);

        // tx-6=144, ty-4=50; tx=150, ty=54; tx-6=144, ty+4=58
        Assert.Equal("144,50 150,54 144,58", points);
    }

    // ── Backward arrow (target LEFT of source), different rows ──────────────
    //
    // source: X=300, Width=40, RowIndex=3, barHeight=22
    //   BarTop(3,22) = 3*36 + 7 = 115                 -> sy = 115 + 11 = 126
    //   sx = 300 + 40 = 340                            -> midX = 340 + 12 = 352
    // target: X=50, RowIndex=1
    //   BarTop(1,22) = 43                              -> ty = 43 + 11 = 54
    //   tx = 50                                        -> tx-4 = 46
    //
    // No special-case branch exists in v2 for this — the elbow still projects
    // forward from the source (to midX=352) before doubling back to the
    // target's left edge, exactly like a forward arrow's shape.
    [Fact]
    public void ComputePathD_Backward_Arrow_Target_Left_Of_Source_Uses_The_Same_Unconditional_Formula()
    {
        var source = new GanttArrowRouting.BarGeometry(300, 40, 3);
        var target = new GanttArrowRouting.BarGeometry(50, 30, 1);

        var pathD = GanttArrowRouting.ComputePathD(source, target, barHeight: 22);

        Assert.Equal("M 340 126 L 352 126 L 352 54 L 46 54", pathD);
    }

    // ── Same-row arrow (source.RowIndex == target.RowIndex) ─────────────────
    //
    // source: X=200, Width=50, RowIndex=2, barHeight=22
    //   BarTop(2,22) = 2*36 + 7 = 79                   -> sy = 79 + 11 = 90
    //   sx = 200 + 50 = 250                            -> midX = 250 + 12 = 262
    // target: X=30, RowIndex=2 (SAME row as source)
    //   ty = 90 (identical to sy since same row)        -> tx = 30, tx-4 = 26
    //
    // sy == ty here, so the path's two elbow points collapse to the same Y —
    // a zero-vertical-extent `L` segment, which v2 emits unconditionally too
    // (it never checks whether the elbow is a no-op).
    [Fact]
    public void ComputePathD_Same_Row_Arrow_Produces_A_Flat_Elbow_Matching_V2_Verbatim()
    {
        var source = new GanttArrowRouting.BarGeometry(200, 50, 2);
        var target = new GanttArrowRouting.BarGeometry(30, 40, 2);

        var pathD = GanttArrowRouting.ComputePathD(source, target, barHeight: 22);

        Assert.Equal("M 250 90 L 262 90 L 262 90 L 26 90", pathD);
    }

    [Fact]
    public void ComputeArrowheadPoints_Same_Row_Arrow_Matches_Hand_Derived_Points()
    {
        var target = new GanttArrowRouting.BarGeometry(30, 40, 2);

        var points = GanttArrowRouting.ComputeArrowheadPoints(target, barHeight: 22);

        // tx-6=24, ty-4=86; tx=30, ty=90; tx-6=24, ty+4=94
        Assert.Equal("24,86 30,90 24,94", points);
    }

    // ── Non-default bar height (16) — proves BarHeight actually scales the
    //    vertical-center math, not just a hardcoded 22 ──────────────────────
    //
    // source: X=0, Width=20, RowIndex=0, barHeight=16
    //   BarTop(0,16) = 0*36 + (36-16)/2 = 10            -> sy = 10 + 8 = 18
    //   sx = 0 + 20 = 20                                 -> midX = 20 + 12 = 32
    // target: X=80, RowIndex=0 (same row)
    //   ty = 18 (same row)                               -> tx = 80, tx-4 = 76
    [Fact]
    public void ComputePathD_Honors_A_Non_Default_BarHeight()
    {
        var source = new GanttArrowRouting.BarGeometry(0, 20, 0);
        var target = new GanttArrowRouting.BarGeometry(80, 20, 0);

        var pathD = GanttArrowRouting.ComputePathD(source, target, barHeight: 16);

        Assert.Equal("M 20 18 L 32 18 L 32 18 L 76 18", pathD);
    }
}
