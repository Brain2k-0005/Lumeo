using System.Text.Json;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// FlowGeometry — the pure math every FlowCanvas gesture and render relies on: coordinate spaces,
/// snapping, fit-view, zoom anchoring, handle anchors and the four edge path generators.
/// </summary>
public class FlowGeometryTests
{
    private const double Eps = 1e-9;

    // ── Coordinate spaces ────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, 1, 0, 0, 0, 0)]
    [InlineData(100, 50, 1, 100, 50, 0, 0)]
    [InlineData(100, 50, 2, 300, 250, 100, 100)]
    [InlineData(-40, 20, 0.5, 10, 70, 100, 100)]
    public void ScreenToFlow_Subtracts_The_Translation_And_Divides_By_Zoom(
        double vx, double vy, double zoom, double sx, double sy, double fx, double fy)
    {
        var p = L.FlowGeometry.ScreenToFlow(sx, sy, new L.FlowViewport(vx, vy, zoom));
        Assert.Equal(fx, p.X, 9);
        Assert.Equal(fy, p.Y, 9);
    }

    [Fact]
    public void FlowToScreen_Is_The_Inverse_Of_ScreenToFlow()
    {
        var vp = new L.FlowViewport(-123.25, 47.5, 1.37);
        foreach (var (x, y) in new[] { (0.0, 0.0), (10.5, -3.25), (1000.0, 800.0), (-50.0, 12.0) })
        {
            var screen = L.FlowGeometry.FlowToScreen(x, y, vp);
            var back = L.FlowGeometry.ScreenToFlow(screen.X, screen.Y, vp);
            Assert.Equal(x, back.X, 9);
            Assert.Equal(y, back.Y, 9);
        }
    }

    [Fact]
    public void A_Zero_Zoom_Viewport_Is_Treated_As_100_Percent_Rather_Than_Dividing_By_Zero()
    {
        var p = L.FlowGeometry.ScreenToFlow(10, 20, new L.FlowViewport(0, 0, 0));
        Assert.Equal(10, p.X);
        Assert.Equal(20, p.Y);
    }

    // ── Zoom ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 0.25, 2, 1)]
    [InlineData(0.1, 0.25, 2, 0.25)]
    [InlineData(5, 0.25, 2, 2)]
    [InlineData(1, 2, 0.5, 2)] // max below min collapses to min
    public void ClampZoom_Clamps_Into_The_Limits(double zoom, double min, double max, double expected)
        => Assert.Equal(expected, L.FlowGeometry.ClampZoom(zoom, min, max));

    [Fact]
    public void ClampZoom_Maps_NaN_To_The_Minimum()
        => Assert.Equal(0.25, L.FlowGeometry.ClampZoom(double.NaN, 0.25, 2));

    [Theory]
    [InlineData(0, 0, 1, 2, 400, 250)]
    [InlineData(-120.5, 33, 0.75, 0.5, 10, 490)]
    [InlineData(300, -80, 1.8, 0.3, 777, 5)]
    public void ZoomAt_Keeps_The_Flow_Point_Under_The_Anchor_Fixed(double x, double y, double zoom, double to, double ax, double ay)
    {
        var before = new L.FlowViewport(x, y, zoom);
        var flowUnderAnchor = L.FlowGeometry.ScreenToFlow(ax, ay, before);
        var after = L.FlowGeometry.ZoomAt(before, to, ax, ay);
        Assert.Equal(to, after.Zoom);
        var screen = L.FlowGeometry.FlowToScreen(flowUnderAnchor.X, flowUnderAnchor.Y, after);
        Assert.Equal(ax, screen.X, 9);
        Assert.Equal(ay, screen.Y, 9);
    }

    [Fact]
    public void CenterOn_Puts_The_Flow_Point_In_The_Middle_Of_The_Pane()
    {
        var vp = L.FlowGeometry.CenterOn(100, 50, 2, 800, 600);
        var screen = L.FlowGeometry.FlowToScreen(100, 50, vp);
        Assert.Equal(400, screen.X, 9);
        Assert.Equal(300, screen.Y, 9);
    }

    // ── Snapping ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 16, 0)]
    [InlineData(7.9, 16, 0)]
    [InlineData(8, 16, 16)]       // ties round up, like JS Math.round
    [InlineData(-8, 16, 0)]       // ...towards +infinity, also for negatives
    [InlineData(-8.1, 16, -16)]
    [InlineData(23.5, 15, 30)]
    [InlineData(100, 0, 100)]     // grid 0 = no snapping
    [InlineData(-3, -5, -3)]      // negative grid = no snapping
    public void Snap_Rounds_To_The_Nearest_Grid_Line(double v, double grid, double expected)
        => Assert.Equal(expected, L.FlowGeometry.Snap(v, grid), 9);

    [Fact]
    public void Snap_Point_Snaps_Each_Axis_To_Its_Own_Grid()
    {
        var p = L.FlowGeometry.Snap(new L.FlowPoint(13, 13), 10, 4);
        Assert.Equal(10, p.X);
        Assert.Equal(12, p.Y);
    }

    // ── Bounds / fit-view ────────────────────────────────────────────────

    [Fact]
    public void GetBounds_Is_Null_For_No_Rects_And_The_Union_Otherwise()
    {
        Assert.Null(L.FlowGeometry.GetBounds(Array.Empty<L.FlowRect>()));
        var b = L.FlowGeometry.GetBounds(new[] { new L.FlowRect(10, 20, 100, 40), new L.FlowRect(-50, 100, 30, 30) })!.Value;
        Assert.Equal(new L.FlowRect(-50, 20, 160, 110), b);
        Assert.Equal(110, b.Right);
        Assert.Equal(130, b.Bottom);
    }

    [Fact]
    public void FitView_Centres_The_Bounds_And_Leaves_The_Padding()
    {
        var rects = new[] { new L.FlowRect(0, 0, 150, 40), new L.FlowRect(400, 300, 150, 40) };
        var vp = L.FlowGeometry.FitView(rects, 800, 500, 0.1, 0.25, 2)!.Value;

        // Bounds 550 x 340: x-zoom 800/(550*1.1)=1.322, y-zoom 500/(340*1.1)=1.337 -> the smaller.
        Assert.Equal(800 / (550 * 1.1), vp.Zoom, 9);
        // Bounds centre lands on the pane centre.
        var c = L.FlowGeometry.FlowToScreen(275, 170, vp);
        Assert.Equal(400, c.X, 9);
        Assert.Equal(250, c.Y, 9);
        // Padding: the bounds occupy 1/1.1 of the limiting (horizontal) extent.
        var left = L.FlowGeometry.FlowToScreen(0, 0, vp);
        var right = L.FlowGeometry.FlowToScreen(550, 0, vp);
        Assert.Equal(800 / 1.1, right.X - left.X, 6);
    }

    [Fact]
    public void FitView_Clamps_To_MaxZoom_For_A_Tiny_Graph()
    {
        var vp = L.FlowGeometry.FitView(new[] { new L.FlowRect(10, 10, 20, 20) }, 1000, 600, 0, 0.5, 4)!.Value;
        Assert.Equal(4, vp.Zoom);
        var c = L.FlowGeometry.FlowToScreen(20, 20, vp);
        Assert.Equal(500, c.X, 9);
        Assert.Equal(300, c.Y, 9);
    }

    [Fact]
    public void FitView_Clamps_To_MinZoom_For_A_Huge_Graph()
    {
        var vp = L.FlowGeometry.FitView(new[] { new L.FlowRect(0, 0, 50000, 30000) }, 800, 500, 0.2, 0.25, 2)!.Value;
        Assert.Equal(0.25, vp.Zoom);
    }

    [Fact]
    public void FitView_Is_Null_Without_Rects_Or_Without_A_Pane_Size()
    {
        Assert.Null(L.FlowGeometry.FitView(Array.Empty<L.FlowRect>(), 800, 500, 0.1, 0.25, 2));
        Assert.Null(L.FlowGeometry.FitView(new[] { new L.FlowRect(0, 0, 10, 10) }, 0, 500, 0.1, 0.25, 2));
        Assert.Null(L.FlowGeometry.FitView(new[] { new L.FlowRect(0, 0, 10, 10) }, 800, 0, 0.1, 0.25, 2));
    }

    [Fact]
    public void FitView_Treats_Negative_Padding_As_None()
    {
        var a = L.FlowGeometry.FitView(new[] { new L.FlowRect(0, 0, 100, 100) }, 200, 200, -1, 0.1, 10)!.Value;
        Assert.Equal(2, a.Zoom, 9);
    }

    // ── Handles ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(L.FlowPosition.Left, 10, 45)]
    [InlineData(L.FlowPosition.Right, 160, 45)]
    [InlineData(L.FlowPosition.Top, 85, 20)]
    [InlineData(L.FlowPosition.Bottom, 85, 70)]
    public void GetHandleAnchor_Is_The_Midpoint_Of_That_Side(L.FlowPosition position, double x, double y)
    {
        var p = L.FlowGeometry.GetHandleAnchor(new L.FlowRect(10, 20, 150, 50), position);
        Assert.Equal(x, p.X, 9);
        Assert.Equal(y, p.Y, 9);
    }

    // ── Edge paths ───────────────────────────────────────────────────────

    [Fact]
    public void Straight_Is_A_Line_With_The_Label_At_Its_Midpoint()
    {
        var p = L.FlowGeometry.GetStraightPath(0, 0, 100, 50);
        Assert.Equal("M0,0 L100,50", p.D);
        Assert.Equal(50, p.LabelX);
        Assert.Equal(25, p.LabelY);
    }

    [Fact]
    public void Bezier_Right_To_Left_Bends_Out_By_Half_The_Horizontal_Distance()
    {
        var p = L.FlowGeometry.GetBezierPath(0, 0, L.FlowPosition.Right, 200, 100, L.FlowPosition.Left);
        Assert.Equal("M0,0 C100,0 100,100 200,100", p.D);
        // t = 0.5 on the cubic
        Assert.Equal(100, p.LabelX, 9);
        Assert.Equal(50, p.LabelY, 9);
    }

    [Fact]
    public void Bezier_To_A_Target_Behind_The_Source_Loops_Out_With_The_Curvature()
    {
        // Target lies left of a right-facing source: distance < 0 -> 0.25 * 25 * sqrt(300) = 108.253
        var p = L.FlowGeometry.GetBezierPath(300, 50, L.FlowPosition.Right, 0, 0, L.FlowPosition.Left);
        Assert.Equal("M300,50 C408.253,50 -108.253,0 0,0", p.D);
    }

    [Fact]
    public void Bezier_Top_Bottom_Uses_Vertical_Control_Points()
    {
        var p = L.FlowGeometry.GetBezierPath(0, 0, L.FlowPosition.Bottom, 0, 200, L.FlowPosition.Top);
        Assert.Equal("M0,0 C0,100 0,100 0,200", p.D);
    }

    [Fact]
    public void Step_Right_To_Left_Runs_Out_Crosses_At_The_Midpoint_And_Runs_In()
    {
        var p = L.FlowGeometry.GetStepPath(0, 0, L.FlowPosition.Right, 200, 100, L.FlowPosition.Left);
        Assert.Equal("M0,0 L100,0 L100,100 L200,100", p.D);
        Assert.Equal(100, p.LabelX, 9);
        Assert.Equal(50, p.LabelY, 9);
    }

    [Fact]
    public void SmoothStep_Rounds_Every_Corner_With_A_Quadratic()
    {
        var p = L.FlowGeometry.GetSmoothStepPath(0, 0, L.FlowPosition.Right, 200, 100, L.FlowPosition.Left);
        Assert.Equal("M0,0 L95,0 Q100,0 100,5 L100,95 Q100,100 105,100 L200,100", p.D);
    }

    [Fact]
    public void SmoothStep_Radius_Never_Exceeds_Half_The_Shorter_Segment()
    {
        // A 6px vertical jog: radius is capped at 3 (half of it), not the default 5.
        var p = L.FlowGeometry.GetSmoothStepPath(0, 0, L.FlowPosition.Right, 200, 6, L.FlowPosition.Left);
        Assert.Equal("M0,0 L97,0 Q100,0 100,3 L100,3 Q100,6 103,6 L200,6", p.D);
    }

    [Fact]
    public void Step_To_A_Target_Behind_The_Source_Crosses_Between_The_Rows()
    {
        var pts = L.FlowGeometry.StepPoints(300, 50, L.FlowPosition.Right, 0, 0, L.FlowPosition.Left, 20);
        Assert.Equal(new[]
        {
            new L.FlowPoint(300, 50), new L.FlowPoint(320, 50), new L.FlowPoint(320, 25),
            new L.FlowPoint(-20, 25), new L.FlowPoint(-20, 0), new L.FlowPoint(0, 0),
        }, pts);
    }

    [Fact]
    public void Step_Between_Mixed_Sides_Turns_Once()
    {
        var pts = L.FlowGeometry.StepPoints(0, 0, L.FlowPosition.Right, 100, 100, L.FlowPosition.Top, 20);
        Assert.Equal(new[]
        {
            new L.FlowPoint(0, 0), new L.FlowPoint(100, 0), new L.FlowPoint(100, 100),
        }, pts);
    }

    [Fact]
    public void Step_Between_Two_Right_Facing_Handles_Runs_Out_To_The_Further_One()
    {
        var pts = L.FlowGeometry.StepPoints(0, 0, L.FlowPosition.Right, 100, 100, L.FlowPosition.Right, 20);
        Assert.Equal(new[]
        {
            new L.FlowPoint(0, 0), new L.FlowPoint(120, 0), new L.FlowPoint(120, 100), new L.FlowPoint(100, 100),
        }, pts);
    }

    [Fact]
    public void A_Degenerate_Edge_Onto_Its_Own_Anchor_Still_Produces_A_Valid_Path()
    {
        foreach (L.FlowEdgeType type in Enum.GetValues(typeof(L.FlowEdgeType)))
        {
            var p = L.FlowGeometry.GetEdgePath(type, -50, -40, L.FlowPosition.Right, -50, -40, L.FlowPosition.Left);
            Assert.StartsWith("M-50,-40", p.D);
            Assert.True(double.IsFinite(p.LabelX) && double.IsFinite(p.LabelY));
        }
    }

    [Fact]
    public void GetEdgePath_Dispatches_On_The_Type()
    {
        Assert.Equal(L.FlowGeometry.GetStraightPath(1, 2, 3, 4),
            L.FlowGeometry.GetEdgePath(L.FlowEdgeType.Straight, 1, 2, L.FlowPosition.Right, 3, 4, L.FlowPosition.Left));
        Assert.Equal(L.FlowGeometry.GetStepPath(1, 2, L.FlowPosition.Right, 3, 4, L.FlowPosition.Left),
            L.FlowGeometry.GetEdgePath(L.FlowEdgeType.Step, 1, 2, L.FlowPosition.Right, 3, 4, L.FlowPosition.Left));
        Assert.Equal(L.FlowGeometry.GetSmoothStepPath(1, 2, L.FlowPosition.Right, 3, 4, L.FlowPosition.Left),
            L.FlowGeometry.GetEdgePath(L.FlowEdgeType.SmoothStep, 1, 2, L.FlowPosition.Right, 3, 4, L.FlowPosition.Left));
        Assert.Equal(L.FlowGeometry.GetBezierPath(1, 2, L.FlowPosition.Right, 3, 4, L.FlowPosition.Left),
            L.FlowGeometry.GetEdgePath(L.FlowEdgeType.Bezier, 1, 2, L.FlowPosition.Right, 3, 4, L.FlowPosition.Left));
    }

    [Theory]
    [InlineData(0.0004, "0")]
    [InlineData(-0.0004, "0")]    // never "-0"
    [InlineData(0.0005, "0.001")]
    [InlineData(-0.0005, "0")]    // ties towards +infinity, like Math.round
    [InlineData(123.4565, "123.457")]
    [InlineData(-123.4565, "-123.456")]
    [InlineData(1000000.1234, "1000000.123")]
    public void Coordinates_Format_Like_FlowJs(double v, string expected)
        => Assert.Equal(expected, L.FlowGeometry.Fmt(v));

    [Fact]
    public void Coordinate_Formatting_Ignores_The_Current_Culture()
    {
        var prev = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("M1.5,2.25 L3.125,4", L.FlowGeometry.GetStraightPath(1.5, 2.25, 3.125, 4).D);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = prev;
        }
    }
}

/// <summary>
/// The C# half of the flow.js lockstep: FlowGeometry must reproduce, character for character, the
/// table flow.js's own functions produced (tests/js/flow-geometry-table.mjs; the JS half,
/// tests/js/flow-geometry.test.mjs, keeps that table honest). An edge the engine redrew during a
/// drag and the path the next render writes must be the same string, or the edge twitches on drop.
/// </summary>
public class FlowGeometryLockstepTests
{
    private static JsonElement LoadTable()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (; dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "Lumeo.Tests", "Components", "Flow", "flow-geometry-table.json");
            if (File.Exists(candidate)) return JsonDocument.Parse(File.ReadAllText(candidate)).RootElement;
        }
        throw new InvalidOperationException("flow-geometry-table.json not found above " + AppContext.BaseDirectory);
    }

    private static L.FlowPosition Pos(string s) => s switch
    {
        "left" => L.FlowPosition.Left,
        "right" => L.FlowPosition.Right,
        "top" => L.FlowPosition.Top,
        _ => L.FlowPosition.Bottom,
    };

    private static L.FlowEdgeType Type(string s) => s switch
    {
        "smoothstep" => L.FlowEdgeType.SmoothStep,
        "step" => L.FlowEdgeType.Step,
        "straight" => L.FlowEdgeType.Straight,
        _ => L.FlowEdgeType.Bezier,
    };

    [Fact]
    public void Every_Edge_Path_Matches_FlowJs()
    {
        var edges = LoadTable().GetProperty("edges");
        Assert.True(edges.GetArrayLength() >= 400);
        var mismatches = new List<string>();
        foreach (var e in edges.EnumerateArray())
        {
            var type = e.GetProperty("type").GetString()!;
            var sPos = e.GetProperty("sPos").GetString()!;
            var tPos = e.GetProperty("tPos").GetString()!;
            var p = L.FlowGeometry.GetEdgePath(Type(type),
                e.GetProperty("sx").GetDouble(), e.GetProperty("sy").GetDouble(), Pos(sPos),
                e.GetProperty("tx").GetDouble(), e.GetProperty("ty").GetDouble(), Pos(tPos));
            var d = e.GetProperty("d").GetString();
            if (p.D != d
                || Math.Abs(p.LabelX - e.GetProperty("labelX").GetDouble()) > 1e-9
                || Math.Abs(p.LabelY - e.GetProperty("labelY").GetDouble()) > 1e-9)
            {
                mismatches.Add($"{type} {sPos}->{tPos} ({e.GetProperty("sx")},{e.GetProperty("sy")})->({e.GetProperty("tx")},{e.GetProperty("ty")}): js '{d}' vs c# '{p.D}'");
            }
        }
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(20)));
    }

    [Fact]
    public void FitView_Matches_FlowJs()
    {
        foreach (var f in LoadTable().GetProperty("fits").EnumerateArray())
        {
            var rects = f.GetProperty("rects").EnumerateArray()
                .Select(r => new L.FlowRect(r.GetProperty("x").GetDouble(), r.GetProperty("y").GetDouble(), r.GetProperty("width").GetDouble(), r.GetProperty("height").GetDouble()))
                .ToList();
            var vp = L.FlowGeometry.FitView(rects, f.GetProperty("w").GetDouble(), f.GetProperty("h").GetDouble(),
                f.GetProperty("padding").GetDouble(), f.GetProperty("min").GetDouble(), f.GetProperty("max").GetDouble())!.Value;
            Assert.Equal(f.GetProperty("x").GetDouble(), vp.X, 9);
            Assert.Equal(f.GetProperty("y").GetDouble(), vp.Y, 9);
            Assert.Equal(f.GetProperty("zoom").GetDouble(), vp.Zoom, 9);
        }
    }

    [Fact]
    public void Snap_ZoomAt_And_Number_Formatting_Match_FlowJs()
    {
        var table = LoadTable();
        foreach (var s in table.GetProperty("snaps").EnumerateArray())
        {
            Assert.Equal(s.GetProperty("r").GetDouble(), L.FlowGeometry.Snap(s.GetProperty("v").GetDouble(), s.GetProperty("grid").GetDouble()), 9);
        }
        foreach (var z in table.GetProperty("zooms").EnumerateArray())
        {
            var vp = L.FlowGeometry.ZoomAt(
                new L.FlowViewport(z.GetProperty("x").GetDouble(), z.GetProperty("y").GetDouble(), z.GetProperty("zoom").GetDouble()),
                z.GetProperty("to").GetDouble(), z.GetProperty("ax").GetDouble(), z.GetProperty("ay").GetDouble());
            Assert.Equal(z.GetProperty("rx").GetDouble(), vp.X, 9);
            Assert.Equal(z.GetProperty("ry").GetDouble(), vp.Y, 9);
            Assert.Equal(z.GetProperty("rzoom").GetDouble(), vp.Zoom, 9);
        }
        foreach (var f in table.GetProperty("formats").EnumerateArray())
        {
            Assert.Equal(f.GetProperty("s").GetString(), L.FlowGeometry.Fmt(f.GetProperty("v").GetDouble()));
        }
    }

    // ── Helper lines (phase 4) ──────────────────────────────────────────

    [Fact]
    public void ComputeHelperLines_Snaps_A_Left_Edge_To_Another_Nodes_Left_Edge()
    {
        var moving = new L.FlowRect(203, 0, 100, 50);
        var others = new[] { new L.FlowRect(200, 300, 100, 50) };

        var result = L.FlowGeometry.ComputeHelperLines(moving, others, 5);

        Assert.Equal(200, result.SnapX);
        Assert.Null(result.SnapY);
        var line = Assert.Single(result.Lines);
        Assert.Equal(200, line.Position);
        Assert.Equal(L.FlowHelperLineAxis.Vertical, line.Axis);
    }

    [Fact]
    public void ComputeHelperLines_Snaps_A_Centre_To_Another_Nodes_Centre()
    {
        // Different widths so left/right land far apart and only the CENTRES coincide (equal
        // widths would move left/centre/right by the same offset and always tie with centre).
        var moving = new L.FlowRect(90, 0, 20, 50); // centre X = 100
        var others = new[] { new L.FlowRect(0, 300, 200, 50) }; // left 0, centre 100, right 200

        var result = L.FlowGeometry.ComputeHelperLines(moving, others, 5);

        Assert.Equal(90, result.SnapX); // already aligned — no shift needed
        Assert.Contains(result.Lines, l => l.Axis == L.FlowHelperLineAxis.Vertical && l.Position == 100);
    }

    [Fact]
    public void ComputeHelperLines_Finds_Both_A_Vertical_And_A_Horizontal_Guide_At_Once()
    {
        var moving = new L.FlowRect(202, 198, 100, 50);
        var others = new[] { new L.FlowRect(200, 145, 100, 50) }; // left edge 2px off; bottom (195) 3px off moving's top (198)

        var result = L.FlowGeometry.ComputeHelperLines(moving, others, 5);

        Assert.Equal(2, result.Lines.Count);
        Assert.NotNull(result.SnapX);
    }

    [Fact]
    public void ComputeHelperLines_Ignores_Anything_Outside_The_Threshold()
    {
        var moving = new L.FlowRect(230, 0, 100, 50);
        var others = new[] { new L.FlowRect(200, 300, 100, 50) }; // 30px away on every candidate axis

        var result = L.FlowGeometry.ComputeHelperLines(moving, others, 5);

        Assert.Null(result.SnapX);
        Assert.Null(result.SnapY);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void ComputeHelperLines_Picks_The_Closest_Candidate_Even_When_A_Farther_One_Is_Checked_First()
    {
        var moving = new L.FlowRect(100, 0, 50, 50); // left edge at 100
        // others[0]'s left edge is 3px away (checked FIRST, sets an initial best); others[1]'s is an
        // EXACT match (0px), checked second — the closer one must win despite arriving later.
        var others = new[] { new L.FlowRect(103, 300, 50, 50), new L.FlowRect(100, 300, 50, 50) };

        var result = L.FlowGeometry.ComputeHelperLines(moving, others, 5);

        Assert.Equal(100, result.SnapX);
        Assert.Contains(result.Lines, l => l.Axis == L.FlowHelperLineAxis.Vertical && l.Position == 100);
    }

    [Fact]
    public void ComputeHelperLines_With_No_Other_Rects_Finds_Nothing()
    {
        var result = L.FlowGeometry.ComputeHelperLines(new L.FlowRect(0, 0, 100, 50), Array.Empty<L.FlowRect>(), 5);
        Assert.Null(result.SnapX);
        Assert.Null(result.SnapY);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void ComputeHelperLines_With_A_Zero_Threshold_Finds_Nothing()
    {
        var moving = new L.FlowRect(200, 0, 100, 50);
        var others = new[] { new L.FlowRect(200, 300, 100, 50) }; // exact alignment, but threshold is 0

        var result = L.FlowGeometry.ComputeHelperLines(moving, others, 0);

        Assert.Empty(result.Lines);
    }
}
