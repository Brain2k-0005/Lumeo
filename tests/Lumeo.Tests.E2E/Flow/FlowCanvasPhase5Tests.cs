using System.Globalization;
using System.Text;
using Lumeo.Tests.E2E.Gantt;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Lumeo.Tests.E2E.Flow;

/// <summary>
/// Real-browser coverage for Lumeo.Flow phase 5 against the Server host:
/// <list type="bullet">
/// <item>/e2e/flow-groups — dragging a group moves its children LIVE and commits only the group (the
/// children stay relative); a child with Extent=Parent is clamped inside its group; Ctrl+G wraps the
/// selection in a new group without moving anything on screen.</item>
/// <item>/e2e/flow-virtual — 2000 nodes with OnlyRenderVisibleNodes: first paint under 1.5 s
/// (performance.now() in the page, from navigation start to the first mounted node), a long pan
/// where every engine viewport report reaches .NET and the mounted window follows, and a node drag
/// on the virtualized canvas that commits normally.</item>
/// </list>
/// Same host/base URL/sequential collection as <see cref="FlowCanvasTests"/>. Every wait is on a DOM
/// condition, never a sleep; a failure appends the engine journal and the sinks.
/// </summary>
public class FlowCanvasPhase5Tests : GanttParityTestBase
{
    private readonly ITestOutputHelper _output;

    public FlowCanvasPhase5Tests(ITestOutputHelper output) => _output = output;

    // Installed before any page script: records performance.now() when the first node mounts and
    // when the engine marks the canvas ready with nodes in it.
    private const string PerfProbe = @"
        window.__lumeoFlowDiag = true;
        window.__p5 = { firstNode: null, ready: null };
        new MutationObserver(() => {
            if (window.__p5.firstNode == null && document.querySelector('[data-flow-node]')) window.__p5.firstNode = performance.now();
            const root = document.querySelector(""[data-testid='flow-root']"");
            if (window.__p5.ready == null && root && root.getAttribute('data-flow-ready') === 'done' && document.querySelector('[data-flow-node]')) window.__p5.ready = performance.now();
        }).observe(document, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-flow-ready'] });";

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Page.SetViewportSizeAsync(1280, 800);
        await Page.AddInitScriptAsync(PerfProbe);
    }

    private ILocator Root => Page.Locator("[data-testid='flow-root']");
    private ILocator NodeEl(string id) => Page.Locator($"[data-testid='flow-root'] [data-flow-node='{id}']");

    private async Task OpenAsync(string path)
    {
        await GotoHost(path);
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-flow-ready", "done", new() { Timeout = 20000 });
        await Page.WaitForFunctionAsync("() => document.querySelector(\"[data-testid='flow-root'] [data-flow-node]\") !== null", null, new() { Timeout = 20000 });
    }

    private Task<string> SinkAsync(string id) => Page.Locator($"[data-testid='{id}']").TextContentAsync().ContinueWith(t => t.Result ?? "");

    private async Task<string[]> NodeFieldsAsync(string id)
    {
        var sink = await SinkAsync("flow-nodes-sink");
        foreach (var part in sink.Split(';'))
        {
            var kv = part.Split(':');
            if (kv[0] == id) return kv[1].Split(',');
        }
        throw new InvalidOperationException($"node {id} not in sink '{sink}'");
    }

    private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    private async Task<string> DumpAsync(params string[] nodeIds)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("--- flow diagnostics (phase 5) ---");
        sb.AppendLine("nodes sink: " + await SinkAsync("flow-nodes-sink"));
        sb.AppendLine("commit count: " + await SinkAsync("flow-commit-count"));
        foreach (var id in nodeIds)
        {
            var box = await NodeEl(id).BoundingBoxAsync();
            sb.AppendLine($"{id} box: " + (box is { } b ? $"X={b.X:F2} Y={b.Y:F2} W={b.Width:F2} H={b.Height:F2}" : "null"));
        }
        sb.AppendLine("journal: " + await Page.EvaluateAsync<string>("() => JSON.stringify((window.__lumeoFlowDiag || []).slice(-40))"));
        return sb.ToString();
    }

    private async Task WaitForCommitsAsync(int count)
    {
        try
        {
            await Assertions.Expect(Page.Locator("[data-testid='flow-commit-count']")).ToHaveTextAsync(count.ToString(CultureInfo.InvariantCulture), new() { Timeout = 10000 });
        }
        catch (PlaywrightException)
        {
            Assert.Fail($"expected {count} committed drag(s)" + await DumpAsync());
        }
    }

    // ── Sub-flows ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Dragging_A_Group_Moves_Its_Children_Live_And_Commits_Only_The_Group()
    {
        await OpenAsync("/e2e/flow-groups");
        var g0 = (await NodeEl("g").BoundingBoxAsync())!;
        var a0 = (await NodeEl("a").BoundingBoxAsync())!;
        const double dx = 90, dy = 50;

        // Grab the group by its label row (above its children).
        var sx = g0.X + 60;
        var sy = g0.Y + 12;
        await Page.Mouse.MoveAsync((float)sx, (float)sy);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= 10; i++) await Page.Mouse.MoveAsync((float)(sx + dx * i / 10), (float)(sy + dy * i / 10));

        // Live, before the drop: the child has moved with its group, by the same delta.
        var aLive = (await NodeEl("a").BoundingBoxAsync())!;
        if (Math.Abs(aLive.X - a0.X - dx) > 1.5 || Math.Abs(aLive.Y - a0.Y - dy) > 1.5)
        {
            Assert.Fail($"child a did not follow its group live: from ({a0.X},{a0.Y}) to ({aLive.X},{aLive.Y})" + await DumpAsync("g", "a"));
        }
        await Page.Mouse.UpAsync();
        await WaitForCommitsAsync(1);

        var g = await NodeFieldsAsync("g");
        var a = await NodeFieldsAsync("a");
        var b = await NodeFieldsAsync("b");
        Assert.Equal((100 + dx, 60 + dy), (D(g[0]), D(g[1])));
        Assert.Equal(("20", "48", "g"), (a[0], a[1], a[4])); // relative position unchanged
        Assert.Equal(("240", "48", "g"), (b[0], b[1], b[4]));

        // And the render after the commit keeps it where the drag left it.
        var aAfter = (await NodeEl("a").BoundingBoxAsync())!;
        Assert.InRange(aAfter.X - aLive.X, -1.5, 1.5);
        Assert.InRange(aAfter.Y - aLive.Y, -1.5, 1.5);
    }

    [Fact]
    public async Task A_Child_With_Extent_Parent_Is_Clamped_Inside_Its_Group()
    {
        await OpenAsync("/e2e/flow-groups");
        var g0 = (await NodeEl("g").BoundingBoxAsync())!;
        var a0 = (await NodeEl("a").BoundingBoxAsync())!;

        var sx = a0.X + a0.Width / 2;
        var sy = a0.Y + a0.Height / 2;
        await Page.Mouse.MoveAsync((float)sx, (float)sy);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= 12; i++) await Page.Mouse.MoveAsync((float)(sx + 70 * i), (float)(sy + 45 * i));

        var aLive = (await NodeEl("a").BoundingBoxAsync())!;
        const double tol = 1.5;
        if (aLive.X + aLive.Width > g0.X + g0.Width + tol || aLive.Y + aLive.Height > g0.Y + g0.Height + tol)
        {
            Assert.Fail($"a escaped its group live: a=({aLive.X},{aLive.Y},{aLive.Width},{aLive.Height}) g=({g0.X},{g0.Y},{g0.Width},{g0.Height})" + await DumpAsync("g", "a"));
        }
        await Page.Mouse.UpAsync();
        await WaitForCommitsAsync(1);

        var a = await NodeFieldsAsync("a");
        // Pinned to the group's bottom-right corner (group 440 x 260, relative coordinates).
        Assert.InRange(D(a[0]) + aLive.Width, 440 - tol, 440 + tol);
        Assert.InRange(D(a[1]) + aLive.Height, 260 - tol, 260 + tol);
        Assert.Equal("g", a[4]);
    }

    [Fact]
    public async Task CtrlG_Wraps_The_Selection_In_A_New_Group_Without_Moving_It()
    {
        await OpenAsync("/e2e/flow-groups");
        var p0 = (await NodeEl("p").BoundingBoxAsync())!;
        await NodeEl("p").ClickAsync();
        await NodeEl("q").ClickAsync(new() { Modifiers = new[] { KeyboardModifier.Shift } });

        await Page.Keyboard.PressAsync("Control+g");

        try
        {
            await Assertions.Expect(NodeEl("grp1")).ToHaveCountAsync(1, new() { Timeout = 10000 });
        }
        catch (PlaywrightException)
        {
            Assert.Fail("expected Ctrl+G to create group grp1" + await DumpAsync("p", "q"));
        }
        Assert.Equal("grp1", (await NodeFieldsAsync("p"))[4]);
        Assert.Equal("grp1", (await NodeFieldsAsync("q"))[4]);
        await Assertions.Expect(NodeEl("grp1").Locator("[data-slot='flow-group']")).ToHaveCountAsync(1);
        await Assertions.Expect(NodeEl("grp1")).ToHaveAttributeAsync("aria-label", "grp1, 2 nodes");

        var p1 = (await NodeEl("p").BoundingBoxAsync())!;
        Assert.InRange(p1.X - p0.X, -1, 1);
        Assert.InRange(p1.Y - p0.Y, -1, 1);
    }

    // ── Virtualization ────────────────────────────────────────────────────

    private Task<int> MountedCountAsync()
        => Page.EvaluateAsync<int>("() => document.querySelectorAll(\"[data-testid='flow-root'] [data-flow-node]\").length");

    [Fact]
    public async Task Virtualized_2000_Nodes_First_Paint_Under_1500ms_And_A_Long_Pan_Drops_No_Report()
    {
        await OpenAsync("/e2e/flow-virtual");
        await Page.WaitForFunctionAsync("() => window.__p5 && window.__p5.ready != null", null, new() { Timeout = 20000 });
        var firstNode = await Page.EvaluateAsync<double>("() => window.__p5.firstNode");
        var ready = await Page.EvaluateAsync<double>("() => window.__p5.ready");
        var mountedBefore = await MountedCountAsync();
        _output.WriteLine($"first node mounted at {firstNode:F1} ms after navigation start; ready with nodes at {ready:F1} ms; {mountedBefore} of 2000 nodes mounted");
        Assert.True(firstNode < 1500, $"first paint took {firstNode:F1} ms (budget 1500 ms)" + await DumpAsync());
        Assert.InRange(mountedBefore, 1, 400); // the window, not the graph

        // Frame timing while panning, measured in the page.
        await Page.EvaluateAsync(@"() => {
            window.__p5.frames = []; window.__p5.run = true;
            let last = performance.now();
            const loop = (ts) => { window.__p5.frames.push(ts - last); last = ts; if (window.__p5.run) requestAnimationFrame(loop); };
            requestAnimationFrame(loop);
        }");

        // n12-12 sits at flow (2640, 1320): outside the first window (viewport + one viewport of margin).
        Assert.Equal(0, await NodeEl("n12-12").CountAsync());

        // Pan up-left into the graph by (-800, -380) in 60 pointer moves, starting on empty background:
        // flow (850, 410) is the gap right of column 3 and below row 3 (nodes are 160 x 44, 220 x 110
        // apart) — and well clear of the minimap in the bottom-right corner.
        var pane = (await Page.Locator("[data-testid='flow-root'] [data-slot='flow-pane']").BoundingBoxAsync())!;
        var sx = pane.X + 20 + 850;
        var sy = pane.Y + 20 + 410;
        await Page.Mouse.MoveAsync((float)sx, (float)sy);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= 60; i++) await Page.Mouse.MoveAsync((float)(sx - 800.0 * i / 60), (float)(sy - 380.0 * i / 60));
        await Page.Mouse.UpAsync();

        // Every report the engine sent (journal) must reach .NET (the fixture's ViewportChanged count),
        // and the final bound viewport must equal the live transform.
        try
        {
            await Page.WaitForFunctionAsync(@"() => {
                const sent = (window.__lumeoFlowDiag || []).filter(e => e.ev === 'report').length;
                const got = Number(document.querySelector(""[data-testid='flow-viewport-count']"").textContent);
                return sent > 0 && sent === got;
            }", null, new() { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            Assert.Fail("not every viewport report reached .NET: sent " + await Page.EvaluateAsync<int>("() => (window.__lumeoFlowDiag || []).filter(e => e.ev === 'report').length")
                        + ", received " + await SinkAsync("flow-viewport-count") + await DumpAsync());
        }
        await Page.EvaluateAsync("() => { window.__p5.run = false; }");

        var live = await Page.EvaluateAsync<string>("() => document.querySelector(\"[data-testid='flow-root'] [data-slot='flow-viewport']\").style.transform");
        var bound = await SinkAsync("flow-viewport-sink");
        var parts = bound.Split('|');
        Assert.Equal($"translate({parts[0]}px, {parts[1]}px) scale({parts[2]})", live);

        // The window followed: nodes near the new viewport are mounted, the old corner is not.
        try
        {
            await Page.WaitForFunctionAsync("() => document.querySelector(\"[data-testid='flow-root'] [data-flow-node='n12-12']\") !== null", null, new() { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            Assert.Fail("the render window did not follow the pan (n12-12 not mounted); bound viewport " + bound + await DumpAsync());
        }
        var mountedAfter = await MountedCountAsync();
        var frames = await Page.EvaluateAsync<double[]>("() => window.__p5.frames");
        var sent = await Page.EvaluateAsync<int>("() => (window.__lumeoFlowDiag || []).filter(e => e.ev === 'report').length");
        var sorted = frames.Skip(1).OrderBy(f => f).ToArray();
        var p95 = sorted.Length == 0 ? 0 : sorted[(int)Math.Floor(sorted.Length * 0.95)];
        _output.WriteLine($"pan: {sent} reports sent / {await SinkAsync("flow-viewport-count")} received; viewport {bound}; {mountedAfter} nodes mounted after; frames {frames.Length}, p95 {p95:F1} ms, max {sorted.DefaultIfEmpty(0).Max():F1} ms");
        Assert.InRange(mountedAfter, 1, 400);
    }

    [Fact]
    public async Task Dragging_A_Node_On_The_Virtualized_Canvas_Commits_Normally()
    {
        await OpenAsync("/e2e/flow-virtual");
        var box = (await NodeEl("n1-1").BoundingBoxAsync())!;
        var sx = box.X + box.Width / 2;
        var sy = box.Y + box.Height / 2;
        await Page.Mouse.MoveAsync((float)sx, (float)sy);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= 10; i++) await Page.Mouse.MoveAsync((float)(sx + 3 * i), (float)(sy + 2 * i));
        await Page.Mouse.UpAsync();
        await WaitForCommitsAsync(1);

        var after = (await NodeEl("n1-1").BoundingBoxAsync())!;
        Assert.InRange(after.X - box.X, 29, 31);
        Assert.InRange(after.Y - box.Y, 19, 21);
    }
}
