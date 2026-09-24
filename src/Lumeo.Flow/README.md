# Lumeo.Flow

`FlowCanvas` — a node/flow editor canvas for [Lumeo](https://www.nuget.org/packages/Lumeo):
templated nodes, SVG edges, pan, pointer-anchored wheel zoom, node drag with snapping,
background grid and zoom controls. First-party engine — no third-party runtime dependency
(no React Flow, no third-party JS canvas library).

Install alongside the Lumeo core package — it registers no services of its own.

## Install

```bash
dotnet add package Lumeo --prerelease
dotnet add package Lumeo.Flow --prerelease
```

```csharp
// Program.cs
builder.Services.AddLumeo();   // the only DI call — core and every satellite share it
```

## Usage

```razor
@using Lumeo

<FlowCanvas @bind-Nodes="_nodes" @bind-Edges="_edges" Height="440px">
    <NodeTemplate Context="ctx">
        <div class="rounded-md border bg-card px-3 py-2 text-sm shadow-sm">@ctx.Node.Id</div>
    </NodeTemplate>
</FlowCanvas>

@code {
    private IReadOnlyList<FlowNode> _nodes = new List<FlowNode> {
        new("a", X: 0,   Y: 0),
        new("b", X: 240, Y: 120),
    };
    private IReadOnlyList<FlowEdge> _edges = new List<FlowEdge> {
        new("a-b", Source: "a", Target: "b"),
    };
}
```

Full docs: https://lumeo.nativ.sh/components/flow-canvas
