using System.Globalization;
using System.Text;
using Lumeo.Tests.E2E.Gantt;
using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Flow;

/// <summary>
/// Real-browser coverage for FlowCanvas (Lumeo.Flow phase 1) against the Server host's /e2e/flow
/// fixture (tests/Lumeo.Tests.ServerHost/Components/Pages/E2E/FlowPage.razor): a node drag commits
/// its position through a real SignalR round trip, a plain wheel zooms around the pointer, a
/// background drag pans, and the arrow keys move a focused node.
///
/// Same host, base URL and sequential collection as the Gantt suite (GanttParityTestBase — it is
/// the shared ServerHost base, not Gantt-specific in behaviour). The engine's diagnostics journal
/// (window.__lumeoFlowDiag) is switched on for every test; a failing assertion appends it, plus the
/// sinks and the live DOM geometry, to the failure message.
///
/// Running locally:
/// <code>
/// ASPNETCORE_ENVIRONMENT=Development dotnet run --project tests/Lumeo.Tests.ServerHost --urls http://localhost:5299
/// dotnet test tests/Lumeo.Tests.E2E --filter "FullyQualifiedName~Flow"
/// </code>
/// </summary>
public class FlowCanvasTests : GanttParityTestBase
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Page.SetViewportSizeAsync(1280, 800);
        await Page.AddInitScriptAsync("window.__lumeoFlowDiag = true;");
    }

    private ILocator Root => Page.Locator("[data-testid='flow-root']");
    private ILocator NodeEl(string id) => Page.Locator($"[data-testid='flow-root'] [data-flow-node='{id}']");

    private async Task OpenAsync(string query = "")
    {
        await GotoHost("/e2e/flow" + query);
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-flow-ready", "done", new() { Timeout = 20000 });
    }

    private Task<string> SinkAsync(string id) => Page.Locator($"[data-testid='{id}']").TextContentAsync().ContinueWith(t => t.Result ?? "");

    private async Task<(double X, double Y)> BoundNodeAsync(string id)
    {
        var sink = await SinkAsync("flow-nodes-sink");
        foreach (var part in sink.Split(';'))
        {
            var kv = part.Split(':');
            if (kv[0] != id) continue;
            var xy = kv[1].Split(',');
            return (double.Parse(xy[0], CultureInfo.InvariantCulture), double.Parse(xy[1], CultureInfo.InvariantCulture));
        }
        throw new InvalidOperationException($"node {id} not in sink '{sink}'");
    }

    private async Task<(double X, double Y, double Zoom)> BoundViewportAsync()
    {
        var parts = (await SinkAsync("flow-viewport-sink")).Split('|');
        return (double.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture), double.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    private Task<int> JournalCountAsync(string ev)
        => Page.EvaluateAsync<int>("ev => (window.__lumeoFlowDiag || []).filter(e => e.ev === ev).length", ev);

    // The failure-only dump: the engine journal, every sink, and the live geometry — enough to see
    // which write produced a wrong position without re-running.
    private async Task<string> DumpAsync(params string[] nodeIds)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("--- flow diagnostics ---");
        sb.AppendLine("nodes sink: " + await SinkAsync("flow-nodes-sink"));
        sb.AppendLine("viewport sink: " + await SinkAsync("flow-viewport-sink"));
        sb.AppendLine("commit count: " + await SinkAsync("flow-commit-count"));
        sb.AppendLine("layer transform: " + await Page.Locator("[data-slot='flow-viewport']").EvaluateAsync<string>("el => el.style.transform"));
        sb.AppendLine("pane box: " + Box(await Page.Locator("[data-slot='flow-pane']").BoundingBoxAsync()));
        foreach (var id in nodeIds)
        {
            sb.AppendLine($"{id} box: {Box(await NodeEl(id).BoundingBoxAsync())} transform: {await NodeEl(id).EvaluateAsync<string>("el => el.style.transform")}");
        }
        sb.AppendLine("journal: " + await Page.EvaluateAsync<string>("() => JSON.stringify(window.__lumeoFlowDiag || [])"));
        return sb.ToString();
    }

    private static string Box(LocatorBoundingBoxResult? b) =>
        b is { } x ? $"X={x.X:F2} Y={x.Y:F2} W={x.Width:F2} H={x.Height:F2}" : "null";

    private async Task CheckAsync(bool condition, string message, params string[] nodeIds)
    {
        if (condition) return;
        Assert.Fail(message + await DumpAsync(nodeIds));
    }

    private async Task WaitOrDumpAsync(string expression, object? arg, string message, params string[] nodeIds)
    {
        try
        {
            await Page.WaitForFunctionAsync(expression, arg, new() { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            Assert.Fail(message + await DumpAsync(nodeIds));
        }
    }

    private async Task DragAsync(double fromX, double fromY, double dx, double dy, int steps = 12)
    {
        await Page.Mouse.MoveAsync((float)fromX, (float)fromY);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= steps; i++)
        {
            await Page.Mouse.MoveAsync((float)(fromX + dx * i / steps), (float)(fromY + dy * i / steps));
        }
        await Page.Mouse.UpAsync();
    }

    private async Task MarqueeAsync(double fromX, double fromY, double toX, double toY, int steps = 8)
    {
        await Page.Keyboard.DownAsync("Shift");
        await Page.Mouse.MoveAsync((float)fromX, (float)fromY);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= steps; i++)
        {
            await Page.Mouse.MoveAsync((float)(fromX + (toX - fromX) * i / steps), (float)(fromY + (toY - fromY) * i / steps));
        }
        await Page.Mouse.UpAsync();
        await Page.Keyboard.UpAsync("Shift");
    }

    private ILocator SourceHandle(string nodeId) => Page.Locator($"[data-testid='flow-root'] [data-flow-node='{nodeId}'] [data-flow-handle][data-handle-type='source']");
    private ILocator TargetHandle(string nodeId) => Page.Locator($"[data-testid='flow-root'] [data-flow-node='{nodeId}'] [data-flow-handle][data-handle-type='target']");

    private async Task ConnectAsync(string sourceNodeId, string targetNodeId)
    {
        var s = (await SourceHandle(sourceNodeId).BoundingBoxAsync())!;
        var t = (await TargetHandle(targetNodeId).BoundingBoxAsync())!;
        await DragAsync(s.X + s.Width / 2, s.Y + s.Height / 2, t.X + t.Width / 2 - s.X - s.Width / 2, t.Y + t.Height / 2 - s.Y - s.Height / 2, steps: 10);
    }

    private Task<string> EdgesSinkAsync() => SinkAsync("flow-edges-sink");
    private Task<string> SelectionSinkAsync() => SinkAsync("flow-selection-sink");

    // A real, on-path click on an SVG edge (its bounding-box CENTER can miss a curved stroke) —
    // getPointAtLength + getScreenCTM guarantees the click lands on the rendered path.
    private async Task ClickEdgeAsync(string edgeId)
    {
        var point = await Page.EvaluateAsync<double[]>(@"id => {
            const el = document.querySelector(`[data-testid='flow-root'] [data-edge-id='${id}']`);
            const len = el.getTotalLength();
            const p = el.getPointAtLength(len / 2);
            const pt = el.ownerSVGElement.createSVGPoint();
            pt.x = p.x; pt.y = p.y;
            const screenPt = pt.matrixTransform(el.getScreenCTM());
            return [screenPt.x, screenPt.y];
        }", edgeId);
        await Page.Mouse.ClickAsync((float)point[0], (float)point[1]);
    }

    [Fact]
    public async Task Dragging_A_Node_Commits_Its_Position_Once()
    {
        await OpenAsync();
        var before = await BoundNodeAsync("n2");
        var zoom = (await BoundViewportAsync()).Zoom;
        var box = (await NodeEl("n2").BoundingBoxAsync())!;

        const double dx = 144, dy = 72;
        await DragAsync(box.X + box.Width / 2, box.Y + box.Height / 2, dx, dy);

        await Assertions.Expect(Page.Locator("[data-testid='flow-commit-count']")).ToHaveTextAsync("1", new() { Timeout = 10000 });
        var after = await BoundNodeAsync("n2");
        await CheckAsync(Math.Abs(after.X - (before.X + dx / zoom)) <= 1 && Math.Abs(after.Y - (before.Y + dy / zoom)) <= 1,
            $"expected n2 committed at ({before.X + dx / zoom:F2}, {before.Y + dy / zoom:F2}), got ({after.X:F2}, {after.Y:F2})", "n2");

        // Exactly one commit crossed the wire, and it was accepted.
        await CheckAsync(await JournalCountAsync("commit") == 1, "expected exactly one CommitNodeDrag", "n2");
        await CheckAsync(await JournalCountAsync("drag-start") == 1, "expected exactly one drag-start", "n2");

        // The node stays where it was dropped once the render lands (no snap-back, no double move).
        var boxAfter = (await NodeEl("n2").BoundingBoxAsync())!;
        await CheckAsync(Math.Abs(boxAfter.X - (box.X + dx)) <= 1.5 && Math.Abs(boxAfter.Y - (box.Y + dy)) <= 1.5,
            $"expected n2's box at ({box.X + dx:F1}, {box.Y + dy:F1}) after the drop, got ({boxAfter.X:F1}, {boxAfter.Y:F1})", "n2");
    }

    [Fact]
    public async Task Wheel_Zoom_Keeps_The_Point_Under_The_Pointer_On_A_Node_Far_From_The_Centre()
    {
        await OpenAsync();
        var pane = (await Page.Locator("[data-slot='flow-pane']").BoundingBoxAsync())!;
        var target = NodeEl("n6"); // bottom-right of the graph: far from the pane centre
        var before = (await target.BoundingBoxAsync())!;
        var px = before.X + before.Width / 2;
        var py = before.Y + before.Height / 2;
        var paneCx = pane.X + pane.Width / 2;
        var paneCy = pane.Y + pane.Height / 2;
        var offCentre = Math.Sqrt((px - paneCx) * (px - paneCx) + (py - paneCy) * (py - paneCy));
        Assert.True(offCentre > 250, $"setup invariant: the pointer must be far from the pane centre to tell a pointer anchor from a centre anchor (was {offCentre:F1}px)");
        var zoomBefore = (await BoundViewportAsync()).Zoom;

        await Page.Mouse.MoveAsync((float)px, (float)py);
        await Page.Mouse.WheelAsync(0, -300);

        // The gesture ends (final report sent) and the report lands in the bound viewport.
        await WaitOrDumpAsync("() => (window.__lumeoFlowDiag || []).some(e => e.ev === 'wheel-end')", null, "the wheel gesture never ended", "n6");
        await WaitOrDumpAsync("z => +document.querySelector('[data-testid=flow-viewport-sink]').textContent.split('|')[2] > z * 1.2", zoomBefore, "the bound viewport never reported the zoom", "n6");
        var zoomAfter = (await BoundViewportAsync()).Zoom;
        await CheckAsync(zoomAfter > zoomBefore * 1.2, $"expected the wheel to zoom in noticeably, zoom {zoomBefore:F3} -> {zoomAfter:F3}", "n6");

        var after = (await target.BoundingBoxAsync())!;
        var cx = after.X + after.Width / 2;
        var cy = after.Y + after.Height / 2;
        await CheckAsync(Math.Abs(cx - px) <= 2 && Math.Abs(cy - py) <= 2,
            $"expected the node centre under the pointer ({px:F1}, {py:F1}) to stay within 2px, it moved to ({cx:F1}, {cy:F1})", "n6");

        // The transform was applied in the wheel handler, before .NET heard about it.
        var ordered = await Page.EvaluateAsync<bool>(@"() => {
            const j = window.__lumeoFlowDiag || [];
            const wheel = j.findIndex(e => e.ev === 'wheel');
            const apply = j.findIndex(e => e.ev === 'viewport-apply' && e.src === 'wheel');
            const report = j.findIndex((e, i) => i > wheel && e.ev === 'report');
            return wheel >= 0 && apply > wheel && report > apply && j[apply].t <= j[report].t;
        }");
        await CheckAsync(ordered, "expected 'viewport-apply' (wheel) to precede the first 'report' after the wheel", "n6");
    }

    [Fact]
    public async Task Dragging_The_Background_Pans_The_Viewport()
    {
        await OpenAsync();
        var start = await BoundViewportAsync();
        var n1Before = (await NodeEl("n1").BoundingBoxAsync())!;

        // An empty background point: the pane itself is the hit target there.
        var point = await Page.EvaluateAsync<double[]>(@"() => {
            const pane = document.querySelector('[data-testid=flow-root] [data-slot=flow-pane]');
            const r = pane.getBoundingClientRect();
            for (let y = r.top + 30; y < r.bottom - 30; y += 20) {
                for (let x = r.left + 120; x < r.right - 30; x += 20) {
                    const el = document.elementFromPoint(x, y);
                    if (el && (el === pane || el.closest('[data-slot=flow-background]') || el.getAttribute('data-slot') === 'flow-nodes' || el.getAttribute('data-slot') === 'flow-viewport')
                        && !el.closest('[data-flow-node]') && !el.closest('[data-flow-overlay]')) return [x, y];
                }
            }
            return null;
        }");
        Assert.NotNull(point);

        const double dx = -150, dy = 80;
        await DragAsync(point[0], point[1], dx, dy);

        await WaitOrDumpAsync("() => (window.__lumeoFlowDiag || []).some(e => e.ev === 'pan-end')", null, "the pan gesture never ended", "n1");
        await WaitOrDumpAsync(
            "([x, y]) => { const p = document.querySelector('[data-testid=flow-viewport-sink]').textContent.split('|'); return Math.abs(+p[0] - x) < 0.5 && Math.abs(+p[1] - y) < 0.5; }",
            new[] { start.X + dx, start.Y + dy }, "the bound viewport never reported the pan", "n1");

        var end = await BoundViewportAsync();
        await CheckAsync(Math.Abs(end.X - (start.X + dx)) <= 0.5 && Math.Abs(end.Y - (start.Y + dy)) <= 0.5 && Math.Abs(end.Zoom - start.Zoom) < 1e-9,
            $"expected the viewport to pan by ({dx}, {dy}) from ({start.X:F2}, {start.Y:F2}), got ({end.X:F2}, {end.Y:F2}, zoom {end.Zoom:F4})", "n1");

        var n1After = (await NodeEl("n1").BoundingBoxAsync())!;
        await CheckAsync(Math.Abs(n1After.X - (n1Before.X + dx)) <= 1 && Math.Abs(n1After.Y - (n1Before.Y + dy)) <= 1,
            "expected n1 to move on screen with the pan", "n1");
        // A pan never commits node positions.
        await CheckAsync(await JournalCountAsync("commit") == 0, "a pan must not commit a node drag", "n1");
    }

    [Fact]
    public async Task Arrow_Keys_Move_The_Focused_Node()
    {
        await OpenAsync();
        var before = await BoundNodeAsync("n1");
        await NodeEl("n1").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("Shift+ArrowDown");

        await Assertions.Expect(Page.Locator("[data-testid='flow-commit-count']")).ToHaveTextAsync("4", new() { Timeout = 10000 });
        var after = await BoundNodeAsync("n1");
        await CheckAsync(after.X == before.X + 3 && after.Y == before.Y + 10,
            $"expected n1 at ({before.X + 3}, {before.Y + 10}), got ({after.X}, {after.Y})", "n1");

        // Focus stays on the node, and the page did not scroll under the arrow keys.
        var focused = await Page.EvaluateAsync<string?>("() => document.activeElement && document.activeElement.getAttribute('data-flow-node')");
        await CheckAsync(focused == "n1", $"expected focus to stay on n1, was '{focused}'", "n1");
        var scrollY = await Page.EvaluateAsync<double>("() => window.scrollY");
        await CheckAsync(scrollY == 0, $"expected the arrow keys not to scroll the page, scrollY={scrollY}", "n1");
    }

    [Fact]
    public async Task Connecting_Two_Handles_Creates_An_Edge()
    {
        await OpenAsync();
        var before = await EdgesSinkAsync();
        await CheckAsync(!before.Contains("n5-n6"), "setup invariant: n5->n6 must not already be an edge", "n5", "n6");

        await ConnectAsync("n5", "n6");

        await WaitOrDumpAsync("() => document.querySelector('[data-testid=flow-edges-sink]').textContent.includes('n5-n6')", null,
            "expected a new n5->n6 edge in the bound edge list", "n5", "n6");
        await CheckAsync(await JournalCountAsync("connect-start") == 1, "expected exactly one connect-start", "n5", "n6");
        await CheckAsync(await JournalCountAsync("connect-result") == 1, "expected exactly one connect-result", "n5", "n6");
        Assert.NotNull(await Page.QuerySelectorAsync("[data-testid='flow-root'] [data-flow-edge][data-source='n5'][data-target='n6']"));
    }

    [Fact]
    public async Task An_Invalid_Connection_Is_Rejected()
    {
        await OpenAsync();
        var before = await EdgesSinkAsync();
        var beforeCount = before.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;

        // The fixture's IsValidConnection rejects every connection that targets n1.
        await ConnectAsync("n2", "n1");

        await WaitOrDumpAsync("() => (window.__lumeoFlowDiag || []).some(e => e.ev === 'connect-result')", null,
            "expected a connect-result event even for a rejected drop", "n1", "n2");
        var accepted = await Page.EvaluateAsync<bool>("() => (window.__lumeoFlowDiag || []).filter(e => e.ev === 'connect-result').pop().accepted");
        await CheckAsync(!accepted, "expected the n2->n1 connection to be reported as rejected", "n1", "n2");
        var after = await EdgesSinkAsync();
        var afterCount = after.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;
        await CheckAsync(afterCount == beforeCount, $"expected no new edge, had {beforeCount} now {afterCount}: '{after}'", "n1", "n2");
    }

    [Fact]
    public async Task A_Marquee_Selects_Every_Enclosed_Node()
    {
        await OpenAsync();
        var n4Box = (await NodeEl("n4").BoundingBoxAsync())!;
        var n5Box = (await NodeEl("n5").BoundingBoxAsync())!;
        var minX = Math.Min(n4Box.X, n5Box.X) - 20;
        var minY = Math.Min(n4Box.Y, n5Box.Y) - 20;
        var maxX = Math.Max(n4Box.X + n4Box.Width, n5Box.X + n5Box.Width) + 20;
        var maxY = Math.Max(n4Box.Y + n4Box.Height, n5Box.Y + n5Box.Height) + 20;

        // Start from the bottom-right corner: the top-left corner sits close enough to e3's
        // (n1->n4, step-routed) turn to occasionally land the pointerdown ON its hit corridor,
        // which correctly starts an edge click instead of a marquee (same as clicking any edge
        // does) — same reasoning as React Flow's ~20px edge interaction width.
        await MarqueeAsync(maxX, maxY, minX, minY);

        await WaitOrDumpAsync("() => (window.__lumeoFlowDiag || []).some(e => e.ev === 'marquee-end')", null,
            "expected the marquee gesture to end", "n4", "n5");
        var selection = await SelectionSinkAsync();
        await CheckAsync(selection.StartsWith("n4,n5|") || selection.StartsWith("n5,n4|") || selection == "n4,n5|" || selection == "n5,n4|",
            $"expected exactly n4 and n5 selected, got '{selection}'", "n4", "n5");
        await CheckAsync(await NodeEl("n1").GetAttributeAsync("data-selected") is null, "n1 must not be selected by the marquee", "n1");
    }

    [Fact]
    public async Task Delete_Removes_The_Selected_Node()
    {
        await OpenAsync();
        await NodeEl("n5").ClickAsync();
        await Assertions.Expect(NodeEl("n5")).ToHaveAttributeAsync("data-selected", "", new() { Timeout = 5000 });

        await Page.Keyboard.PressAsync("Delete");

        await WaitOrDumpAsync("() => !document.querySelector('[data-testid=flow-nodes-sink]').textContent.includes('n5:')", null,
            "expected n5 to leave the bound node list", "n5");
        await CheckAsync(await Page.Locator("[data-testid='flow-root'] [data-flow-node='n5']").CountAsync() == 0, "expected n5's DOM element to be gone");
        var edges = await EdgesSinkAsync();
        await CheckAsync(!edges.Contains(":n4-n5") && !edges.Contains("-n5"), $"expected every edge touching n5 to be removed, got '{edges}'");
    }

    [Fact]
    public async Task Clicking_An_Edge_Selects_It()
    {
        await OpenAsync();
        await ClickEdgeAsync("e1");

        await WaitOrDumpAsync("() => document.querySelector('[data-testid=flow-selection-sink]').textContent.endsWith('|e1')", null,
            "expected e1 in the selection sink", "n1", "n2");
        await CheckAsync(await Page.Locator("[data-testid='flow-root'] [data-flow-edge][data-edge-id='e1']").GetAttributeAsync("data-selected") == "",
            "expected e1's path to carry data-selected");
    }

    [Fact]
    public async Task Clicking_The_MiniMap_Pans_The_Canvas()
    {
        await OpenAsync();
        var before = await BoundViewportAsync();
        var map = Page.Locator("[data-testid='flow-root'] [data-slot='flow-minimap'] svg");
        var box = (await map.BoundingBoxAsync())!;

        // Click a corner far from wherever the minimap's own centre currently maps to.
        await Page.Mouse.ClickAsync(box.X + 4, box.Y + 4);

        await WaitOrDumpAsync(
            "([x, y]) => { const p = document.querySelector('[data-testid=flow-viewport-sink]').textContent.split('|'); return Math.abs(+p[0] - x) > 0.5 || Math.abs(+p[1] - y) > 0.5; }",
            new[] { before.X, before.Y }, "expected the minimap click to pan the bound viewport");
        var after = await BoundViewportAsync();
        await CheckAsync(Math.Abs(after.Zoom - before.Zoom) < 1e-9, "a minimap pan must not change the zoom");
    }
}
