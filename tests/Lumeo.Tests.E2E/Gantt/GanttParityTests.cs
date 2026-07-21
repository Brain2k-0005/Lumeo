using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// v2/v3 Gantt parity harness (feat/gantt-v3, T4 — see the design spec's
/// "Parity harness (the gate for the swap)" section and
/// docs/superpowers/gantt-v3-t4-report.md). Both routes render the SAME 12-task
/// fixture (<c>GanttParityFixtures.SharedTasks</c>) through
/// <c>tests/Lumeo.Tests.ServerHost</c>'s <c>/e2e/gantt-v2</c> (v2's <c>Gantt</c>,
/// SVG) and <c>/e2e/gantt-v3</c> (the working-name <c>Gantt3</c>, plain divs).
///
/// Every geometry assertion compares THREE values: v2's rendered DOM, v3's
/// rendered DOM, and <see cref="GanttDayModeMath"/>'s independent re-derivation
/// from the fixture's own dates — not just v2 against v3 — so a bug shared by
/// both renderers can't hide behind an "they agree with each other" false pass.
///
/// KNOWN INTENTIONAL DELTA (pinned, not a failure): the milestone LABEL sits
/// ~2px further right in v3 (flex <c>ms-2</c> margin, ~8px) than v2 (SVG
/// <c>cx+half+6</c>, 6px) — see the T2 review entry in
/// docs/superpowers/gantt-v3-ledger.md. The milestone DIAMOND geometry itself
/// (bounding box) is asserted strictly; the label's do X position is not.
/// </summary>
public class GanttParityTests : GanttParityTestBase
{
    // ── Fixed row-slot indices the flattened row model assigns each task —
    // see GanttParityFixtures.SharedTasks' remarks: a synthetic GroupHeader row
    // is injected before each group's first task and occupies its own slot
    // (v2's rowSlot counter / v3's Rows-list position both advance for it), so
    // task row indices are offset by however many group headers precede them.
    private static readonly Dictionary<string, int> RowIndex = new()
    {
        ["fe1"] = 1, ["fe2"] = 2, ["fe-ms"] = 3, ["fe3"] = 4, ["fe4"] = 5, ["fe5"] = 6,
        ["be1"] = 8, ["be2"] = 9, ["be3"] = 10, ["be4"] = 11, ["be5"] = 12, ["be6"] = 13,
    };

    private static readonly DateTime Origin = GanttDayModeMath.Origin(new DateTime(2026, 2, 23)); // fe1.Start

    private const double PxTolerance = 1.0;

    // ── Bar count ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bar_count_matches_between_v2_and_v3()
    {
        await GotoHost("/e2e/gantt-v2");
        var v2Count = await WaitAndCountV2Bars();

        await GotoHost("/e2e/gantt-v3");
        var v3Count = await WaitAndCountV3Bars();

        Assert.Equal(12, v2Count);
        Assert.Equal(12, v3Count);
    }

    // ── Bar geometry (regular bars) ──────────────────────────────────────────

    [Theory]
    [InlineData("fe1", "2026-02-23", "2026-03-01")]
    [InlineData("fe2", "2026-03-01", "2026-03-08")]
    [InlineData("fe3", "2026-03-08", "2026-03-15")]
    [InlineData("be1", "2026-03-01", "2026-03-10")]
    [InlineData("be2", "2026-03-05", "2026-03-18")]
    [InlineData("be6", "2026-03-28", "2026-04-03")]
    public async Task Bar_x_and_width_match_expected_within_tolerance(string taskId, string startIso, string endIso)
    {
        var start = DateTime.Parse(startIso, CultureInfo.InvariantCulture);
        var end = DateTime.Parse(endIso, CultureInfo.InvariantCulture);
        var (expectedX, expectedWidth) = GanttDayModeMath.BarGeometry(Origin, start, end, isMilestone: false);

        await GotoHost("/e2e/gantt-v2");
        await WaitAndCountV2Bars();
        var (v2X, v2W) = await ReadV2BarGeometry(taskId);

        await GotoHost("/e2e/gantt-v3");
        await WaitAndCountV3Bars();
        var (v3X, v3W) = await ReadV3BarGeometry(taskId);

        AssertClose(expectedX, v2X, PxTolerance, $"{taskId} v2.X");
        AssertClose(expectedX, v3X, PxTolerance, $"{taskId} v3.X");
        AssertClose(expectedWidth, v2W, PxTolerance, $"{taskId} v2.Width");
        AssertClose(expectedWidth, v3W, PxTolerance, $"{taskId} v3.Width");
        AssertClose(v2X, v3X, PxTolerance, $"{taskId} v2 vs v3 X");
        AssertClose(v2W, v3W, PxTolerance, $"{taskId} v2 vs v3 Width");
    }

    // ── Milestone geometry (diamond bounding box strict; label position pinned-not-asserted) ──

    [Fact]
    public async Task Milestone_diamond_bounding_box_matches_between_v2_and_v3()
    {
        var (expectedX, expectedWidth) = GanttDayModeMath.BarGeometry(
            Origin, new DateTime(2026, 3, 8), new DateTime(2026, 3, 8), isMilestone: true);

        await GotoHost("/e2e/gantt-v2");
        await WaitAndCountV2Bars();
        var v2Group = Page.Locator("[data-testid='gantt-v2-root'] g.lumeo-gantt-bar-wrapper[data-task-id='fe-ms']");
        var polygonPoints = await v2Group.Locator("polygon").GetAttributeAsync("points");
        Assert.NotNull(polygonPoints);
        var xs = polygonPoints!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => double.Parse(p.Split(',')[0], CultureInfo.InvariantCulture)).ToArray();
        var v2X = xs.Min();
        var v2Width = xs.Max() - xs.Min();
        // Read v2's label WHILE STILL ON THE V2 PAGE: v2Group is a lazy Locator
        // that re-resolves against whatever page is CURRENT when awaited, not
        // the page it was captured against — reading it after GotoHost("v3")
        // below would silently re-resolve it against the v3 DOM (no match,
        // 30s timeout) instead of erroring immediately.
        var v2Label = await v2Group.Locator("text.lumeo-gantt-bar-label").TextContentAsync();

        await GotoHost("/e2e/gantt-v3");
        await WaitAndCountV3Bars();
        var v3Div = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id='fe-ms'][data-milestone='true']");
        var (v3X, v3Width) = ParseBarStyle(await v3Div.GetAttributeAsync("style"));
        var v3Label = await Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-milestone-label").TextContentAsync();

        AssertClose(expectedX, v2X, PxTolerance, "milestone v2.X");
        AssertClose(expectedX, v3X, PxTolerance, "milestone v3.X");
        AssertClose(expectedWidth, v2Width, PxTolerance, "milestone v2.Width");
        AssertClose(expectedWidth, v3Width, PxTolerance, "milestone v3.Width");

        // Both render the milestone's name somewhere — the EXACT x offset is the
        // pinned intentional delta (see class remarks), not asserted here.
        Assert.Equal("Design Sign-off", v2Label);
        Assert.Equal("Design Sign-off", v3Label);
    }

    // ── Per-task custom colours ───────────────────────────────────────────────

    [Theory]
    [InlineData("fe3", "#f59e0b")]
    [InlineData("be1", "#22c55e")]
    public async Task Custom_bar_color_matches_between_v2_and_v3(string taskId, string expectedColor)
    {
        await GotoHost("/e2e/gantt-v2");
        await WaitAndCountV2Bars();
        var v2Fill = await Page.Locator($"[data-testid='gantt-v2-root'] g.lumeo-gantt-bar-wrapper[data-task-id='{taskId}'] rect.lumeo-gantt-bar-bg")
            .GetAttributeAsync("fill");

        await GotoHost("/e2e/gantt-v3");
        await WaitAndCountV3Bars();
        var v3Style = await Page.Locator($"[data-testid='gantt-v3-root'] [data-task-id='{taskId}'] .lumeo-gantt-v3-bar-bg").GetAttributeAsync("style");

        Assert.Equal(expectedColor, v2Fill);
        Assert.Contains($"background-color:{expectedColor}", v3Style);
    }

    // ── Progress fill (representation differs: v2 = px width, v3 = CSS %; ratio must agree) ──

    [Theory]
    [InlineData("fe1", 100)]
    [InlineData("fe2", 50)]
    [InlineData("fe3", 0)]
    public async Task Progress_fill_ratio_matches_task_progress(string taskId, int expectedProgress)
    {
        await GotoHost("/e2e/gantt-v2");
        await WaitAndCountV2Bars();
        var group = Page.Locator($"[data-testid='gantt-v2-root'] g.lumeo-gantt-bar-wrapper[data-task-id='{taskId}']");
        var bgWidth = double.Parse((await group.Locator("rect.lumeo-gantt-bar-bg").GetAttributeAsync("width"))!, CultureInfo.InvariantCulture);
        var progressWidth = double.Parse((await group.Locator("rect.lumeo-gantt-bar-progress").GetAttributeAsync("width"))!, CultureInfo.InvariantCulture);
        var v2Ratio = bgWidth == 0 ? 0 : (progressWidth / bgWidth) * 100.0;

        await GotoHost("/e2e/gantt-v3");
        await WaitAndCountV3Bars();
        var v3Style = await Page.Locator($"[data-testid='gantt-v3-root'] [data-task-id='{taskId}'] .lumeo-gantt-v3-bar-progress").GetAttributeAsync("style");
        var v3Match = Regex.Match(v3Style ?? "", @"width:(\d+(?:\.\d+)?)%");
        Assert.True(v3Match.Success, $"v3 progress style missing width%: {v3Style}");
        var v3Ratio = double.Parse(v3Match.Groups[1].Value, CultureInfo.InvariantCulture);

        AssertClose(expectedProgress, v2Ratio, 1.5, $"{taskId} v2 progress ratio");
        Assert.Equal(expectedProgress, v3Ratio, 0.01);
    }

    // ── Dependency arrows ─────────────────────────────────────────────────────

    [Fact]
    public async Task Arrow_count_is_three_for_both_routes()
    {
        await GotoHost("/e2e/gantt-v2");
        await WaitAndCountV2Bars();
        var v2Arrows = await Page.Locator("[data-testid='gantt-v2-root'] svg.lumeo-gantt-svg > path").CountAsync();

        await GotoHost("/e2e/gantt-v3");
        await WaitAndCountV3Bars();
        var v3Arrows = await Page.Locator("[data-testid='gantt-v3-root'] path.lumeo-gantt-v3-arrow").CountAsync();

        Assert.Equal(3, v2Arrows);
        Assert.Equal(3, v3Arrows);
    }

    [Theory]
    [InlineData("fe1", "fe2")] // forward, adjacent row
    [InlineData("fe1", "fe5")] // forward, distant row (substitutes for an impossible literal "same row" — see class/fixture remarks)
    [InlineData("be1", "be2")] // backward/target-left (be2 starts before be1 ends)
    public async Task Arrow_endpoints_connect_the_right_bars_within_tolerance(string fromId, string toId)
    {
        var fromRow = RowIndex[fromId];
        var toRow = RowIndex[toId];
        var (fromX, fromW) = ExpectedGeometry(fromId);
        var (toX, toW) = ExpectedGeometry(toId);
        var expectedPoints = GanttDayModeMath.ArrowPath((fromX, fromW, fromRow), (toX, toW, toRow));

        await GotoHost("/e2e/gantt-v2");
        await WaitAndCountV2Bars();
        // v2 has no data-arrow-from/to attributes, so the matching arrow is
        // located by its geometry (both endpoints — see FindMatchingV2Arrow's
        // remarks for why start alone can be ambiguous).
        var v2Points = await FindMatchingV2Arrow(expectedPoints[0], expectedPoints[^1]);

        await GotoHost("/e2e/gantt-v3");
        await WaitAndCountV3Bars();
        var v3D = await Page.Locator($"[data-testid='gantt-v3-root'] path.lumeo-gantt-v3-arrow[data-arrow-from='{fromId}'][data-arrow-to='{toId}']").GetAttributeAsync("d");
        Assert.NotNull(v3D);
        var v3Points = GanttDayModeMath.ParsePathD(v3D!);

        for (var i = 0; i < expectedPoints.Length; i++)
        {
            AssertClose(expectedPoints[i].X, v2Points[i].X, PxTolerance, $"{fromId}->{toId} point[{i}].X v2");
            AssertClose(expectedPoints[i].Y, v2Points[i].Y, PxTolerance, $"{fromId}->{toId} point[{i}].Y v2");
            AssertClose(expectedPoints[i].X, v3Points[i].X, PxTolerance, $"{fromId}->{toId} point[{i}].X v3");
            AssertClose(expectedPoints[i].Y, v3Points[i].Y, PxTolerance, $"{fromId}->{toId} point[{i}].Y v3");
        }
    }

    // ── Header label runs, all 6 view modes ──────────────────────────────────

    [Theory]
    [InlineData("QuarterDay")]
    [InlineData("HalfDay")]
    [InlineData("Day")]
    [InlineData("Week")]
    [InlineData("Month")]
    [InlineData("Year")]
    public async Task Header_label_runs_match_between_v2_and_v3(string viewMode)
    {
        await GotoHost($"/e2e/gantt-v2?viewMode={viewMode}");
        await WaitAndCountV2Bars();
        var v2Upper = await ReadV2HeaderTexts(isUpperRow: true);
        var v2Lower = await ReadV2HeaderTexts(isUpperRow: false);

        await GotoHost($"/e2e/gantt-v3?viewMode={viewMode}");
        await WaitAndCountV3Bars();
        var v3Upper = await ReadV3UpperRunTexts();
        var v3Lower = await ReadV3LowerLabelTexts();

        Assert.Equal(v2Upper, v3Upper);
        Assert.Equal(v2Lower, v3Lower);
    }

    // ── Zoom switcher (interactive, Day/Week/Month/Year — the 4 toolbar buttons) ──

    [Fact]
    public async Task Toolbar_view_mode_switch_recomputes_header_identically()
    {
        await GotoHost("/e2e/gantt-v2");
        await WaitAndCountV2Bars();
        var v2LowerCellLocator = Page.Locator("[data-testid='gantt-v2-root'] svg.lumeo-gantt-svg > text[y='38']");
        var v2InitialCount = await v2LowerCellLocator.CountAsync();
        await Page.Locator("[data-testid='gantt-v2-root'] button", new() { HasTextString = "Week" }).ClickAsync();
        // Week's 140px/7-day columns render FAR fewer header cells than Day's
        // 38px/1-day columns for the same fixture range — wait for the count to
        // actually change (both v2's synchronous JS re-render and v3's Blazor
        // SERVER round-trip) rather than a blind delay, which previously raced
        // ReadV3LowerLabelTexts against a STALE (larger, still-Day-mode) DOM.
        await Assertions.Expect(v2LowerCellLocator).Not.ToHaveCountAsync(v2InitialCount, new() { Timeout = 10000 });
        var v2Lower = await ReadV2HeaderTexts(isUpperRow: false);

        await GotoHost("/e2e/gantt-v3");
        await WaitAndCountV3Bars();
        var v3LowerCellLocator = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-header > div:nth-child(2) > div");
        var v3InitialCount = await v3LowerCellLocator.CountAsync();
        await Page.Locator("[data-testid='gantt-v3-root'] button", new() { HasTextString = "Week" }).ClickAsync();
        await Assertions.Expect(v3LowerCellLocator).Not.ToHaveCountAsync(v3InitialCount, new() { Timeout = 10000 });
        var v3Lower = await ReadV3LowerLabelTexts();

        Assert.Equal(v2Lower, v3Lower);
        // Week's weekRange format ("d/M") looks nothing like Day's zero-padded
        // day number — a cheap sanity check that the switch actually happened.
        Assert.DoesNotContain(v2Lower, l => l.Length == 2 && l.All(char.IsDigit) && int.Parse(l) > 12);
    }

    // ── Today marker ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Today_marker_renders_at_expected_x_on_both_routes()
    {
        var today = DateTime.Today;
        var origin = GanttDayModeMath.Origin(today.AddDays(-10)); // today-1.Start
        var expectedX = GanttDayModeMath.DateToX(origin, today);

        await GotoHost("/e2e/gantt-v2?fixture=today");
        await WaitAndCountV2Bars(expectedCountAtLeast: 1);
        // State=Attached, not the default Visible: a vertical SVG <line> (x1==x2)
        // has a ZERO-WIDTH bounding box, which Playwright's actionability check
        // treats as "not visible" even though it renders fine — same reasoning
        // as WaitAndCountV2Bars/V3Bars below.
        var v2Line = Page.Locator("[data-testid='gantt-v2-root'] line.lumeo-gantt-today-line");
        await v2Line.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });
        var v2X = double.Parse((await v2Line.GetAttributeAsync("x1"))!, CultureInfo.InvariantCulture);

        await GotoHost("/e2e/gantt-v3?fixture=today");
        await WaitAndCountV3Bars(expectedCountAtLeast: 1);
        var v3Line = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-today-line");
        await v3Line.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });
        var v3Style = await v3Line.GetAttributeAsync("style");
        var v3Match = Regex.Match(v3Style ?? "", @"left:(-?\d+(?:\.\d+)?)px");
        Assert.True(v3Match.Success);
        var v3X = double.Parse(v3Match.Groups[1].Value, CultureInfo.InvariantCulture);

        AssertClose(expectedX, v2X, PxTolerance, "today marker v2.X");
        AssertClose(expectedX, v3X, PxTolerance, "today marker v3.X");
    }

    // ── Initial viewport (P1, Codex review wave) ─────────────────────────────
    //
    // Regression: Gantt3.ComputeInitialRange pads ~60 Day-mode columns before
    // the earliest task, and nothing ever moved scrollLeft off its default 0 —
    // the committed v3 Day visual baseline itself showed an empty grid on first
    // paint. Uses the today-anchored fixture (not the shared, date-fixed one):
    // v2's own init-time scroll centers on DateTime.Today (gantt-v2.js's
    // tryScroll), and the shared fixture's fixed 2026-03 dates are nowhere near
    // whatever today happens to be when this runs — a correct implementation of
    // "center on today" would legitimately show an empty viewport for THAT
    // fixture, which would make this assertion meaningless. The today fixture's
    // tasks straddle DateTime.Today by construction, so centering on today is
    // guaranteed to bring one into view on both routes.

    [Fact]
    public async Task Initial_viewport_shows_at_least_one_bar_on_both_routes()
    {
        await GotoHost("/e2e/gantt-v2?fixture=today");
        await WaitAndCountV2Bars(expectedCountAtLeast: 1);
        var v2Bar = Page.Locator("[data-testid='gantt-v2-root'] g.lumeo-gantt-bar-wrapper").First;
        await Assertions.Expect(v2Bar).ToBeInViewportAsync(new() { Timeout = 15000 });

        await GotoHost("/e2e/gantt-v3?fixture=today");
        await WaitAndCountV3Bars(expectedCountAtLeast: 1);
        var v3Bar = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id]").First;
        await Assertions.Expect(v3Bar).ToBeInViewportAsync(new() { Timeout = 15000 });
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static (double X, double Width) ExpectedGeometry(string taskId)
    {
        var (start, end, isMilestone) = taskId switch
        {
            "fe1" => (new DateTime(2026, 2, 23), new DateTime(2026, 3, 1), false),
            "fe2" => (new DateTime(2026, 3, 1), new DateTime(2026, 3, 8), false),
            "fe5" => (new DateTime(2026, 3, 22), new DateTime(2026, 3, 29), false),
            "be1" => (new DateTime(2026, 3, 1), new DateTime(2026, 3, 10), false),
            "be2" => (new DateTime(2026, 3, 5), new DateTime(2026, 3, 18), false),
            _ => throw new ArgumentOutOfRangeException(nameof(taskId)),
        };
        return GanttDayModeMath.BarGeometry(Origin, start, end, isMilestone);
    }

    private async Task<(double X, double Y)[]> FindMatchingV2Arrow((double X, double Y) expectedStart, (double X, double Y) expectedEnd)
    {
        // Match on BOTH endpoints, not just the start: a task with multiple
        // dependents (fe1 -> fe2 AND fe1 -> fe5 in this fixture) produces
        // several arrows sharing the identical start point (fe1's own right
        // edge) — matching on start alone non-deterministically returned
        // whichever same-start arrow the DOM happened to list first.
        var paths = Page.Locator("[data-testid='gantt-v2-root'] svg.lumeo-gantt-svg > path");
        var count = await paths.CountAsync();
        for (var i = 0; i < count; i++)
        {
            var d = await paths.Nth(i).GetAttributeAsync("d");
            if (d is null) continue;
            var points = GanttDayModeMath.ParsePathD(d);
            var lastPoint = points[^1];
            if (Math.Abs(points[0].X - expectedStart.X) <= PxTolerance && Math.Abs(points[0].Y - expectedStart.Y) <= PxTolerance
                && Math.Abs(lastPoint.X - expectedEnd.X) <= PxTolerance && Math.Abs(lastPoint.Y - expectedEnd.Y) <= PxTolerance)
                return points;
        }
        throw new InvalidOperationException($"No v2 arrow found from {expectedStart} to {expectedEnd}");
    }

    private async Task<int> WaitAndCountV2Bars(int expectedCountAtLeast = 1)
    {
        var bars = Page.Locator("[data-testid='gantt-v2-root'] g.lumeo-gantt-bar-wrapper");
        await bars.First.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });
        return await bars.CountAsync();
    }

    private async Task<int> WaitAndCountV3Bars(int expectedCountAtLeast = 1)
    {
        var bars = Page.Locator("[data-testid='gantt-v3-root'] [data-task-id]");
        await bars.First.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });
        return await bars.CountAsync();
    }

    private async Task<(double X, double Width)> ReadV2BarGeometry(string taskId)
    {
        var rect = Page.Locator($"[data-testid='gantt-v2-root'] g.lumeo-gantt-bar-wrapper[data-task-id='{taskId}'] rect.lumeo-gantt-bar-bg");
        var x = double.Parse((await rect.GetAttributeAsync("x"))!, CultureInfo.InvariantCulture);
        var w = double.Parse((await rect.GetAttributeAsync("width"))!, CultureInfo.InvariantCulture);
        return (x, w);
    }

    private async Task<(double X, double Width)> ReadV3BarGeometry(string taskId)
    {
        var div = Page.Locator($"[data-testid='gantt-v3-root'] [data-task-id='{taskId}']");
        var style = await div.GetAttributeAsync("style");
        return ParseBarStyle(style);
    }

    private static (double X, double Width) ParseBarStyle(string? style)
    {
        Assert.NotNull(style);
        var x = Regex.Match(style!, @"--lumeo-gantt-bar-x:(-?\d+(?:\.\d+)?)px");
        var w = Regex.Match(style!, @"--lumeo-gantt-bar-w:(-?\d+(?:\.\d+)?)px");
        Assert.True(x.Success && w.Success, $"style missing bar-x/bar-w custom properties: {style}");
        return (double.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(w.Groups[1].Value, CultureInfo.InvariantCulture));
    }

    private async Task<List<string>> ReadV2HeaderTexts(bool isUpperRow)
    {
        // Both header rows AND group-header labels are top-level <text> children
        // of the SVG (bar/milestone labels live inside a <g> instead — see class
        // remarks) — the upper row's text sits at y=18 (gantt-v2.js line 395);
        // everything else at y=HEADER_HEIGHT-18=38 is the lower row (group
        // headers use their own distinct y and are excluded by this y filter).
        var texts = Page.Locator("[data-testid='gantt-v2-root'] svg.lumeo-gantt-svg > text");
        var count = await texts.CountAsync();
        var result = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var t = texts.Nth(i);
            var y = await t.GetAttributeAsync("y");
            // Group-header labels (gantt-v2.js: ghText, y = HEADER_HEIGHT + rowSlot*ROW_HEIGHT
            // — always >= HEADER_HEIGHT=56) are ALSO top-level <text> children of the SVG, so
            // "not the upper row" alone wrongly swept them into the lower-row set. The lower
            // row's own y is the single fixed value HEADER_HEIGHT-18=38 (gantt-v2.js line 377)
            // — require it exactly instead of "anything other than upper".
            var isUpper = y == "18";
            var isLower = y == "38";
            if (isUpperRow ? !isUpper : !isLower) continue;
            result.Add((await t.TextContentAsync()) ?? "");
        }
        return result;
    }

    private async Task<List<string>> ReadV3UpperRunTexts()
    {
        var locator = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-header > div:nth-child(1) > div");
        var count = await locator.CountAsync();
        var result = new List<string>(count);
        for (var i = 0; i < count; i++) result.Add((await locator.Nth(i).TextContentAsync()) ?? "");
        return result;
    }

    private async Task<List<string>> ReadV3LowerLabelTexts()
    {
        var locator = Page.Locator("[data-testid='gantt-v3-root'] .lumeo-gantt-v3-header > div:nth-child(2) > div");
        var count = await locator.CountAsync();
        var result = new List<string>(count);
        for (var i = 0; i < count; i++) result.Add((await locator.Nth(i).TextContentAsync()) ?? "");
        return result;
    }

    private static void AssertClose(double expected, double actual, double tolerance, string what)
    {
        Assert.True(Math.Abs(expected - actual) <= tolerance,
            $"{what}: expected {expected}, got {actual} (tolerance {tolerance})");
    }
}
