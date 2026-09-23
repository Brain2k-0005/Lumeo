using System.Globalization;
using System.Text;
using Lumeo.Tests.E2E.Gantt;
using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Flow;

/// <summary>
/// Real-browser coverage for Lumeo.Flow phase 3a against the Server host's /e2e/flow fixture:
/// auto-layout (the "Apply layout" button calls FlowLayout.Tree), undo after a drag (FlowHistory +
/// FlowControls' undo button), and reconnecting an existing edge's end.
///
/// Same host/base URL/sequential collection as <see cref="FlowCanvasTests"/> — see that class' XML
/// doc for how to run the ServerHost locally.
/// </summary>
public class FlowCanvasPhase3Tests : GanttParityTestBase
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

    private async Task<string?> BoundEdgeTargetAsync(string edgeId)
    {
        var sink = await SinkAsync("flow-edges-sink");
        foreach (var part in sink.Split(';'))
        {
            var kv = part.Split(':');
            if (kv[0] != edgeId) continue;
            return kv[1].Split('-')[1];
        }
        return null;
    }

    private async Task<string> DumpAsync(params string[] nodeIds)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("--- flow diagnostics (phase 3a) ---");
        sb.AppendLine("nodes sink: " + await SinkAsync("flow-nodes-sink"));
        sb.AppendLine("edges sink: " + await SinkAsync("flow-edges-sink"));
        sb.AppendLine("commit count: " + await SinkAsync("flow-commit-count"));
        sb.AppendLine("history count: " + await SinkAsync("flow-history-count"));
        foreach (var id in nodeIds)
        {
            var box = await NodeEl(id).BoundingBoxAsync();
            sb.AppendLine($"{id} box: " + (box is { } b ? $"X={b.X:F2} Y={b.Y:F2} W={b.Width:F2} H={b.Height:F2}" : "null"));
        }
        sb.AppendLine("journal: " + await Page.EvaluateAsync<string>("() => JSON.stringify(window.__lumeoFlowDiag || [])"));
        return sb.ToString();
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

    [Fact]
    public async Task Clicking_The_Layout_Button_Rearranges_The_Nodes()
    {
        await OpenAsync();
        var before = await BoundNodeAsync("n2");

        await Page.Locator("[data-testid='flow-layout-btn']").ClickAsync();
        // FlowLayout.Tree runs synchronously in .NET; wait for the bound sink to report an X other
        // than n2's known starting value (320) — deterministic, not timing-dependent.
        try
        {
            await Page.WaitForFunctionAsync(
                "() => { const t = document.querySelector(\"[data-testid='flow-nodes-sink']\").textContent; const m = t.match(/n2:([-\\d.]+),/); return m && m[1] !== '320'; }",
                new PageWaitForFunctionOptions { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            Assert.Fail("expected the layout button to move n2" + await DumpAsync("n2"));
        }

        var after = await BoundNodeAsync("n2");
        Assert.True(Math.Abs(after.X - before.X) > 1 || Math.Abs(after.Y - before.Y) > 1,
            $"expected n2 to move from ({before.X}, {before.Y}), stayed at ({after.X}, {after.Y})" + await DumpAsync("n2"));
    }

    [Fact]
    public async Task Undo_After_A_Drag_Restores_The_Position()
    {
        await OpenAsync();
        var before = await BoundNodeAsync("n2");
        var box = (await NodeEl("n2").BoundingBoxAsync())!;

        await DragAsync(box.X + box.Width / 2, box.Y + box.Height / 2, 96, 48);
        await Assertions.Expect(Page.Locator("[data-testid='flow-commit-count']")).ToHaveTextAsync("1", new() { Timeout = 10000 });
        var afterDrag = await BoundNodeAsync("n2");
        Assert.True(Math.Abs(afterDrag.X - before.X) > 1 || Math.Abs(afterDrag.Y - before.Y) > 1, "setup invariant: the drag must have moved n2" + await DumpAsync("n2"));

        await Page.Locator("[data-testid='flow-root'] [aria-label='Undo']").ClickAsync();

        try
        {
            await Page.WaitForFunctionAsync(
                "xy => { const t = document.querySelector(\"[data-testid='flow-nodes-sink']\").textContent; const m = t.match(/n2:([-\\d.]+),([-\\d.]+)/); return m && Math.abs(+m[1] - xy[0]) < 1 && Math.abs(+m[2] - xy[1]) < 1; }",
                new[] { before.X, before.Y },
                new PageWaitForFunctionOptions { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            Assert.Fail($"expected undo to restore n2 to ({before.X}, {before.Y})" + await DumpAsync("n2"));
        }
    }

    [Fact]
    public async Task Reconnecting_An_Edge_Moves_Its_End()
    {
        await OpenAsync();
        var targetBefore = await BoundEdgeTargetAsync("e1"); // n1 -> n2
        Assert.Equal("n2", targetBefore);

        // Select the edge (a real on-path click, its bounding-box centre can miss a curved stroke).
        var point = await Page.EvaluateAsync<double[]>(@"() => {
            const el = document.querySelector(""[data-testid='flow-root'] [data-edge-id='e1']"");
            const len = el.getTotalLength();
            const p = el.getPointAtLength(len / 2);
            const pt = el.ownerSVGElement.createSVGPoint();
            pt.x = p.x; pt.y = p.y;
            const s = pt.matrixTransform(el.getScreenCTM());
            return [s.x, s.y];
        }");
        await Page.Mouse.ClickAsync((float)point[0], (float)point[1]);

        var endHandle = Page.Locator("[data-testid='flow-root'] [data-flow-edge-end][data-edge-id='e1'][data-end='target']");
        await Assertions.Expect(endHandle).ToBeVisibleAsync(new() { Timeout = 10000 });
        var endBox = (await endHandle.BoundingBoxAsync())!;

        var n3TargetHandle = Page.Locator("[data-testid='flow-root'] [data-flow-node='n3'] [data-flow-handle][data-handle-type='target']");
        var n3Box = (await n3TargetHandle.BoundingBoxAsync())!;

        await DragAsync(endBox.X + endBox.Width / 2, endBox.Y + endBox.Height / 2,
            n3Box.X + n3Box.Width / 2 - endBox.X - endBox.Width / 2, n3Box.Y + n3Box.Height / 2 - endBox.Y - endBox.Height / 2, steps: 10);

        try
        {
            await Page.WaitForFunctionAsync(
                "() => (document.querySelector(\"[data-testid='flow-edges-sink']\").textContent || '').includes('e1:n1-n3')",
                new PageWaitForFunctionOptions { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            Assert.Fail("expected e1's target to become n3" + await DumpAsync());
        }
    }
}
