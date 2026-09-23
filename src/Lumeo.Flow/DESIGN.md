# Lumeo.Flow — node canvas engine (spec v1, 2026-09-23)

Owner decision: build a first-party node/flow editor for Lumeo, in the spirit of ReUI's
"Flow" application blocks (https://reui.io/blocks/application/flow), which sit on React Flow.
Blazor has no React Flow, and Lumeo ships first-party engines only (no third-party runtime deps,
MIT). So `Lumeo.Flow` is a new satellite package with its own engine, modelled on `Lumeo.Gantt`
(GanttChart v3 is the template for structure, interop style, tests and docs).

## Goals (MVP = Phase 1 + Phase 2)
- A `FlowCanvas` that renders nodes (user-templated Razor) and edges (SVG) on a pannable,
  zoomable canvas; nodes are draggable; edges can be created by dragging from a source handle
  to a target handle; elements can be selected (click, shift-click, marquee) and deleted.
- Pan/zoom and node drag never re-render Blazor per frame: JS moves the viewport layer /
  the dragged node with CSS transforms and updates the connected edges' `d` live; .NET is told
  once on commit (drop, wheel-gesture end via throttled report). Same discipline as GanttChart.
- Everything themed through tokens; RTL-safe; keyboard-usable; deterministic under test.
- Docs page, live catalog showcase, registry entry, E2E fixture, bUnit + E2E tests.

## Non-goals (v1)
- Sub-flows/groups with nested canvases, edge editing by dragging existing edge ends onto new
  handles (Phase 3 "reconnect"), collaborative cursors, touch pinch (Phase 3), custom edge
  path templates (labels yes, custom `d` no).

## Package layout (mirror Lumeo.Gantt)
```
src/Lumeo.Flow/
  Lumeo.Flow.csproj            net8.0;net10.0, RootNamespace Lumeo, PackageId Lumeo.Flow,
                               ProjectReference ../Lumeo, PublicAPI analyzer like Gantt
  PublicAPI.Shipped.txt / PublicAPI.Unshipped.txt
  _Imports.razor
  DESIGN.md                    this spec, committed
  UI/FlowCanvas/
    FlowCanvas.razor           root: viewport layer + node layer + SVG edge layer + children
    FlowNodeHost.razor         wrapper per node: absolute position, drag hooks, selection, a11y
    FlowHandle.razor           source/target port inside a node template
    FlowEdgeLayer.razor        <svg> with one <path> per edge (+ label, marker)
    FlowBackground.razor       dots | lines | cross grid that follows the viewport
    FlowControls.razor         zoom in/out, fit view, lock toggle (Lumeo Buttons)
    FlowMiniMap.razor          scaled overview with viewport rect, click/drag to pan
    FlowPanel.razor            corner-positioned overlay for custom toolbars
    FlowNodeToolbar.razor      toolbar above the selected node (Phase 2)
    FlowTypes.cs               records + enums (below)
    FlowGeometry.cs            pure math: coordinate transforms, snapping, fit-view, edge paths
    FlowState.cs               selection, viewport, measured sizes; no render-loop coupling
  wwwroot/js/flow.js           the engine (pan, zoom, drag, connect, marquee, measurement)
  wwwroot/css/lumeo-flow.css   plain rules (no var()-based arbitrary Tailwind classes — see
                               memory: css-extractor-dynamic-classes)
```
Wiring, all mandatory: `Lumeo.slnx`; `docs/Lumeo.Docs.csproj` ProjectReference +
`BlazorWebAssemblyLazyLoad Include="Lumeo.Flow.dll"`; docs `ILLink.Descriptors.xml`;
`tests/Lumeo.Tests.csproj`, `tests/Lumeo.Tests.E2E`, `tests/Lumeo.Tests.ServerHost` refs;
`tools/Lumeo.RegistryGen/Program.cs` package map (`["FlowCanvas"] = "Lumeo.Flow"`, plus every
public sub-component), category and description maps; `PerComponentEnricher.cs` candidates
list; `.github/workflows/publish.yml` (restore/build/pack like Lumeo.Gantt) and `ci.yml`
vulnerability scan list; `AddLumeoFlow()` DI extension only if a service is needed (prefer
none — GanttChart needs none). Docs `Program.cs`/lazy-load of the assembly on the docs page,
copied from how `GanttChartPage` / `EnterpriseScheduleView` load `Lumeo.Gantt.dll`.

## Data model (immutable records, `with` for updates — like GanttTask)
```csharp
public enum FlowHandleType { Source, Target }
public enum FlowPosition { Left, Right, Top, Bottom }        // physical, not logical (RTL: document)
public enum FlowEdgeType { Bezier, SmoothStep, Step, Straight }
public enum FlowBackgroundVariant { Dots, Lines, Cross }

public sealed record FlowNode(
    string Id, double X, double Y,
    string? Type = null,               // user's discriminator for NodeTemplate
    object? Data = null,
    double? Width = null, double? Height = null,   // optional fixed size; else measured
    bool Draggable = true, bool Selectable = true, bool Connectable = true,
    bool Deletable = true, int? ZIndex = null);

public sealed record FlowEdge(
    string Id, string Source, string Target,
    string? SourceHandle = null, string? TargetHandle = null,
    string? Label = null, FlowEdgeType Type = FlowEdgeType.Bezier,
    bool Animated = false, bool Dashed = false, bool Deletable = true,
    string? MarkerEnd = "arrow", object? Data = null);

public readonly record struct FlowViewport(double X, double Y, double Zoom);
public sealed record FlowConnection(string Source, string? SourceHandle, string Target, string? TargetHandle);
public sealed record FlowNodeChange(string Id, double X, double Y);                 // drag commit
public sealed record FlowSelection(IReadOnlySet<string> NodeIds, IReadOnlySet<string> EdgeIds);
public sealed record FlowNodeContext(FlowNode Node, bool Selected, bool Dragging, FlowCanvas Canvas);
public sealed record FlowEdgeContext(FlowEdge Edge, bool Selected, double LabelX, double LabelY);
```

## FlowCanvas parameters (MVP)
- `Nodes` / `NodesChanged` (`@bind-Nodes`), `Edges` / `EdgesChanged`, `Viewport` / `ViewportChanged`.
- `NodeTemplate` (RenderFragment<FlowNodeContext>, required), `EdgeLabelTemplate` (optional).
- `Height` ("500px"), `MinZoom` (0.25), `MaxZoom` (2), `FitViewOnInit` (true), `FitViewPadding` (0.1),
  `SnapToGrid` (false), `SnapGrid` ((16,16)), `NodesDraggable`, `NodesConnectable`,
  `ElementsSelectable`, `PanOnDrag` (true), `ZoomOnScroll` (true; plain wheel zooms like
  React Flow — document; `ZoomOnScrollRequiresModifier` false), `SelectionOnShiftDrag` (true),
  `Readonly` (turns drag/connect/delete off), `DeleteKey` ("Delete" + "Backspace"),
  `Class`, `AdditionalAttributes` (root), `ChildContent` (for FlowBackground/Controls/MiniMap/Panel).
- Events: `OnNodeClick`, `OnNodeDoubleClick`, `OnNodeContextMenu(FlowNode, x, y)`, `OnEdgeClick`,
  `OnPaneClick`, `OnConnect(FlowConnection)` + `IsValidConnection` (Func<FlowConnection,bool>),
  `OnNodeDragStop(IReadOnlyList<FlowNodeChange>)`, `OnSelectionChanged(FlowSelection)`,
  `OnDelete(FlowSelection)` (when absent, the canvas removes from Nodes/Edges itself and emits
  NodesChanged/EdgesChanged).
- Methods (via `@ref`): `FitViewAsync(double? padding)`, `ZoomToAsync(double zoom)`,
  `ZoomInAsync/ZoomOutAsync`, `SetCenterAsync(x, y, zoom?)`, `SetViewportAsync(FlowViewport)`,
  `ScreenToFlow(x, y)`, `FlowToScreen(x, y)`, `SelectAsync(ids)`, `ClearSelectionAsync()`.
- Sub-components read the canvas via CascadingValue (`FlowCanvas` context record, IsFixed=false).

## Coordinate spaces (test these first)
screen (clientX/Y) → pane-local (minus pane rect) → flow: `(local - viewport.X) / viewport.Zoom`.
Viewport layer style: `transform: translate(Xpx, Ypx) scale(Zoom)`; nodes are absolutely positioned
in flow coords inside it; the SVG edge layer is a sibling inside the same transformed layer with
`overflow: visible`. Wheel zoom keeps the flow point under the pointer fixed (the #385 lesson:
the anchor must be applied in the same frame as the geometry — JS writes the transform itself and
reports afterwards; when .NET sets the viewport, it stamps `data-flow-viewport="x|y|zoom|id"` and
JS applies it from a MutationObserver — same pattern as `data-gantt-v3-zoom-anchor`).

## JS engine contract (flow.js, ES module, no dependencies)
`registerCanvas(paneEl, dotNetRef, options)` / `unregisterCanvas(paneEl)` / `setViewport(paneEl, x, y, zoom, animateMs)` /
`fitView(paneEl, padding, minZoom, maxZoom)` / `getViewport(paneEl)` / `updateOptions(paneEl, options)`.
Options: `{ minZoom, maxZoom, snap: [gx, gy] | null, nodesDraggable, panOnDrag, zoomOnScroll,
selectionOnShiftDrag, connectable, readonly, rtl }`. JS-side responsibilities:
- pan: pointer drag on the pane background (not on a node/handle), space+drag anywhere;
  transform applied live; `OnViewportChanged(x,y,zoom)` reported at most once per animation
  frame and once more on gesture end (final).
- zoom: wheel (deltaY, ctrl/pinch = same), anchored on the pointer; clamp [min,max]; same reporting.
- node drag: pointerdown on `[data-flow-node]` (not on `[data-flow-handle]` / `[data-flow-nodrag]`),
  threshold 3px, live transform on the node element AND live `d` rewrite of connected edge
  paths (`[data-flow-edge][data-source=..]`/`[data-target=..]`) using the same path math as
  C# (bezier/smoothstep/step/straight — keep `FlowGeometry` and `flow.js` in lockstep; a bUnit
  test compares C# output against a JSON table produced by the JS function for fixed inputs);
  snapping in JS when enabled; multi-drag when the node is part of the current selection;
  on pointerup → `CommitNodeDrag([{id,x,y}...])`, then JS clears its transforms (the next
  render positions them from state). Generation counter so a late commit never overwrites a
  newer state (memory: blazor-guard-before-await).
- connect: pointerdown on a source handle draws a temporary path in a JS-owned `<path
  data-flow-connection-line>`; hovering a compatible target handle highlights it
  (`data-flow-handle-valid`); on drop → `CommitConnect(source, sourceHandle, target,
  targetHandle)`; cancel on Escape or drop on nothing.
- marquee: shift+drag on the pane draws a selection rect; on release →
  `CommitMarquee(nodeIds)` (JS hit-tests node rects in flow coords); plain click on the pane →
  `PaneClicked(x,y)`; click on node → `NodeClicked(id, shift, ctrl)`.
- measurement: ResizeObserver on every node element → `NodesMeasured([{id,w,h}])` debounced;
  handle offsets read from the DOM (`[data-flow-handle]` rects relative to the node).
- diagnostics: `window.__lumeoFlowDiag` journal like #500 (viewport writes, commits, with timestamps).
- Idempotent registration (re-register updates options), cleanup on unregister, no global listeners
  left behind; pointer capture on the pane during gestures.

Interop: add `Flow*Async` members to `IComponentInteropService` with default no-op
implementations exactly like the `GanttV3*Async` family (components never touch `IJSRuntime`).
Add them to `tests/Lumeo.Tests/Helpers/TrackingInteropService.cs`.

## Rendering & a11y
- Root `<div data-slot="flow-canvas" role="application" aria-label=...>` with the pane
  `[data-slot="flow-pane"]` (position: relative, overflow: hidden, height), viewport layer
  `[data-slot="flow-viewport"]`, node layer, SVG edge layer, then ChildContent overlays.
- Node host: `tabindex="0"`, `role="group"`, `aria-label` = node Id unless the template's root
  carries its own, `data-flow-node`, `data-selected`, `data-dragging`; arrow keys move the
  selected node(s) by the snap grid (or 1px; Shift ×10) and commit; Delete/Backspace deletes
  the selection; Escape clears selection; Tab order = node order.
- Handles: `<button type="button" data-flow-handle data-handle-type data-handle-id
  data-position>` 8px circle, `aria-label` "Connect from …"; Phase 2: keyboard connect
  (Enter on a source handle enters "connecting" mode, Enter on a target commits).
- Edges: `<path data-flow-edge stroke="var(--color-border)">`, selected → `--color-primary`,
  `Animated` → dash animation (respects `prefers-reduced-motion`), `Dashed` → stroke-dasharray,
  `MarkerEnd` → `<marker>` defs in the layer, labels as `<foreignObject>`-free positioned divs
  in the viewport layer (simpler and themeable) at the path midpoint.
- Theme tokens only: background dots `--color-border`, selection rect `--color-primary` at 10%
  fill, minimap `--color-muted` / nodes `--color-muted-foreground`, controls = Lumeo `Button`
  Outline/Icon. No `dark:` prefixes. Radius via `--radius-*`.
- Reduced motion: no animated edges/transitions when `prefers-reduced-motion`.

## Tests
- bUnit (`tests/Lumeo.Tests/Components/Flow/`): geometry (transforms both ways, snapping, fit-view
  bounds incl. padding/min/max, four edge path generators, handle anchor points per position),
  rendering (nodes at positions, edges reference nodes, selection classes, readonly disables),
  events (CommitNodeDrag updates Nodes via NodesChanged, CommitConnect calls OnConnect and
  respects IsValidConnection, delete flows), viewport stamping, interop registration/cleanup
  (`TrackingInteropService`), a deterministic "late commit after newer state" race test.
- E2E (`tests/Lumeo.Tests.E2E/Flow/`, fixture `docs/Lumeo.Docs/Pages/E2E/FlowPreview.razor`,
  `[data-testid="flow-root"]`, query params for options): drag a node and assert its committed
  position; wheel-zoom keeps the point under the pointer (±2px, measured on a node far from
  the pane centre — the #385 discipline); pan; connect two handles creates an edge; marquee
  selects; Delete removes; keyboard arrow move. Wait on `data-flow-ready="done"`, never on
  timeouts. Run each new class 5× locally before opening the PR.
- Docs tests: page renders, showcase renders (AllShowcasesRenderTests picks it up by convention).

## Docs deliverables
- `/components/flow-canvas` page (`GanttChartPage.razor` as template: installation, usage,
  demos: basic, custom node cards, connect with validation, readonly, background variants,
  minimap+controls, keyboard), `Shared/Showcases/FlowCanvasShowcase.razor` (live, interactive,
  inside the 16:9 box, README rules), registry maps, nav entry (category "Data Display"),
  ILLink descriptors, sitemap is automatic.
- Phase 3 adds `/blocks/flow-automation`, `/blocks/flow-agent-tree`, `/blocks/flow-pipeline`,
  `/blocks/flow-impact-map` built only from Lumeo components (Card, Badge, Avatar, Item, Sheet
  inspector, ContextMenu, Kbd) — the ReUI four, with our own data.

## Phases and gates
Phase 1 (this dispatch, engine core): package skeleton + all wiring; FlowTypes, FlowGeometry
(+ tests), FlowState; FlowCanvas with nodes, pan, zoom, node drag, background, controls, viewport
stamping, measurement, a11y basics (focus/arrow move/escape); flow.js; interop members; docs
page with two demos; E2E fixture + E2E tests for drag/zoom/pan. Edges may render (static, from
Edges) but connect/select/delete are Phase 2.
Phase 2: handles + connect (+ validation), selection (click/shift/marquee), delete, edge labels,
markers, animated/dashed, FlowMiniMap, FlowPanel, FlowNodeToolbar, keyboard connect, showcase,
remaining docs demos.
Phase 3: auto-layout (`FlowLayout.Tree`, `FlowLayout.Layered`), snap-grid toggle UI, `FlowHistory`
(undo/redo helper, capped), reconnect, touch/pinch, four blocks, RTL audit, a11y audit.

Gates for every phase PR: `dotnet build Lumeo.slnx -c Release -warnaserror` 0/0; full
`tests/Lumeo.Tests` green on both TFMs; new E2E classes 5/5 locally; PublicAPI.Unshipped.txt
complete; registry regen as last commit; root `npm run build:css` if new utility classes appear
in src/**; docs `npm run css:build` if the docs page adds classes; CHANGELOG `## [Unreleased]`
→ Added. Version target: 5.11.0 (new package), bumped at release time by the owner.
