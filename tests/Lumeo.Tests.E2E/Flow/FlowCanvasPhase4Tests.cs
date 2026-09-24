using System.Globalization;
using System.Text;
using Lumeo.Tests.E2E.Gantt;
using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Flow;

/// <summary>
/// Real-browser coverage for Lumeo.Flow phase 4 against the Server host's /e2e/flow fixture:
/// resizing a node by its grip, helper-line alignment while dragging, Ctrl+C/V duplication with
/// the (20, 20) offset, inline edge-label editing, and the SVG export.
///
/// Same host/base URL/sequential collection as <see cref="FlowCanvasTests"/> — see that class' XML
/// doc for how to run the ServerHost locally.
/// </summary>
public class FlowCanvasPhase4Tests : GanttParityTestBase
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

    private async Task<(double? W, double? H)> BoundSizeAsync(string id)
    {
        var sink = await SinkAsync("flow-nodes-sink");
        foreach (var part in sink.Split(';'))
        {
            var kv = part.Split(':');
            if (kv[0] != id) continue;
            var fields = kv[1].Split(',');
            double? w = fields.Length > 2 && fields[2].Length > 0 ? double.Parse(fields[2], CultureInfo.InvariantCulture) : null;
            double? h = fields.Length > 3 && fields[3].Length > 0 ? double.Parse(fields[3], CultureInfo.InvariantCulture) : null;
            return (w, h);
        }
        throw new InvalidOperationException($"node {id} not in sink '{sink}'");
    }

    private async Task<string> DumpAsync(params string[] nodeIds)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("--- flow diagnostics (phase 4) ---");
        sb.AppendLine("nodes sink: " + await SinkAsync("flow-nodes-sink"));
        sb.AppendLine("edges sink: " + await SinkAsync("flow-edges-sink"));
        sb.AppendLine("resize count: " + await SinkAsync("flow-resize-count"));
        sb.AppendLine("paste count: " + await SinkAsync("flow-paste-count"));
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
    public async Task Resizing_A_Node_By_Its_SE_Grip_Commits_The_New_Size()
    {
        await OpenAsync();
        await NodeEl("n2").ClickAsync(); // select n2 so its FlowNodeResizer grips render
        var grip = Page.Locator("[data-testid='flow-root'] [data-flow-node='n2'] [data-flow-resize-handle][data-resize-dir='se']");
        await Assertions.Expect(grip).ToBeVisibleAsync(new() { Timeout = 5000 });
        var box = (await grip.BoundingBoxAsync())!;

        const double dx = 60, dy = 40;
        await DragAsync(box.X + box.Width / 2, box.Y + box.Height / 2, dx, dy);

        await Assertions.Expect(Page.Locator("[data-testid='flow-resize-count']")).ToHaveTextAsync("1", new() { Timeout = 10000 });
        var size = await BoundSizeAsync("n2");
        if (size is not { W: { } w, H: { } h } || w <= 100 || h <= 30)
        {
            Assert.Fail($"expected n2's committed size to grow past its ~100x30 unresized card, got ({size.W}, {size.H})" + await DumpAsync("n2"));
        }
    }

    [Fact]
    public async Task CtrlD_Duplicates_The_Selection_With_A_20_20_Offset()
    {
        await OpenAsync();
        var before = await SinkAsync("flow-nodes-sink");
        var beforeCount = before.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;
        await NodeEl("n2").ClickAsync();

        await Page.Keyboard.PressAsync("Control+d");

        try
        {
            await Page.WaitForFunctionAsync(
                "n => document.querySelector(\"[data-testid='flow-nodes-sink']\").textContent.split(';').filter(Boolean).length > n",
                beforeCount, new PageWaitForFunctionOptions { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            Assert.Fail("expected Ctrl+D to add a node" + await DumpAsync());
        }
        var after = await SinkAsync("flow-nodes-sink");
        Assert.Equal(beforeCount + 1, after.Split(';', StringSplitOptions.RemoveEmptyEntries).Length);
        // n2 starts at (320, 0); the duplicate lands at (340, 20).
        Assert.Contains(":340,20", after);
    }

    [Fact]
    public async Task DoubleClicking_A_Label_Opens_An_Inline_Editor_And_Enter_Commits()
    {
        await OpenAsync();
        var label = Page.Locator("[data-testid='flow-root'] [data-flow-edge-label][data-edge-id='e4']");
        await label.DblClickAsync();

        var input = Page.Locator("[data-testid='flow-root'] [data-flow-edge-label][data-edge-id='e4'] input");
        await Assertions.Expect(input).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(input).ToHaveValueAsync("next");
        await input.FillAsync("later");
        await input.PressAsync("Enter");

        try
        {
            await Page.WaitForFunctionAsync(
                "() => (document.querySelector(\"[data-testid='flow-edges-sink']\").textContent || '').includes('e4:n4-n5:later')",
                new PageWaitForFunctionOptions { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            Assert.Fail("expected e4's label to commit to 'later'" + await DumpAsync());
        }
    }

    [Fact]
    public async Task ExportSvg_Produces_A_Vector_Document_Containing_Node_Labels()
    {
        await OpenAsync();

        await Page.Locator("[data-testid='flow-export-svg-btn']").ClickAsync();

        var sink = Page.Locator("[data-testid='flow-export-svg-sink']");
        await Assertions.Expect(sink).Not.ToHaveTextAsync("", new() { Timeout = 10000 });
        var svg = await sink.TextContentAsync() ?? "";
        Assert.StartsWith("<svg", svg);
        Assert.Contains("Trigger", svg); // n1's Data
        Assert.Contains("<path", svg);
    }
}
