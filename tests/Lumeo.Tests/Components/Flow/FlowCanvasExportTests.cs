using Bunit;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>
/// Export (phase 4): ExportSvgAsync builds a self-contained vector SVG purely from the canvas'
/// own state (no DOM access, so it can't fail); ExportPngAsync delegates to the engine's
/// best-effort DOM rasterization and never throws, even when it fails.
/// </summary>
public class FlowCanvasExportTests : FlowCanvasTestBase
{
    [Fact]
    public async Task ExportSvgAsync_Contains_Every_Node_Label()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Data: "Start"), new("b", 300, 0, Data: "End") };
        var (cut, _, _) = RenderBoundWithEdges(nodes, new List<L.FlowEdge>());

        var svg = await cut.Instance.ExportSvgAsync();

        Assert.StartsWith("<svg", svg);
        Assert.Contains("Start", svg);
        Assert.Contains("End", svg);
    }

    [Fact]
    public async Task ExportSvgAsync_Contains_Edge_Paths_And_Labels()
    {
        var edges = new List<L.FlowEdge> { new("a-b", "a", "b", Label: "next") };
        var (cut, _, _) = RenderBoundWithEdges(ThreeNodes(), edges);

        var svg = await cut.Instance.ExportSvgAsync();

        Assert.Contains("<path d=\"M", svg);
        Assert.Contains("next", svg);
    }

    [Fact]
    public async Task ExportSvgAsync_Escapes_Label_Text()
    {
        var nodes = new List<L.FlowNode> { new("a", 0, 0, Data: "<script>") };
        var (cut, _, _) = RenderBoundWithEdges(nodes, new List<L.FlowEdge>());

        var svg = await cut.Instance.ExportSvgAsync();

        Assert.DoesNotContain("<script>", svg);
        Assert.Contains("&lt;script&gt;", svg);
    }

    [Fact]
    public async Task ExportPngAsync_Returns_Null_Before_The_Engine_Registers()
    {
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));
        // Force the "not registered" branch deterministically instead of racing bUnit's own
        // first-render registration.
        var field = typeof(L.FlowCanvas).GetField("_registered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(cut.Instance, false);

        var result = await cut.Instance.ExportPngAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ExportPngAsync_Returns_Whatever_The_Interop_Layer_Returns()
    {
        Interop.FlowExportPngResult = "data:image/png;base64,xyz";
        var cut = Ctx.Render<L.FlowCanvas>(p => p.Add(c => c.Nodes, ThreeNodes()));

        var result = await cut.Instance.ExportPngAsync(2);

        Assert.Equal("data:image/png;base64,xyz", result);
        Assert.Equal(2, Interop.LastFlowExportPngScale);
    }
}
