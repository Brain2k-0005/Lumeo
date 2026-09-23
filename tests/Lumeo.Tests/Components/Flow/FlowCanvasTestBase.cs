using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Flow;

/// <summary>Shared fixture: a bUnit context with Lumeo services and a recording interop fake.</summary>
public abstract class FlowCanvasTestBase : IAsyncLifetime
{
    protected readonly BunitContext Ctx = new();
    protected readonly TrackingInteropService Interop = new();

    protected FlowCanvasTestBase()
    {
        Ctx.AddLumeoServices();
        Ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(Interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await Ctx.DisposeAsync();

    protected static List<L.FlowNode> ThreeNodes() => new()
    {
        new L.FlowNode("a", 0, 0),
        new L.FlowNode("b", 300, 40),
        new L.FlowNode("c", 120, 260, Draggable: false),
    };

    protected static List<L.FlowEdge> TwoEdges() => new()
    {
        new L.FlowEdge("a-b", "a", "b"),
        new L.FlowEdge("b-c", "b", "c", Type: L.FlowEdgeType.SmoothStep),
    };

    /// <summary>Renders a canvas whose parent holds the node list like <c>@bind-Nodes</c> would.</summary>
    protected (IRenderedComponent<L.FlowCanvas> Cut, Func<IReadOnlyList<L.FlowNode>> Current) RenderBound(
        IReadOnlyList<L.FlowNode> nodes, Action<ComponentParameterCollectionBuilder<L.FlowCanvas>>? extra = null)
    {
        IReadOnlyList<L.FlowNode> current = nodes;
        IRenderedComponent<L.FlowCanvas>? cut = null;
        cut = Ctx.Render<L.FlowCanvas>(p =>
        {
            p.Add(c => c.Nodes, nodes)
             .Add(c => c.NodesChanged, (IReadOnlyList<L.FlowNode> n) =>
             {
                 current = n;
                 cut!.Render(pp => pp.Add(c => c.Nodes, n));
             })
             .Add(c => c.FitViewOnInit, false);
            extra?.Invoke(p);
        });
        return (cut, () => current);
    }

    protected static L.FlowNode Node(IEnumerable<L.FlowNode> nodes, string id) => nodes.Single(n => n.Id == id);

    /// <summary>Renders a canvas whose parent holds both the node and edge lists like <c>@bind-Nodes</c>/<c>@bind-Edges</c> would.</summary>
    protected (IRenderedComponent<L.FlowCanvas> Cut, Func<IReadOnlyList<L.FlowNode>> CurrentNodes, Func<IReadOnlyList<L.FlowEdge>> CurrentEdges) RenderBoundWithEdges(
        IReadOnlyList<L.FlowNode> nodes, IReadOnlyList<L.FlowEdge> edges, Action<ComponentParameterCollectionBuilder<L.FlowCanvas>>? extra = null)
    {
        IReadOnlyList<L.FlowNode> currentNodes = nodes;
        IReadOnlyList<L.FlowEdge> currentEdges = edges;
        IRenderedComponent<L.FlowCanvas>? cut = null;
        cut = Ctx.Render<L.FlowCanvas>(p =>
        {
            p.Add(c => c.Nodes, nodes)
             .Add(c => c.NodesChanged, (IReadOnlyList<L.FlowNode> n) =>
             {
                 currentNodes = n;
                 cut!.Render(pp => pp.Add(c => c.Nodes, n));
             })
             .Add(c => c.Edges, edges)
             .Add(c => c.EdgesChanged, (IReadOnlyList<L.FlowEdge> e) =>
             {
                 currentEdges = e;
                 cut!.Render(pp => pp.Add(c => c.Edges, e));
             })
             .Add(c => c.FitViewOnInit, false);
            extra?.Invoke(p);
        });
        return (cut, () => currentNodes, () => currentEdges);
    }
}
