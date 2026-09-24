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

## Phase 4 (parity features, 2026-09-24)
- `FlowNodeResizer` (inside a node template): corner + edge grips, `MinWidth/MinHeight/MaxWidth/MaxHeight`,
  Shift keeps aspect; live resize in JS with connected edges following; commit `Width/Height` (and X/Y for
  left/top grips) through `NodesChanged` + `OnNodeResizeStop`. Keyboard: Shift+arrows resize the focused node.
- Helper lines: while dragging, snap to other nodes' left/centre/right and top/middle/bottom within `HelperLineThreshold`
  (5px), draw guides in the edge SVG layer; `HelperLines` parameter (off by default), works with SnapToGrid off.
- Clipboard: Ctrl+C / Ctrl+V / Ctrl+D on the focused canvas copy/paste/duplicate the selection including edges between
  selected nodes, pasted with a (20,20) offset and new ids from `NewNodeId` (Func<FlowNode,string>, default GUID);
  `OnPaste` hook; honours the editable-target guard.
- `ConnectionMode` Strict (default, source→target) | Loose (any handle to any other handle, direction inferred).
- Edge label inline editing: `EdgeLabelEditable`; double-click a label opens an inline Lumeo Input, Enter commits
  (`EdgesChanged` + `OnEdgeLabelChanged`), Escape cancels; screen-reader announced.
- Export/import: `FlowDocument(Nodes, Edges, Viewport)` record, `ToDocument()` / `LoadDocument(doc)` on the canvas,
  JSON-round-trippable (System.Text.Json source-gen friendly); `ExportSvgAsync()` (edges + node boxes with labels,
  vector) and `ExportPngAsync(scale)` best effort via SVG foreignObject of the node DOM with inlined computed styles
  → canvas → data URL; documented limitations (cross-origin images, fonts).
- From #511: touch/pinch E2E via CDP `Input.dispatchTouchEvent`; opt-in `ValidateOnHover` calling `IsValidConnection`
  per hovered target; `FlowNodeToolbar` for multi-selection (one toolbar at the selection's bounding box top);
  agent-tree block switched to `FlowLayout.Tree`.

## Phase 5 (scale and structure)
- Sub-flows/groups: `FlowNode.ParentId` + `FlowExtent` (None | Parent); child X/Y relative to the parent; dragging a
  parent moves its subtree live in JS; `Extent=Parent` clamps children inside; `FlowGroupNode` default template
  (label, tinted background, resizable); z-order parents under children; edges use absolute positions; marquee and
  minimap use absolute rects; `FlowLayout` respects groups (lays out per group); keyboard: Ctrl+G groups the selection,
  Ctrl+Shift+G ungroups (raising `NodesChanged`).
- Virtualization: `OnlyRenderVisibleNodes` (default false): only nodes intersecting viewport+margin (one viewport in
  each direction) and edges touching them are rendered; the engine's rAF viewport report drives a debounced,
  hysteresis-guarded .NET re-render; measurement cache keeps sizes of unmounted nodes; drag/connect of visible nodes
  unaffected; a 2000-node E2E measures first paint < 1.5 s and a pan without dropped commits; docs demo with 1500 nodes.
- Both: a11y (groups announced with child count), RTL, docs demos, bUnit + E2E, showcase untouched.

## Phase 4 — as built
Shipped essentially as specified above, with these adaptations:
- `LoadDocument(doc)` shipped as `LoadDocumentAsync(FlowDocument document)` — every other public
  Task-returning method on `FlowCanvas` carries the `Async` suffix (`FitViewAsync`, `SetViewportAsync`,
  ...); this keeps the new method consistent with that existing convention rather than the spec's
  literal name.
- `ExportSvgAsync()`'s node/edge colours are hardcoded (not theme tokens): the export is a portable
  artifact meant to be opened OUTSIDE the running app (saved to disk, pasted elsewhere), where CSS
  custom properties have nothing to resolve against. `ExportPngAsync` inlines each node's live
  COMPUTED styles instead, which is theme-correct by construction (it reads whatever the browser
  actually painted) — the two exports intentionally get their correctness from different mechanisms.
- Helper lines only ever compute for a single dragged node (not a multi-node drag — there is no
  single "the" moving node to align in that case) and have no automated JS/C# lockstep table the
  way the four edge-path generators do; `FlowGeometry.ComputeHelperLines` is the source of truth,
  `flow.js`'s `computeHelperLines` is a hand-kept port, exercised live but not table-tested.
- `ConnectionMode` is enforced structurally in `flow.js` (which handle types a connect gesture may
  start from and land on); `FlowCanvas.CommitConnect` itself never inspected handle types even in
  Strict mode (only node-level `Connectable`/`Readonly`/`IsValidConnection`), so the C# commit path
  is unchanged either way — only the *gesture* (pointer and keyboard) changes what it will propose.
- `EdgesReconnectable`'s reconnect gesture (phase 3a) was left Strict-only — Loose reconnecting an
  existing edge's end onto a source-typed handle raises the same "what does direction even mean now"
  question `ConnectionMode` answers for a fresh connect, and extending it wasn't in this phase's
  scope; flagged as a follow-up if an app asks for it.
- `FlowNodeResizer`'s eight pointer grips are `aria-hidden` (no keyboard equivalent — matches the
  precedent phase 3a already set for the reconnect edge-end handles, and for the same reason: an
  unreachable screen-reader stop is worse than none). The keyboard path is Shift+arrow on the
  FOCUSED NODE, gated on that node actually carrying a `FlowNodeResizer` in its template
  (`FlowCanvas.RegisterResizer`/`UnregisterResizer`, called from the resizer's own lifecycle) —
  a node without one keeps the existing "Shift = ×10 move" behaviour unchanged.
- Agent-tree block (`docs/Lumeo.Docs/Pages/Patterns/FlowAgentTreePattern.razor`): its hand-written
  `Tidy()` recursive layout is gone, replaced by a single `FlowLayout.Tree` call. Because
  `FlowLayout.Tree` treats every edge — dashed ones included — as a tree edge, the "model" and
  "attachment" nodes (connected to "root" by dashed, non-tree edges in the original design) now
  rank as ordinary depth-1 children next to research/coding instead of hanging in their own
  reserved slot below the root; a real layout difference, not a regression — this block exists to
  demonstrate the library API, not to preserve one hand-tuned arrangement.
- Multi-selection `FlowNodeToolbar` renders once at the selection's bounding box, but — unlike the
  single-node case, which flow.js additionally repositions LIVE via `data-flow-toolbar-for` while
  a pointer drag is in flight — it only re-anchors on the next Blazor render (a settled drag,
  selection change, pan/zoom report). Extending live multi-node tracking to an aggregate rect
  (rather than one node's own) was judged not worth the added engine complexity for this phase.

## Phase 5 — as built
Shipped as specified above, with these decisions and adaptations:
- **Model.** `FlowNode` gained `ParentId` and `Extent` as optional trailing primary-constructor parameters.
  The old twelve-member `Deconstruct` is kept as an explicit overload (and `FlowLayoutOptions` keeps its
  three-member one after gaining `GroupPadding`/`GroupHeaderHeight`), so existing source compiles
  unchanged. Binary compatibility is not preserved (the constructors changed) — irrelevant for a package
  that has not shipped yet, and recorded in `PublicAPI.Unshipped.txt`. A missing parent id, a self-parent
  or a parent cycle is treated as top-level (every member of a cycle; a node hanging off a cycle member
  stays its child). `FlowHierarchy` (internal, pure) resolves parents, depths, absolute positions and the
  render order once per node list; `FlowGeometry.GetAbsolutePositions` and `FlowCanvas.GetAbsolutePosition`
  expose the same math publicly.
- **Flat DOM, absolute hosts.** Children are NOT nested DOM elements (React Flow does the same): every
  host sits at its absolute flow position and `data-x`/`data-y` carry absolute numbers, so every existing
  engine path (edges, marquee, helper lines, fit, export) kept working unchanged. `.NET` converts back:
  `CommitNodeDrag`/`CommitNodeResize` receive absolute positions and store positions relative to the
  parent's NEW absolute position (the parent may have moved in the same gesture).
- **Subtree drag.** `flow.js` splits a drag into LEADERS (dragged nodes without a dragged ancestor) and
  FOLLOWERS (every mounted descendant of a leader). Only leaders snap, clamp and use helper lines;
  followers keep their exact offset to their leader, so .NET sees an unchanged relative position and
  does not report them in `OnNodeDragStop`. This is the resizer's "derive from the live rect" idea
  generalised to a set of nodes rather than a reuse of `anchorForResize` itself.
- **Z-order.** Parents render before their children; a nested node without an explicit `ZIndex` gets
  `z-index: depth`. Edges whose deeper end is nested at level N draw in an extra
  `<svg data-slot="flow-edges-raised" data-level="N">` with `z-index: N` (labels too) — above their group,
  below the nested nodes. Consequence: a nested node paints above every top-level node, including a
  top-level node that comes later in the list and overlaps its group (React Flow's elevation rule too).
- **Extent=Parent** clamps in the live drag (JS, against the parent element's rect), on commit and on
  arrow keys (.NET). It does not clamp a resize of the child, and shrinking the group does not push its
  children back in — they are clamped the next time they move. Documented rather than engineered: a
  resize-time clamp needs the children's sizes inside the resize gesture.
- **Resize of a group** from a west/north grip shifts its direct children by the opposite delta on
  commit, so they stay where they are on screen (React Flow's `NodeResizer` behaviour).
- **Ctrl+G** creates a node of `Type = FlowGroupNode.GroupType` ("group") sized to the selection's bounds
  plus 20 padding and a 28 label row, in the selection's common parent (top level when they differ); the
  id comes from `NewNodeId` (called with the draft group node) or `"flow-group-" + a short GUID`; the
  members keep their `Extent`. A selected node whose ancestor is also selected comes along inside it.
- **Ctrl+Shift+G** dissolves every selected group (children move one level up at unchanged absolute
  positions, the group and any edge touching it are removed). With no group selected it takes the
  selected nodes out of their group instead — the spec only said "ungroups"; this makes the shortcut
  useful on a child too. Both are announced through the canvas' own live region.
- **Delete** follows React Flow: deleting a group deletes its subtree (and `OnDelete` receives the
  expanded selection). A descendant that is not `Deletable` survives, re-parented to its nearest
  surviving ancestor (or the top level) at the same absolute position, so no node ever points at a
  parent that is gone.
- **Clipboard** copies a selected group's descendants too; a copied child of a copied group keeps its
  relative position inside the new group, everything else lands (20, 20) away in its original parent.
- **FlowLayout** lays out each sibling set separately, deepest groups first, with only the edges between
  two members of that set (an edge crossing a group border counts at the level where both ends meet, as
  an edge of the group). Children land at (`GroupPadding`, `GroupPadding + GroupHeaderHeight`) inside
  their group; a group grows (never shrinks) its `Width`/`Height` to fit them. Without any `ParentId`
  the result is exactly the flat algorithm's.
- **Virtualization window.** The visible viewport inflated by one viewport on every side (3×3 viewports).
  A node mounts when its absolute rect intersects the window, plus: its ancestors (z-order, clamping and
  subtree drags read the parent element), any node being dragged, and an already-mounted selected node
  (so a keyboard move out of the window never drops focus before the next re-window). Edges mount when
  either end is mounted; the path elements carry `data-sa`/`data-ta` ("x|y|position") so the engine can
  redraw an edge whose other end is not in the DOM while the mounted end is dragged or resized.
- **Hysteresis + "debounce".** The window is rebuilt only when the visible rect leaves the window
  deflated by half a viewport (a pan of more than half a viewport, or a zoom-out past roughly 2×), or
  when a zoom-in shrinks it below half the size it was built for. The spec's "debounced" re-render is a
  throttle with a trailing call (at most one rebuild per 80 ms while a gesture runs, always one on the
  final report): a pure debounce would not mount anything new during a long continuous pan until the
  pointer stopped, and the one-viewport margin only covers one viewport of travel.
- **Fit.** The engine cannot fit what is not mounted, so a virtualized canvas fits from the MODEL: on
  init once the engine reports the pane size (`PaneResized`; nothing mounts before that) and in
  `FitViewAsync` (fixed sizes, measurements, else the 150 × 40 default). The engine is told
  (`virtualized` option) and skips its own DOM fit. A fit that shows the whole graph mounts the whole
  graph — virtualization only helps when the graph is larger than the screen.
- **What stays DOM-bound** under virtualization, by design: helper-line candidates, marquee hit-testing,
  connect/reconnect targets and handle measurements only see mounted nodes (everything within one
  viewport of the visible area). Everything model-level — selection, delete, clipboard, keyboard moves,
  a drag of the selection (unmounted selected nodes move by the same delta in the model), minimap,
  export, `ToDocument` — sees every node. Measured sizes survive an unmount (`FlowState` forgets a size
  only when the node leaves the list).
- **RTL.** Group geometry is physical like the rest of the canvas (a child at `X = 20` sits 20 flow units
  from its group's LEFT edge under `dir="rtl"` too); `FlowGroupNode`'s label row is ordinary flow content
  and starts at the inline-start edge, so it reads right-to-left there.
- **Engine perf fix found on the way:** `findNode` walked every node element per call (twice per
  connected edge per frame) — quadratic on large graphs; it is now a native attribute selector lookup.
- **Measured (2000-node E2E, Blazor Server, local):** first node mounted ~127–160 ms after navigation
  start (budget 1.5 s); 132 of 2000 nodes mounted at 100%; a 60-step pan of (-800, -380) sent 61 viewport
  reports and .NET received all 61; 210 nodes mounted afterwards; frame p95 16.7 ms, worst frame ~33–47 ms
  (the frame a re-window render lands in).
