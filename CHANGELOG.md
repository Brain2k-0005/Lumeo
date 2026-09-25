# Changelog

All notable changes to Lumeo will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **`DataGrid.ScrollbarBelowHeader`**: keeps the browser's own scrollbar but starts its vertical
  track below the header instead of running alongside it. Splits the header into its own
  non-scrolling `<table>` above a second, independently-scrolling one for the body (kept aligned
  via a JS horizontal-scroll mirror and identical `table-layout: fixed` column widths on both);
  the header is padded by the body's own scrollbar-gutter width so its background/border still
  span the full grid frame. Requires every column to resolve to a fixed pixel width, otherwise
  falls back to the classic single-table layout. Default `false`; ignored when `OverlayScrollbar`
  is also set. Owner field report.
- **`ThemeService.CycleModeAsync()`: the three-way System → Dark → Light → System cycle**, split
  out of `ToggleModeAsync()` (see Changed below) for callers that deliberately want System as a
  stop — `ThemeToggle` now calls this when `IncludeSystem="true"` (its default). SQL Analyst field
  report (5.11.2), finding LU-22.

### Changed
- **`ThemeService.ToggleModeAsync()` now flips between the two RESOLVED appearances instead of
  cycling System → Dark → Light → System.** From System mode, the old cycle always stepped to
  Dark next, which was a visual no-op when the OS already preferred dark — a light/dark switch
  needed two clicks. It now toggles based on `IsDark` (Dark→Light, Light→Dark, and System resolves
  to whichever is opposite of what's currently showing), so a single click always flips the visible
  theme. The old three-way cycle behaviour is preserved under the new `CycleModeAsync()` (see
  Added). SQL Analyst field report (5.11.2), finding LU-22.

### Fixed
- **`DataGrid`'s default (single-table) layout: the header's background/border now span the full
  grid frame, including the native vertical scrollbar's gutter** — previously the header's
  bg-card/border only ever painted across the `<thead>`'s own table width, leaving a "white notch"
  above the scrollbar at the frame's rounded top-right corner. A live-measured
  `--lumeo-grid-header-offset` custom property drives a background band on the scroll container
  that repaints the same header colours across the gutter; in Chromium/Safari (`::-webkit-
  scrollbar-track` recognised again since LU-21, above) the same property also starts the
  scrollbar thumb's own travel range below the header via a track margin. The band carries the
  header cells' own muted tint, so a grid whose columns are narrower than the frame shows one
  continuous header instead of a lighter strip past the last column. No parameter — on by
  default for the classic layout. Owner field report.
- **`DataGrid.OverlayScrollbar`'s vertical track now starts below the header** instead of
  overlapping it — the track/thumb live-measure the sticky header's height (handles density,
  grouped headers, a hidden header). Owner field report.
- **Consumers can once again style Lumeo's scrollbars with `::-webkit-scrollbar*` rules in
  Chromium/WebKit.** Since Chrome 121, any element with a standard `scrollbar-width`/
  `scrollbar-color` value set ignores every `::-webkit-scrollbar*` pseudo-element on that element —
  including a consumer's own overrides — so Lumeo's blanket `* { scrollbar-width: thin; ... }` rule
  silently killed all WebKit scrollbar styling (ours and consumers') in every Chromium/WebKit
  browser. The standard properties now only apply inside
  `@supports not selector(::-webkit-scrollbar)` (Firefox); Chromium/WebKit get the same thin look
  from the (now live again) `::-webkit-scrollbar*` rules. SQL Analyst field report (5.11.2),
  finding LU-21.

## [5.11.3] - 2026-09-25

### Added
- **`Lumeo.Flow`: `FlowFitViewOptions.AnchorAlign` (`FlowAnchorAlign`: `Center` default, `Start`, `End`).**
  When `FitViewAsync`'s anchor-clamped branch fires (the anchor would otherwise need a zoom below
  `MinZoom`), `Center` keeps the anchor mid-pane (unchanged behaviour); `Start`/`End` instead pin the
  anchor's leading/trailing edge at the fit padding from the pane's matching edge on both axes — for
  a `LeftToRight`/`TopToBottom` tree, centring the anchor used to waste half the pane on the side the
  tree never grows into. RTL-aware horizontally. Implemented on both the .NET-computed and the
  engine-computed fit paths. SQL Analyst field report (5.11.1), finding LU-19.

### Fixed
- **`DropdownMenuContent`/`ContextMenuContent`/`MenubarContent`: a separator no longer forces a
  horizontal scrollbar.** The panel's padding lived on the outer element while the inner
  `overflow-y-auto` scroll viewport had no horizontal padding of its own, so a full-bleed `-mx-1`
  separator overhung the viewport's zero-padding edge by 4px each side — and `overflow-y:auto`
  implies `overflow-x:auto` once `overflow-x` is left at its visible default, so every menu with a
  separator showed a horizontal scrollbar. The padding now lives on the scroll viewport instead
  (`overflow-x-hidden` added as a belt); submenus still escape via the panel's `overflow-visible`.
  SQL Analyst field report (5.11.1), finding LU-20.
- **`Lumeo.Flow`: an `Animated=true, Dashed=false` edge's travelling "flow" overlay now follows
  `FlowEdge.Class`/`Style`.** The overlay path never received the edge's class or style, so a
  recoloured edge's travelling marker stayed the library's default colour. SQL Analyst field report
  (5.11.1), finding LU-18.

## [5.11.2] - 2026-09-25

### Fixed
- **The grid row/header and menu item height tokens added in 5.11.1 now take effect.**
  `--lumeo-grid-header-h` and `--lumeo-grid-row-h` set the minimum height of the DataGrid header
  and data rows, and `--lumeo-menu-item-h` sets the minimum height of DropdownMenu, ContextMenu and
  Menubar items (including checkbox, radio and sub-trigger items). In 5.11.1 they were declared but
  no component read them. Defaults are unchanged; Compact density keeps its own tighter heights.
- **Docs site: desktop dropdown and context menu items are 32px again.** The docs site's own
  stylesheet had not been rebuilt, so the items kept the 44px touch floor at every width. A test
  now fails when that stylesheet is stale.

## [5.11.1] - 2026-09-25

### Added
- **`Lumeo.Flow`: `FlowEdge.Class`/`Style`, per-edge colouring without `!important`.** The default
  edge stroke now reads a `--lumeo-flow-edge-stroke` CSS custom property (falls back to
  `--color-muted-foreground`) instead of a literal inline colour, so an external rule — via the new
  `FlowEdge.Class` merged onto the edge's path, or scoped to the whole canvas — wins in the cascade
  with no `!important`; `FlowEdge.Style` remains a one-off inline escape hatch appended last. The
  default arrowhead marker follows the same token (and upgrades to `context-stroke` under
  `@supports` in engines that implement it, so a selected/recoloured edge's arrowhead tracks it
  too). SQL Analyst field report (5.11.0), finding LU-02/LU-03.
- **`Lumeo.Flow`: `FlowCanvas.FitViewAsync(FlowFitViewOptions)`, `PaneSize`, `GetPaneSizeAsync()`, `OnFitView`.**
  Per-call `Padding`/`MinZoom`/`MaxZoom` overrides and an optional `AnchorNodeId` — when the plain
  fit would need a zoom below `MinZoom`, the canvas centres on that node at `MinZoom` instead of
  zooming out further ("start readable" on a large graph). `PaneSize` exposes the pane's last known
  size; `GetPaneSizeAsync()` re-measures it fresh from the DOM. `OnFitView` fires once a fit
  completes, including the engine's own initial `FitViewOnInit` fit. SQL Analyst field report,
  finding LU-08.
- **Density tokens for the controls consumers override most.** `--lumeo-grid-header-h`,
  `--lumeo-grid-row-h`, `--lumeo-menu-item-h`, `--lumeo-popover-p` and `--lumeo-button-radius` join
  the existing control-height, icon-size and grid-cell-padding tokens; defaults are unchanged
  (consumer report).

### Changed
- **`Switch`'s default (Md, Comfortable density) track now matches shadcn new-york v4's
  `switch.tsx` exactly: 18.4×32px (`h-[1.15rem] w-8`) with a 1px border, was 20×36px
  (`h-5 w-9`) with a 2px border — the old, pre-v4 shadcn/Radix default. The thumb (16×16px)
  and checked translate distance (14px, matching shadcn's own
  `translate-x-[calc(100%-2px)]`) already lined up. The other 6 `Lumeo.Size` rungs
  (Xxs–Xxl, excluding Md) are rescaled proportionally around the new Md so the full
  7-rung scale stays monotonic; the invisible touch-target hit-area extension (PR #388)
  is re-derived for the new geometry and now also covers `Lg`, which dipped under the
  24px minimum as a side effect of the rescale.

### Fixed
- **`Lumeo.Flow`: `FlowLayout.Layered` no longer throws on a duplicate node id.** It built its
  in-degree map with `ids.ToDictionary(id => id, ...)`, which threw `ArgumentException` the second
  time a node id repeated (real data — a SQL Server deadlock XML repeating a resource id — crashed
  the whole Blazor Server circuit); `FlowLayout.Tree` and the internal back-edge removal already
  handled duplicates and were audited for the same class of bug. SQL Analyst field report, finding
  LU-01.
- **`Lumeo.Flow`: `Animated` no longer forces a dashed stroke.** `Animated=true, Dashed=false` now
  stays a solid line with a small travelling-dash overlay, instead of always rendering
  `stroke-dasharray`; `Dashed` alone is unaffected, and the overlay respects
  `prefers-reduced-motion`. SQL Analyst field report, finding LU-04.
- **`Lumeo.Flow`: `FlowHandle` drops out of the tab order (and goes `aria-hidden`) when connecting is impossible.**
  Previously it stayed a focusable, announced "Connect from/to" control even under `Readonly`,
  `NodesConnectable="false"` or a node's own `Connectable="false"` — a dead-end tab stop with
  misleading screen-reader text. It stays visually a port either way. SQL Analyst field report,
  finding LU-05.
- **`Lumeo.Flow`: `Enter`/`Space` on a focused node now raises `OnNodeClick`.** Matches a mouse
  click (including the selection toggle), respects the editable-target guard the rest of the
  canvas' shortcuts already use, and `Space` no longer scrolls the page. SQL Analyst field report,
  finding LU-06.
- **`Lumeo.Flow`: `FitViewAsync` after a container resize now fits the CURRENT pane size.**
  It used to compute against the last debounced resize report, which can still be in flight right
  after a container grows/shrinks in the same tick (fits against the previous size); it now
  re-measures the pane fresh from the DOM first. SQL Analyst field report, finding LU-07.
- **`Lumeo.Flow`: the default edge label pill no longer wraps.** Added `white-space: nowrap`
  (truncating a very long label with an ellipsis past a small max-width) instead of letting a
  short label like "owns · X" wrap into a two-line pill with background per line. SQL Analyst
  field report, finding LU-09.
- **`Lumeo.Flow`: `FlowLayout.Tree` places a multi-parent node under its DEEPEST parent.** A
  node's depth was already the longest path reaching it; it is now centred under the single
  parent that explains that depth, instead of under whichever parent's subtree walk happened to
  reach it first (previously the column and the visual parent could disagree). SQL Analyst field
  report, finding LU-10.
- **`Lumeo.Flow`: an edge without an explicit `SourceHandle`/`TargetHandle` keeps the default side anchor.**
  Adding any `FlowHandle` to a node used to silently re-anchor every edge from/to that node onto
  the node's first handle of the matching type, even for edges that never named one; only an
  explicit handle id now switches an edge into handle-based anchoring. SQL Analyst field report,
  finding LU-11.
- **DataGrid: `ColumnSizing="FitWithMinimum"` now actually caps growth and honours a
  resized/restored width** (DocFlow consumer report against 5.11.0). Previously
  `DataGridHeaderCell` emitted only a CSS `min-width` under `table-layout: auto`, which two
  ways failed the mode's own contract: (1) a `white-space: nowrap` cell's content-driven
  minimum width always won regardless of any width declared on the cell, so a 12-column
  search grid could render 1512px wide in a 1310px container instead of filling it; (2)
  `MinWidth ?? Width` unconditionally preferred the declared `MinWidth`, so a user-dragged or
  `LayoutStorageKey`-restored column width was never reflected in any CSS property and
  "jumped back" on reload. The grid now measures its horizontal scroll container (a
  `ResizeObserver` reporting back to .NET) and computes an explicit pixel width per column,
  then renders `table-layout: fixed` at those widths — a real ceiling, since fixed layout
  uses only the widths it's given, never content. Columns fill the container exactly when
  there's room, never shrink below their own `MinWidth`, and a resized/restored width always
  wins (clamped to `MinWidth`/`MaxWidth`); cell content now truncates with an ellipsis
  instead of forcing the column wider.
- **DataGrid: `Virtualized` + `OnRangeRequest` with `IsLoading="true"` no longer unmounts the
  scroll container** (SQL Analyst consumer report, LU-12). `IsLoading` used to replace the
  ENTIRE body with a plain skeleton, tearing down Blazor's `<Virtualize ItemsProvider>` along
  with it — and with it the only thing that ever calls `OnRangeRequest`, so a consumer whose
  own loading flag flipped back to `false` from inside that same handler could never get
  there and stayed stuck showing the skeleton forever. The skeleton now renders BESIDE a
  still-mounted `<Virtualize>` for this mode instead of replacing it.
- **DataGrid: a `LayoutStorageKey`-persisted sort now reaches the FIRST server-virtualization
  range request**, not just a second one (SQL Analyst consumer report, LU-13). Blazor's
  `<Virtualize ItemsProvider>` fires its first request from the child component's own
  initialization — before the grid gets a chance to await the persisted-layout read — so the
  very first request always carried the default sort/filters, with the restored ones only
  arriving (and visibly reordering the rows) on a second request a moment later. The initial
  fetch now waits for a pending restore to resolve.
- **MCP: `lumeo_search` no longer requires the whole query to appear verbatim.** A
  multi-word query (e.g. `"flow diagram nodes edges"`) is now tokenized and scored per
  word, so a component surfaces when it matches ANY of the query's words instead of only
  the exact phrase — `lumeo_search("flow diagram nodes edges")` now returns `FlowCanvas`
  first. (Field report finding LU-15.)
- **MCP: `publish-mcp` now fails the release if the regenerated data doesn't match the
  tag.** A hard version-match assertion (and a Lumeo.Flow presence check) runs right
  after `Lumeo.RegistryGen`, before build/publish, so a stale or partial regen can never
  ship silently again. (Field report finding LU-15.)
- **MCP: `lumeo_get_install` no longer names DI methods that don't exist.** `AddLumeoDataGrid()`,
  `AddLumeoCharts()`, `AddLumeoEditor()`, `AddLumeoScheduler()`, `AddLumeoGantt()` and
  `AddLumeoMotion()` were never real methods — `AddLumeo()` is the only service-collection
  extension in the library, and it already registers everything satellites need (including
  `IDataGridExportService`). Fixed in the MCP install notes and in `skills/lumeo/SKILL.md`.
  A new test derives the ground truth from the actual C# source and fails if any install
  note ever names a method that isn't real. (Field report finding LU-16.)
- **Every satellite package now ships its own README, not the core library's.**
  `Directory.Build.targets` used to unconditionally pack the repo-root `README.md` into
  every package that opted in — so `Lumeo.Flow`, `Lumeo.DataGrid`, `Lumeo.Charts` and
  every other satellite's `.nupkg` (plus every `Lumeo.Icons.*` pack) showed the generic
  core-library description on nuget.org, with no mention of what the package actually
  provides. `Lumeo.DataGrid.Export` had no README at all. Each satellite now carries a
  concise, install-and-usage README next to its `.csproj`; the packing rule prefers it
  when present. (Field report finding LU-17.)
- **Escape inside an open Select, Combobox, DropdownMenu, ContextMenu, Popover, DatePicker,
  TimePicker, Cascader or TreeSelect no longer also closes the surrounding Dialog, Sheet or
  Drawer.** The inner overlay now stops the key event unconditionally; a propagation stop gated on
  a per-render expression did not hold in a real browser (consumer report).
- **Destructive Badge, Button, Chip, SpeedDial, Upload trigger and AlertDialog action use white
  text, as in shadcn v4.** They no longer read `--destructive-foreground`, which themes from tweakcn
  and others repoint to a dark red text colour, turning every destructive badge red on red
  (consumer report).

## [5.11.0] - 2026-09-24

### Added
- **New package `Lumeo.Flow`: `FlowCanvas`, a node/flow editor canvas.** Your own Razor
  node templates and SVG edges (bezier, smooth-step, step, straight) on a pannable, zoomable canvas
  — a first-party engine, no third-party runtime dependency. Drag nodes (the connected edges follow
  live, the position is committed once on drop through `@bind-Nodes` and `OnNodeDragStop`), pan by
  dragging the background (or anywhere with Space held), zoom with the wheel around the pointer,
  snap to a grid, move a focused node with the arrow keys. `FitViewOnInit`, zoom limits,
  `Readonly`, `@bind-Viewport` and the imperative `FitViewAsync` / `ZoomInAsync` / `ZoomOutAsync` /
  `ZoomToAsync` / `SetCenterAsync` / `SetViewportAsync` round it off, with `FlowBackground` (dots,
  lines, cross), `FlowControls` (zoom, fit, lock) and `FlowHandle` ports. Pan, zoom and drag never
  re-render per frame; a drag that started before the node list was replaced from outside is
  never committed over it.
  - **Connect**: drag from a source `FlowHandle` to a target handle (or Tab to a source handle,
    `Enter` to start connecting, `Enter` on a target to commit, `Esc` to cancel) proposes a
    connection through `IsValidConnection` and `OnConnect`; without a handler the canvas appends
    the `FlowEdge` itself via the new `@bind-Edges`/`EdgesChanged`. `NodesConnectable` and
    per-node `Connectable` gate it, together with `Readonly` and the controls' lock toggle.
  - **Selection**: click, `Shift`/`Ctrl`-click to toggle, shift-drag a marquee, click an edge —
    all through `ElementsSelectable`/`SelectionOnShiftDrag`, `OnSelectionChanged`, and the new
    `SelectAsync`/`ClearSelectionAsync` methods.
  - **Delete**: `Delete`/`Backspace` (configurable via `DeleteKey`) removes the selection; without
    `OnDelete` the canvas removes it itself (and every edge touching a removed node), honouring
    `Deletable` on nodes and edges.
  - **Edges**: `EdgeLabelTemplate` / `FlowEdge.Label` positioned at the path midpoint and moved
    live during a drag, an arrowhead via `MarkerEnd`, `Animated` (reduced-motion aware) and
    `Dashed` strokes, a wider invisible hit corridor so a thin edge is actually clickable, and a
    default stroke colour that reads in light mode.
  - **Overlays**: `FlowMiniMap` (a scaled overview with the viewport rectangle; click or drag to
    pan), `FlowPanel` (a plain corner overlay for your own content) and `FlowNodeToolbar` (floats
    above the single selected node and follows it while dragging).
  - `NodeAriaLabel` names a node for assistive technology beyond its `Id`.
  - Docs at `/components/flow-canvas`; a live catalog showcase card.
  - **Auto-layout**: `FlowLayout.Tree` (rooted tree/forest, children centred under their parent)
    and `FlowLayout.Layered` (longest-path ranking + barycenter crossing reduction), both pure
    functions returning a new node list — apply with `Nodes = FlowLayout.Tree(...)` then
    `FitViewAsync()`. Both break cycles deterministically (DFS back-edge removal) so a cyclic
    graph still lays out instead of looping; `FlowLayoutOptions` (direction, spacing) and the new
    `FlowCanvas.MeasuredSizes`/`CurrentEdges` read-only properties round it off.
  - **`FlowHistory`**: an undo/redo stack of node+edge snapshots (capped, default 50). Set it on
    `FlowCanvas.History` and every committed change (drag, keyboard move, connect, delete, an
    externally applied replace such as a layout result) pushes exactly once; `Ctrl+Z` undoes,
    `Ctrl+Y`/`Ctrl+Shift+Z` redoes while the canvas has focus, and the new `UndoAsync`/`RedoAsync`
    methods are there for your own buttons.
  - **Reconnect**: dragging an existing selected edge's end onto a different handle (small grab
    handles at each end, `data-flow-edge-end`) moves that end — validated through
    `IsValidConnection` the same way a fresh connection is; without an `OnReconnect` handler the
    canvas updates the edge itself. `EdgesReconnectable` gates it; `Esc` cancels mid-drag.
  - **Touch and pinch**: one-finger pan/drag (immediate, no long-press) and two-finger pinch-zoom
    anchored on the midpoint between the fingers, all through the same Pointer Events code path.
  - **Snap-grid toggle**: `FlowControls`' new `ShowSnapToggle` adds a grid-icon button bound to
    `SnapToGrid` (now two-way bindable, `@bind-SnapToGrid`); `ShowHistory` adds undo/redo buttons
    bound to the canvas' `History`.
  - The keyboard-connect aria-live announcements ("Connecting from…", "Connected.", "Connection
    rejected/cancelled.") are now localized (`ILumeoLocalizer`) instead of English-only, across all
    14 bundled locales.
  - **`FlowNodeResizer`**: corner and edge grips on a node template (`MinWidth`/`MinHeight`/
    `MaxWidth`/`MaxHeight`, Shift keeps aspect on a corner grip), live in `flow.js` with connected
    edges following, committed once through `NodesChanged` + `OnNodeResizeStop`; a node carrying a
    resizer also gets `Shift+Arrow` keyboard resize on its focused host (other nodes keep the
    existing `Shift` = ×10 move).
  - **Helper lines**: `HelperLines`/`HelperLineThreshold` snap a single dragged node to the nearest
    other node's left/centre/right and top/middle/bottom, drawing the matching guide(s) live in the
    edge SVG layer; works with `SnapToGrid` off.
  - **Clipboard**: `Ctrl+C`/`Ctrl+V`/`Ctrl+D` on the focused canvas copy/paste/duplicate the
    selection (internal edges between two selected nodes included), pasted at a `(20, 20)` offset
    with new ids from `NewNodeId`; `OnPaste`; honours the same editable-target guard the other
    shortcuts do.
  - **`ConnectionMode`**: `Strict` (default, drag must start on a source handle and land on a
    target) or `Loose` (any handle to any other handle, direction inferred from the drag).
  - **Edge label inline editing**: `EdgeLabelEditable` — double-click a label opens an inline Lumeo
    `Input`, `Enter` commits (`EdgesChanged` + `OnEdgeLabelChanged`), `Escape` cancels, the commit is
    announced to a screen reader.
  - **Export/import**: `FlowDocument` (`Nodes`, `Edges`, `Viewport`) plus `ToDocument()` /
    `LoadDocumentAsync()` round-trip the whole canvas through JSON; `ExportSvgAsync()` builds a
    self-contained vector SVG purely from canvas state (never fails); `ExportPngAsync(scale)` is a
    best-effort raster export of the live node DOM (computed styles inlined through an SVG
    `foreignObject` onto a `<canvas>`) — returns `null`, never throws, when the browser refuses
    (a cross-origin image/font tainting the canvas is the usual cause).
  - **`ValidateOnHover`** (opt in): runs `IsValidConnection` once per hovered target handle during a
    connect drag instead of only on drop, marking a would-be-rejected target `data-flow-handle-invalid`.
  - **Multi-selection `FlowNodeToolbar`**: with two or more nodes selected, one toolbar now renders
    at the selection's bounding box (previously nothing rendered above two-plus selected nodes).
  - The agent-tree block (`/blocks/flow-agent-tree`) now lays itself out with `FlowLayout.Tree`
    instead of a hand-written recursive layout.
  - Touch/pinch (shipped in phase 3a) now has real E2E coverage via CDP `Input.dispatchTouchEvent`.
  - **Sub-flows / groups**: `FlowNode.ParentId` puts a node inside a group node — its `X`/`Y` become
    relative to the group, it paints above the group (edges between nested nodes too) and moves
    with it: dragging a group moves its whole subtree live. `FlowNode.Extent = FlowExtent.Parent`
    keeps a child inside its group while dragged or arrow-key moved. `FlowGroupNode` is the default
    group chrome (label, tinted box, resizable; a west/north resize keeps the children in place).
    `Ctrl+G` / `Ctrl+Shift+G` (and `GroupSelectionAsync` / `UngroupSelectionAsync`) group and
    ungroup the selection; deleting a group deletes its subtree (React Flow's behaviour — a
    non-`Deletable` child survives, re-parented one level up); copy/duplicate carry a group's
    children; `FlowLayout.Tree`/`Layered` lay out per group (new `GroupPadding`/`GroupHeaderHeight`
    options); edges, fit-view, the minimap, marquee, helper lines and export all use absolute
    positions (`GetAbsolutePosition`, `FlowGeometry.GetAbsolutePositions`); a group is announced
    with its child count. `ParentId`/`Extent` were appended as optional trailing parameters and the
    twelve-member `Deconstruct` is kept, so existing code compiles unchanged; `FlowDocument`
    round-trips them.
  - **Virtualization**: `OnlyRenderVisibleNodes` mounts only the nodes inside the viewport plus one
    viewport of margin (with their parent groups) and the edges touching them, and follows pans and
    zooms from the engine's viewport reports with hysteresis and an 80 ms throttle. Measurements
    survive an unmount; selection, delete, clipboard, keyboard moves and a drag of the selection
    still reach off-screen nodes; fit-view and the minimap work on every node. A 2000-node E2E
    fixture paints its first node about 130 ms after navigation (budget 1.5 s) and loses no
    viewport report during a long pan.
  - Docs: sub-flow and 1,500-node demos; the small demos (three nodes or fewer) cap `MaxZoom` at
    1 so fit-view no longer blows two cards up to 200%.
- **DataGrid: `ColumnSizing` parameter** (`DataGridColumnSizing.Auto`, the default, or
  `FitWithMinimum`). `Auto` is the historic `table-layout: fixed` behavior, under which a
  `FillWidth` column can be squeezed to 0px with enough other columns visible — fixed table
  layout does not honor CSS `min-width` on cells. `FitWithMinimum` fills the available width when
  there's room and never shrinks a column below its own `MinWidth` (falling back to `Width`);
  once the visible columns' combined floor exceeds the container, the grid scrolls horizontally
  instead of squeezing a column away (consumer report).
- **DataGrid: bulk column auto-size, `ResetColumnWidthsAsync`, and `OverlayScrollbar`.**
  `AutoSizeAllColumnsAsync()` fits every visible, resizable column to its content (header + the
  currently rendered rows — under virtualization, just the rendered window) in one pass, clamped
  to each column's `MinWidth`/`MaxWidth` and committed through the same width state a manual
  resize uses (persisted with the layout when `LayoutStorageKey` is set); also reachable from a
  new "Autosize all columns" button in the Columns panel. `AutoSizeColumnAsync(field)` does the
  same for a single column — the public-API counterpart to the column menu's existing per-column
  "Fit to content" entry. `ResetColumnWidthsAsync()` restores every column's declared width
  without touching sort, filter, visibility, pin or order, narrower than the existing
  `ResetLayoutAsync()` (consumer report). Separately, the new `OverlayScrollbar` parameter hides
  DataGrid's native scrollbar and draws thin, theme-matched overlay scrollbars on both axes
  instead (draggable thumbs, appear on hover/scroll, RTL-safe, `prefers-reduced-motion`-aware,
  reserves no layout space) — off by default, so existing consumers see no change (consumer
  report).
- **DropdownMenu/ContextMenu/Menubar: content scrolls a long list itself.** `DropdownMenuContent`
  (and `ContextMenuContent`/`MenubarContent`, which share the same overflow-visible/fixed-submenu
  pattern) now cap an inner item viewport to the live available viewport space in the placement
  direction — `--lumeo-dropdown-available-height`, positioning JS's parity with Radix's
  `--radix-dropdown-menu-content-available-height` — and scroll past it, overridable per-panel
  with the new `MaxHeight` parameter. The outer panel stays `overflow-visible` so a submenu
  (`position: fixed`) keeps escaping it fully unclipped, even from an item deep in the scrolled
  list — no more reaching for a consumer-supplied wrapper, which could reintroduce clipping by
  giving the panel a containing block (#520).
- **Popover: content can follow the trigger's rendered width.** `PopoverContent.MatchTriggerWidth`
  reuses the positioning JS's existing trigger-width-matching plumbing (already used by
  Select/Combobox/TreeSelect) so the panel's width tracks the trigger live instead of needing a
  manual `Class="w-full"` alongside `PopoverTrigger AsChild`. The trigger's rendered width is also
  published as `--lumeo-popover-trigger-width` (Radix's `--radix-popover-trigger-width` pattern)
  on the content element regardless of the flag, for a custom `Class` to derive its own width from.
  Default unchanged (#518).

### Fixed
- **DataGrid: `ApplyLayoutAsync` reloads exactly like a header click.** A layout applied with a
  new sort now raises `OnServerRequest` in `ServerMode` and re-sorts the bound list in client
  mode; a grid using server-side row virtualization (`Virtualized` + `OnRangeRequest`) now routes
  through `RefreshVirtualizedAsync`, matching `HandleSort`/`HandleFilter` — previously it silently
  fell through to `ProcessClientData` against an `Items` list that, by design in that mode, isn't
  the full set (consumer report).
- **DataGrid: a `LayoutStorageKey`-persisted layout is now restored before the initial
  `ServerMode` request**, instead of after it — the header no longer briefly shows the saved sort
  while the rows are still the first (default-sort) request's, and `ServerMode` no longer pays for
  two round trips on first load (consumer report).
- **DataGrid: the selection checkbox column is sticky-left by default** whenever the grid scrolls
  horizontally, not only when another column is *also* pinned left (consumer report).
- **DataGrid: `DataGridColumnDef.Title` reactivity now also covers the `Columns` PARAMETER path**
  (a host-rebuilt `List<DataGridColumn<TItem>>`, e.g. to translate headers on a UI-language
  switch) — previously only declarative `DataGridColumnDef` children picked up a changed `Title`;
  the `Columns`-parameter path never did, because column identity there was compared by
  `DataGridColumn.Id` (a random per-instance GUID) instead of `Field`, so a freshly rebuilt list
  looked like an entirely different column set every render (consumer report).
- **DataGrid: the row divider no longer renders blurry** on some displays/zoom levels. Rows now
  draw it as an inset `box-shadow` instead of `border-b`, paired with `border-separate` on the
  table, so it isn't subject to a `border-collapse` merge landing on a sub-pixel boundary;
  `Bordered="true"` grids are unchanged (that variant relies on `border-collapse` merging cell
  borders into single-pixel grid lines) (consumer report).
- **Sidebar: `SidebarMenuButton`'s icon-mode size/padding no longer ship `!important`.** A
  consumer's own `Class="group-data-[collapsible=icon]:px-0"` (or similar) used to lose to the
  library's `!important` regardless of being merged in last — `Cx.Merge`/`TailwindMerge` only
  lets a plain token evict an `!important` owner by also being `!important`, which forced
  consumers into their own `!` and then a raw CSS-specificity fight Lumeo doesn't control.
  Default (uncustomized) visuals are unchanged (consumer report).

## [5.10.5] - 2026-09-23

- **The Dutch locale is complete.** 166 of the 632 keys had no `nl` entry, so a Dutch UI fell back
  to English for the whole Editor, the AI primitives (`AgentMessage`, `AgentMessageList`,
  `PromptInput`), `QueryBuilder`, `PickList`, the theme switcher, the `TimePicker` labels, the
  `FileManager` views and a few dozen aria-labels. Four existing strings are corrected
  (`DataGrid.Global` read "Globaal", which means "roughly"; `Filters.Reorder` and its hint now use
  the verb the grid and Gantt already use; `Gantt.ResizeTreePane` in natural word order) and the
  duplicated `DataGrid.SortAscending`/`SortDescending` entries are removed. The key catalogue on
  `/docs/localization` is regenerated.

## [5.10.4] - 2026-09-16

- **Tooltip/Popover placement**: overlays anchored inside wide scrolled containers no longer measure their
  wrapped static-position size (a `position: fixed` box measured before `left`/`top` are set wraps at its static
  position deep inside the canvas and inflates its height), so Gantt bar tooltips sit on their bars; the anchor is
  also clipped to the visible part of a trigger that is partially scrolled out of its pane.
- **`GanttChart`: Ctrl/Cmd+wheel zoom no longer jumps before it anchors.** The date under the
  cursor is meant to stay under the cursor across a zoom, but the anchoring scroll position was
  applied by a second round trip issued after the render that had already repainted every bar at
  the new scale — so the chart showed one frame (locally ~30ms, on a real circuit a full
  round-trip) of the new zoom under the old scroll position, the anchored date visibly jumping by
  the whole scroll delta and snapping back afterwards. The timeline now carries the anchor
  position in that same render, so the scroll lands in the same browser frame as the geometry it
  anchors; the interop call still follows and writes the identical pixel as an idempotent
  backstop. Fixes the intermittent `CtrlWheel_Anchors_The_Zoom_On_The_Pointer_Not_The_Viewport_Center`
  failure (issue #385).

## [5.10.3] - 2026-09-16

- **Gantt bar labels stay readable across the progress fill.** The completed part of a bar was
  an opaque dark fill with the label drawn inline across it, so a label vanished wherever it
  crossed the fill; the fill is now a translucent overlay of the bar colour in both the
  `GanttChart` (v3) and the legacy `Gantt` (v2) renderer, every in-bar label carries a thin
  halo in the background colour (so it reads on any fill, also where the theme's primary
  equals its foreground, as in zinc dark), and the horizontal scrollbar no longer overlays the
  last row.

- **DataGrid: `FilteredRowCount`, `TotalRowCount` and `OnRowCountChanged`.** The count of rows
  that survive the grid's own filters and search (before paging; the server total in
  `ServerMode` and row virtualization) and the source count, plus an event so a host can
  render "n of m" that follows the built-in search. The pagination summary and the
  filtered-empty state read the same numbers.
- **DataGrid: the row-detail panel spans the visible viewport** (`DetailStickyToViewport`,
  default `true`). On a grid wider than its container the detail `<td>` was as wide as the
  table's scroll width, so a detail panel laid itself out mostly off-screen and its text looked
  cut off until the user scrolled right. The grid now measures its scroll container with a
  ResizeObserver and the detail wrapper sticks to the left edge at that width, so a detail
  template always reads within what is on screen.

## [5.10.2] - 2026-09-16

- **"Menu Color" follows the active theme and stays out of embedded previews.** The setting
  wrote eight zinc-grey `--color-sidebar*` values as inline properties on `<html>`, so every
  sidebar on the page (docs demos, the dashboard block, the home teaser) turned zinc-dark, and a
  coloured theme's own dark sidebar was ignored; a live switch also wrote only two of the eight
  tokens. It is now a `data-menu-color` attribute, every theme file carries its light and dark
  sidebar sets as private `--_sidebar-{light,dark}-*` variables, and "Dark" means that theme's
  dark sidebar. `SidebarProvider.IsolateMenuColor` (used by the docs demos) keeps an embedded UI
  on the mode's own set; a consumer's `--sidebar*` override keeps winning. The customizer's reset
  clears the setting completely, and an old inline value is cleaned up on the next load.

## [5.10.1] - 2026-09-15

- **`SvgGlyph` has a real `Class` parameter.** It took `class` only through the attribute
  splat, so a call site writing `Class="h-4 w-4"` set an attribute literally named `Class` on
  the `<svg>`, which on an SVG element is not `class`: after WASM hydration the glyph had no
  size and filled its button. Visible on every `DropdownButton` and `SplitButton` chevron (53px
  in a 32px button); the prerendered HTML hid it because the HTML parser lowercases the name.
  `Class` and a splatted `class` now merge, and the two common cases allocate nothing extra.
- **`IconPicker`'s popover scales with `Size`.** The search box is the Lumeo `Input`
  (`Variant=Search`, same rung as the trigger, Lumeo focus ring), and one ladder drives the cell,
  glyph, gap, row height (so `Virtualize` never jumps) and the popover width for all seven rungs.
- **Composites use Lumeo controls, not native ones.** The DataGrid docs' custom cell editor, the
  QueryBuilder's field/operator/value pickers and the Scheduler dialog's calendar picker
  rendered a raw `<select>`; they are `Select` now, sized to their surroundings, with the
  QueryBuilder placeholders localized. The Scheduler's calendar field takes its id from
  `FormField`, so the click-outside handler finds the trigger again.
- **Search boxes inside composites are the Lumeo `Input`.** Cascader, TreeSelect, Transfer,
  PickList, TreeView and the DataGrid toolbar rendered their own `<input>`; each is now
  `Input` with `Variant=Search` at the rung that matches its previous height, so density and
  future Input changes reach them and keyboard navigation is unchanged. The Scheduler's
  appointment editor uses `DatePicker` (all-day) or `DateTimePicker` (timed) for start and
  end instead of the browser's native date inputs.

## [5.10.0] - 2026-09-15

Field report #464 (a Blazor WASM product on 5.0.0) is worked through in this release: the four
new findings and the carried list, each either fixed, added, or verified and pinned with a test.

- **`IconPicker`**, a searchable, virtualized icon grid behind a `Popover` trigger (field report
  #464). It is pack-agnostic: it takes an `IReadOnlyList<IconPickerItem>` (name, `IconSource`,
  optional keywords) built from any installed `Lumeo.Icons.*` pack, the docs show the one-line
  reflection helper. Rows virtualize past 500 icons, search matches name and keywords, the arrow
  keys move a roving focus across the grid, and it carries `Clearable`, `ShowLabel`, the full
  `Size` scale and `FormField` integration.
- **`Size` on `DatePicker`, `DateRangePicker`, `TimePicker` and `DateTimePicker`** (field report
  #464). All seven rungs set the trigger's height, text and padding on the ladder `Input` uses,
  so a picker sits flush next to an input at the same rung; the calendar and time columns keep
  their own tokens. `DateTimePicker` also picks up ambient `Density` like its siblings.
- **`OverlayOptions.PlayExitAnimation` and `AlertDialogOptions.PlayExitAnimation`** (default
  `true`, field report #464 finding 4). A service-opened Dialog, Sheet, Drawer or AlertDialog
  can opt out of its exit animation and unmount at once, the lever the declarative components
  already had; `SheetContent` gained the parameter directly.
- **DataGrid: `HasActiveFilters`, `ClearFiltersAsync()` and a typed `EmptyTemplate`** (field
  report #464). A client-mode grid whose rows are all filtered away shows a localized "No rows
  match the current filters" state with a clear action instead of the plain empty state, and a
  custom empty template receives the same signal.
- **`DataGridColumnDef.Editable`** locks one column out of Cell and Batch editing while the rest
  of the row stays editable; **`CellEditContext.Commit()` / `Cancel()`** let a custom
  `EditTemplate` close its cell, and opening another cell commits the one that was open, as the
  built-in editors do (field report #464).
- **Drawer: `Modal`, `ShowCloseButton` and `ScaleBackground`** (#346). `Modal="false"` drops the
  backdrop, focus trap and scroll lock while `PreventClose` keeps meaning "not dismissible",
  vaul's split; `ShowCloseButton` mirrors `SheetContent`'s; `ScaleBackground` scales and rounds
  the element marked `data-lumeo-drawer-wrapper` behind an open bottom drawer.

### Changed
- **Skeleton bars are silent by default.** Every `Skeleton` and `SkeletonCircle` rendered
  `role="status" aria-label="Loading"`, so a card of six bars was six English live regions
  whatever the culture (field report #464). A bar is now `aria-hidden`; opt one bar into a
  single announcement with the new `Announce` parameter or an explicit `AriaLabel`, and the
  default label comes from the localizer (`Skeleton.Loading`). A consumer who relied on the
  per-bar status role needs the opt-in.
- **Radio and checkbox menu items close the menu on selection.** `DropdownMenuRadioItem`,
  `DropdownMenuCheckboxItem`, `ContextMenuRadioItem` and `MenubarRadioItem` close like
  `DropdownMenuItem` and Radix (field report #464); `MenubarCheckboxItem` stays open on
  purpose, for flipping several toggles in one visit. The same pass fixed
  `ContextMenuRadioGroup` losing an uncontrolled selection on an unrelated re-render.
- **A `Scheduler` still bound to the removed `OnInitError` throws** an
  `InvalidOperationException` at render that points at the migration note, instead of the
  parameter vanishing into `AdditionalAttributes` (field report #464). Every remaining JS
  registration degrades gracefully when `scheduler-views.js` is missing.
- **The build references `Microsoft.SourceLink.GitHub` 10.0.401**; the 8.0.0 build task fails
  NuGet's audit (CVE-2026-62900) and, with warnings as errors, every clean restore.

- **`Select` with `Searchable` filters composed `<SelectItem>` children** (field report #464,
  finding 1). The filter only ran over `Items`, so a select composed from children went empty on
  the first keystroke. Composed items now filter on their rendered label, then an explicit
  `SearchValue`, then `Value`. A label wrapped in another component (`<Text>`, `<Badge>`) still
  needs `SearchValue`; that limit is documented on the parameter and pinned by a test.
- **`OtpInput` keeps focus while deleting** (field report #464, finding 2). The focus moves after
  typing, backspace, paste and a rejected character are issued from `OnAfterRenderAsync`, so
  they target the re-rendered box instead of racing the render that recreates it.
- **A slow swipe-to-close on a `Sheet` no longer snaps back to fully open** before the exit
  animation (field report #464, finding 3). The slide-out starts from the drag offset at
  release, through a custom property the keyframes read.
- **The DataGrid filter chip shows the column `Title`**, not the raw field name (field report
  #464), and **a cell editor's keystrokes no longer reach row selection**: a space typed while
  editing toggled the row in multi-select mode.
- **A `<form>` placed directly inside `DrawerContent` no longer collapses to 0 height**, and
  the bottom drawer's `mt-24` peek margin applies to the bottom side only (field report #464,
  #346).
- **A horizontal `Stepper` with long labels no longer forces the page wider** (field report
  #464); the header rail scrolls within itself and labels truncate.
- **Charts: pie, donut and nightingale slices no longer paint the same colour** (field report
  #464). The theme's per-slice gradient callback read a palette value ECharts never populates
  once a colour callback is registered; slices now get an explicit per-item gradient cycling
  through `Colors`, `ColorPalette` or the theme palette.
- **Charts: an `OptionOverride` key that collides with a generated key (`series`) replaces it**
  instead of being emitted twice, and `ChartAccessibility` degrades to no table instead of
  throwing on the duplicate (field report #464). `AreaChart` without `Colors` and `var(--token)`
  colours were verified working and pinned with tests.
- **Scheduler:** the sr-only announcer no longer escapes the card into an ancestor's scroll
  area; week, day and range titles follow the culture's day/month order; the appointment
  tooltip shows the resource's title instead of its id (field report #464).
- **Gantt v3 keeps a move region on bars narrower than 12px**; the resize handles shrink with
  the bar instead of covering it. The docs bar that "refused to drag" (#400) is the demo's own
  commit gate rejecting a start before today, and the double scroll-to-today (#390) was already
  fixed; both are pinned with tests.
- **A `GanttBar` whose keyboard/drag registration changes while an earlier registration is
  still in flight no longer issues a redundant interop call** (#413). Each registration stamps
  a generation before its first await and a superseded one bails out, so exactly one performs
  the correction whatever the completion order; the CI-only flake this produced is pinned by a
  deterministic two-gate test. Two toast stacking tests get wider real-clock margins (#447).
- **A select editor of `Filters` is as wide as its widest option** (field report §16.6). The
  panel was a fixed 12rem, which cut long labels such as a document type with its code in
  half. It is now `w-max` between 12rem and 36rem (and never wider than the viewport); past the
  ceiling the labels truncate as before. A field that sets any width of its own through
  `FilterField.Class` (`w-40`, `min-w-max`, `max-w-[48rem]`) gets exactly that, as before.
- **The column-resize guideline spans the grid's scroll container, not the table** (field
  report §18.4). Under virtualization the table is as tall as every row and its top sits far
  above the viewport once scrolled, so the fixed-position line ran from above the toolbar to
  below the window. It now takes the scroll container's rectangle, clamped to the viewport.
- **`lumeo-classes.txt` lists the classes inside `class="@Cx.Merge("…", Class)"`.** The
  extractor paired quotes per line, and in that shape the class string sits between a closing
  and an opening quote, so a class used only there was missing from the manifest a consumer's
  own Tailwind build reads (card spacing, the filter cells, gradients). A second pass takes
  every literal that follows a `(` or `,`.

### Upgrade notes
- **Re-version `lumeo.css` when you upgrade.** The stylesheet changes in most releases and
  browsers cache it; append a version query or hash (`lumeo.css?v=5.10.0`, or
  `asp-append-version="true"`), otherwise the old stylesheet stays in place and components
  render with stale rules (field report #464).
- **Not reproduced:** "component-internal localized defaults do not follow a runtime language
  switch". Dialog, DatePicker, Select, Pagination and DataGrid re-resolve their default strings
  on the next render after `CultureInfo.CurrentUICulture` changes, now pinned by tests; a
  repro on the issue is welcome.

## [5.9.1] - 2026-09-04

- **The inline filter editor's popover is one control tall.** The text and number editors sat as
  a bordered group inside a padded popover, so the popover stood 16px taller than the chip it
  edits and framed the field twice; the group's dark focus frame made it heavier still. The
  popover is now the field's only frame: no panel padding, no border or ring of the group's own,
  so the editor is the chip's height plus the popover's 1px border; keyboard focus shows as a
  1px inset line at a quarter strength.
- **The editor's X (discard) and an apply that changes nothing close the editor.** Both reach
  the chip from the editor's own click handler, which re-rendered the editor and not the chip,
  so the popover stayed open; the chip and the advanced row now re-render when they close it.
- **Class strings in `.cs` helpers reach the CSS bundles.** Both Tailwind builds (the package's
  `lumeo-utilities.css` and the docs) scanned `UI/**/*.razor` only, so a class that lived in a
  helper such as `FilterStyles` shipped only when a `.razor` file happened to repeat it; the
  source globs now include `UI/**/*.cs`.

## [5.9.0] - 2026-09-04

- **`Filters.MenuActions` and `FilterField.MenuActions`** (field report §16). The entries of a
  rule's menu as flags (`Duplicate`, `Negate`, `Group`, `Remove`); the bar sets them for every
  rule, a field narrows them, a rule shows the intersection, and a chip drops its menu segment
  when nothing is left. A host whose API knows no negation drops `Negate` instead of showing an
  entry that does nothing.

- **The add-filter button of `Filters` shows Lucide's `list-filter-plus`** (the filter lines with a
  plus) instead of the plain filter glyph, so the collapsed icon-only button reads as "add".
  `LumeoIcons` gains `ListFilterPlus`.

- **The inline text and number editors of `Filters` are one control.** The input and its
  apply / discard buttons used to be three boxes glued together, each with its own border and
  radius, and the input's focus ring drew a heavy dark outline around the input alone. The
  border, the radius and a soft focus ring now sit on the group; the input and the buttons
  inside are flat, the buttons divided by a hairline. The editors' inputs also sit on the chip
  geometry now (the bar's control height, 14px text, the chips' padding): at `Size="Sm"` the
  Input component's own rung is 32px with 12px text while the chips beside it are 28px with
  14px. `Input` itself learned two things on the way: a font-size override in `Class` reaches
  the inner field of the wrapped (number, prefix, suffix) variant instead of losing to its
  `md:text-xs`, and that inner field shrinks with its wrapper (`min-w-0`) instead of
  overflowing it.

- **Shift-click on the selection checkbox selects the range** (field report §16.2, follows
  #440). The Checkbox's own click ran before the cell's, toggled the row and re-anchored the
  range on it, so the cell's Shift handler then filled a range of one: exactly the two clicked
  rows, in every mode. The cell now reads Shift on pointerdown and the checkbox path extends
  from the anchor itself; a Shift-click on a row already inside the range leaves its checkbox
  checked (the Checkbox's own optimistic flip is re-applied). Covered for a client grid, a
  `ServerMode` grid with `OnServerRequest` and no virtualization, and `SelectOnRowClick="false"`.

## [5.8.0] - 2026-09-03

- **`Filters`, a filter builder in `Lumeo.DataGrid`** (field report §15, ReUI's Filters ported).
  Add a filter, pick the attribute in a searchable list (nested attributes drill down), then the
  condition and the value inside the chip; the chip's menu duplicates, negates or removes.
  Seven value kinds with their operator catalogue (text, number, range, date, select,
  multiselect, boolean), an editor per kind, async options with debounce, paging and retry
  (`LoadOptions`, `ResolveValues`), an exclusive "none" option, custom editors and value
  rendering, a size ladder on the geometry tokens, `ReadOnly` and `Disabled`, keyboard
  (arrows, Home/End, Delete, Enter, Alt+arrows to reorder) and live announcements. The model is
  a query tree (`FilterQuery`, `FilterRule`, `FilterGroup`) with pure operations in
  `FilterQueries` and `Flatten()` for an API call; every string comes from the `Filters.*`
  locale keys. `Command` gains `SearchChanged` and `ShouldFilter` for lists that filter or
  load their items themselves.
- **`Filters` advanced variant** (`Variant="Advanced"`): a nested condition builder, inline or
  behind a trigger that counts the rules. Rows with a combinator per group (Where / And / Or,
  the second row toggles it), groups that nest, a text or number value edited in place, every
  other value in its editor, a menu that duplicates, negates, wraps a row in a group, moves it
  to another group or removes it, a group menu that ungroups. `Reorderable` adds drag handles
  (a pointer drag between groups, Alt copies) and Alt with the arrow keys; the keyboard
  travels the cells with arrows, Home and End, Delete removes a row. A field's `Validate`
  message shows on the row once its value was edited. `IComponentInteropService` gains
  `RegisterFilterDrag` / `UnregisterFilterDrag`. A saved query round-trips through
  `System.Text.Json` (`$type` on the nodes, values back in their shape); `Filters.CollectIssues()`
  reports everything that keeps a query from running, a field's `Validate` message included;
  a field's `DefaultOperator` skips the condition step; the date editor reads the phrases its
  locale shows (`heute`, `in 2 Wochen`); a chip's field segment reopens the attribute picker;
  a restored query naming a field or operator the schema no longer has is reported as an issue
  (`UnknownField`, `UnknownOperator`); a `DateOnly` or `DateTime` value keeps its type through
  JSON.

- **Shift-click selects a range in virtualized server mode.** With `Virtualized="true"` and
  `OnRangeRequest`, rows go from the provider straight into `<Virtualize>` and the grid holds
  no row list, so a Shift-click could not find the anchor and quietly toggled the one row
  instead. The range is now fetched by index through `OnRangeRequest`, so it can reach beyond
  the rows currently loaded; a sort, filter or search change drops the anchor. `DataGrid`
  gains `SelectRangeAsync(fromIndex, toIndex)` for a host that runs its own selection UI, and
  `DataGridContext.ToggleSelectionModifiedAt` carries the row index.

## [5.7.0] - 2026-09-03

The items of the DocFlow field report that were still open after 5.6.0, re-checked
against the current source. Several entries the report still lists as open had been
shipped between 5.1.0 and 5.6.0 and are only re-confirmed here; the ones below changed.

- **DataGrid `SelectOnRowClick`** (field report 1.8). `false` keeps selection on the checkbox
  column and leaves the row click to `OnRowClick`, e.g. to open a detail view.
- **DataGrid `ShowReorderHandle` / `ShowPinButton`** (1.11). Hide the grip and the pin button;
  dragging the header and the column menu still reorder and pin.
- **`DataGridColumnDef.DefaultSort`** and `DataGridColumn.DefaultSort` (1.10). The sort a
  column starts with; a saved layout still wins, and in server mode the initial request
  carries it.
- **DataGrid `SortIconTemplate`** (1.15). Your own glyphs for none / ascending / descending.
- **DataGrid `FooterTemplate`** (1.9). A footer strip with the displayed row count, the total
  and the displayed items, with or without column aggregates.
- **DataGrid `ColumnMenuContent`** (1.18). Entries appended to the column menu after a
  separator, so a host command sits next to sort, fit, pin and move.
- **Calendar geometry tokens** (4.10). `--lumeo-calendar-cell-size` (32px) and
  `--lumeo-calendar-p` (12px) size the day cells and the frame; the week grid follows.

- **Badge's default rung is shadcn's badge** (2.2): `px-2 py-0.5 text-xs` on a 16px line,
  22px tall, so descenders no longer clip inside a truncating span. The larger rungs moved
  up one step to keep the ladder ordered. 5.0 to 5.6 sat on reui's 20px `leading-none`
  badge.
- **Tabs root is `flex gap-2`** (4.15), column-wise when horizontal, as shadcn's: the 8px
  between the list and the panel come from the root, `TabsList` is `w-fit` and
  `TabsContent` is `flex-1`. Pages that added their own margin can drop it.
- **CardHeader `gap-2` and `[.border-b]:pb-*`, CardFooter `[.border-t]:pt-*`**, shadcn's
  current card.tsx.
- **AccordionTrigger is `items-start gap-4 rounded-md`**, shadcn's accordion.tsx.
- **Row separators are the border colour** (1.13). `TableRow`, `TableHeader`, `TableFooter`,
  the table skeleton and DataGrid rows use `border-b` / `border-t` instead of
  `border-border/40`, as shadcn's table.tsx does.
- **Menus no longer clip** (4.4.1). DropdownMenu, ContextMenu and Menubar contents are
  `overflow-visible`: they have no scroll height of their own, and a sub-menu is a DOM child
  that `overflow-hidden` clipped whenever a host's CSS gave the content a containing block.

- **A single-value Select shows its label** (4.1b). A `SelectTrigger` without custom
  content rendered nothing for the selected value, against its own documentation. It now
  shows the item's label, and a closed composition-mode Select registers its items so a
  pre-selected value reads right before the list was ever opened.
- **`DataGridColumnDef.Title` is reactive** (1.5). A changed title reaches the header, so a
  runtime language switch no longer needs `@key` on the grid.

### Docs
- Localization: the `BlazorWebAssemblyLoadAllGlobalizationData` property a runtime culture
  switch needs (5.1).
- ToggleGroup: bind `SelectedValues` to an `IEnumerable<string>` (4.6). Popover: what
  `Class` on the anchor wrapper does (4.5, 4.8). Field: the alias for a host with its own
  `Field` type (4.16). Calendar and theme overrides: the new tokens.

## [5.6.0] - 2026-09-02

The remaining items from the DocFlow field report.


- **`SideOffset` and `AlignOffset`** on `DropdownMenuContent`, `DropdownMenuSubContent`,
  `ContextMenuSubContent`, `PopoverContent` and `HoverCardContent` (Radix's `sideOffset` /
  `alignOffset`), and `AlignOffset` beside the existing `Offset` on `TooltipContent`. The
  defaults (4px gap, no shift) are what placed the contents before, so nothing moves.
  `IComponentInteropService.PositionFixed` gains the align-offset overloads as additive
  default members. Field report 4.4.
- **The localization key catalogue.** The localization page lists every one of the 500
  component-string keys with its English default, the German text and how many of the 14
  shipped locales carry it, generated from the source by
  `scripts/i18n/generate-key-catalog.py`, with a section on adding a locale Lumeo does not
  ship through `LumeoLocalizationOptions.AddMany` (Croatian as the example). Field report
  1.19 / 1.20.


- **A click on a modal backdrop no longer takes the keyboard out of the dialog.** The
  backdrop moved focus to the page body, or to an outer dialog when one dialog sat inside
  another, so the next Escape closed nothing or the wrong dialog; an `AlertDialog`, whose
  backdrop does not dismiss, stranded the user. Dialog, AlertDialog, Sheet and Drawer
  backdrops cancel the mousedown default now, so focus stays inside and Escape closes the
  innermost dialog first. Reproduced headless with a dialog nested in a dialog, both as a
  sibling and declared inside `DialogContent`. Field report 4.14, #444.

## [5.5.0] - 2026-09-02

Issue #434, in the order the field report suggested: stable override contracts first,
then a small fixed token set. Nothing renders differently until you use them.


- **`data-slot` on every component.** All 324 element-rooted components mark their root
  with `data-slot="<component-in-kebab-case>"` (`button`, `sidebar-menu-button`,
  `dropdown-menu-item`, ...); a component with two root branches marks both, and form
  controls whose root is the field wrapper also mark the control itself as
  `<name>-control` (`input-control`, `textarea-control`, `checkbox-control`, ...). A
  stylesheet written against `[data-slot=…]` survives a release that renames a utility
  class; one written against `.h-7` does not. Card and Sidebar had this since 5.2 and 5.3.
- **Geometry tokens.** Twelve CSS variables carry the geometry products tune most, each
  read as `var(--lumeo-…, <today's value>)` on the component's Comfortable rung:
  `--lumeo-control-h` (with `-xs`, `-sm`, `-lg`) for Button and every field-shaped control
  (Input, Select, DatePicker, TimePicker, Cascader, TreeSelect, ColorPicker, TagInput,
  NumberInput, PasswordInput), `--lumeo-icon-size` for an unsized icon inside a button,
  menu item, command item, tab trigger or sidebar item, `--lumeo-sidebar-item-h` (with
  `-sm`, `-lg`), `--lumeo-table-head-h` and `--lumeo-table-cell-p`, `--lumeo-grid-cell-px`
  and `-py`. Defaults follow Tailwind's `--spacing`, so a rescaled spacing scale moves them
  too; `Density.Compact` and `Density.Spacious` stay one step below and above as fixed
  presets. Set a token on `:root` for the app or on any ancestor for a region: six lines
  replace a hundred of class-literal overrides. Documented on the theme-overrides page,
  with a guard test that keeps every fallback equal to the declared default.

## [5.4.0] - 2026-09-02


- **`DataGrid` column menu.** `ColumnMenu="true"` turns the header click into the
  shadcn/ReUI column menu instead of a sort toggle: sort ascending / descending (a check
  marks the current direction, picking it again clears), fit to content, pin left / right,
  move left / right. Entries follow the column's `Sortable`, `Resizable`, `Pinnable` and
  `Reorderable` flags; a pinned column does not move. The menu is 160px wide with 28px
  rows, as the reference measures. Field report 1.18.
- **`DataGrid.OnHeaderClick`** fires before the grid's own response to a header click, with
  `ColumnHeaderClickEventArgs.PreventDefault` to take the click over entirely. Field
  report 1.18 asked for exactly this hook.
- **`IComponentInteropService.AutoFitColumn`** runs a column's fit-to-content from code,
  through the same path a double-click on the resize handle takes.
- Six new localization strings for the menu, in all 14 locales.
- **`DataGridColumn.FillWidth`** (and the `DataGridColumnDef` parameter): the column that
  absorbs the grid's free space, as ReUI's `meta.fillWidth` does, so the grid keeps spanning
  its container while every other column stays exactly its width.


- **Column resizing moves one edge only.** With every visible column sized, the grid lays
  out fixed at the sum of the widths: each column renders exactly its `Width` (it used to be
  stretched to fill the container), and dragging a handle changes that column alone, 1:1
  with the pointer. Free space goes to the `FillWidth` column when there is one; without one
  the table may sit narrower than its container, as a TanStack table does. Before, the
  freed space was handed back to every column proportionally as soon as the table was no
  wider than its container, so the dragged edge lagged the pointer and the other columns
  shifted. Grids with an unsized column keep the auto layout.

## [5.3.1] - 2026-09-02


- **Every border defaults to the border token.** shadcn's base layer sets
  `border-color` to the border token on every element, so a bare `border` class draws
  the hairline; Lumeo's CSS never did, and `border` in a consumer's markup, or in a
  verbatim shadcn port, drew `currentColor`, a near-black frame. The rule lives in
  `@layer base` in `lumeo.css`, so utilities like `border-primary` still win.


- **Mail and Music** are shadcn's examples, ported one to one from the last commit
  that carried them: the resizable three-pane inbox with the collapsing folder rail,
  and the Menubar-driven music app with album context menus.
- **The eleven remaining full pages sit in shadcn's sidebar-07 shell** (Analytics,
  Calendar, Chat, E-Commerce, File Manager, Filters, Form Wizard, Kanban,
  Notifications, Settings, Social Feed): team switcher, nested navigation, user menu
  and a breadcrumb header, with each page's own frame removed.

## [5.3.0] - 2026-09-02

5.2.1 was tagged and never published; its three fixes ship here.

shadcn's four example apps are in the blocks catalogue now, ported one to one from
the current sources, and porting them verbatim showed where four primitives still
drew their own thing. Each of those lands on shadcn v4 here.


- **`CardAction`** and a two-row `CardHeader` grid, ported from shadcn's `card.tsx`:
  the header grows a second column only when an action is present, and the action
  spans both rows so it sits beside title and description. Card, CardHeader,
  CardContent, CardFooter, CardTitle and CardDescription carry their `data-slot`
  attributes, so shadcn's `*:data-[slot=card]:` selectors work on Lumeo cards.
- **Blocks**: shadcn's **Dashboard** (dashboard-01: app sidebar, stat cards, the
  interactive area chart, a tabbed data table), **Tasks** (faceted filters with
  counts, sortable and hideable columns, row actions, pagination on the 100 sample
  tasks), **Playground** (preset combobox, three editing modes, a model combobox
  that previews the highlighted model, sampling sliders explained by HoverCards) and
  **Authentication** (the split hero with the create-account form). Dashboard
  replaces "Dashboard (full)", Tasks replaces "Task Tracker", Authentication replaces
  the three-screen page; Playground is new.


- **`Slider` looks like shadcn's**: a 16px white thumb with a primary hairline and
  `shadow-sm` that grows a 4px ring on hover and focus, a 6px muted track with
  rounded ends, and the track filled up to the thumb in the primary colour. It was a
  20px primary-filled thumb on an 8px `primary/20` track.
- **`TableHead` is `text-foreground`** and head and cells are `whitespace-nowrap`,
  as in shadcn v4; the head used to be muted (shadcn v3). A row whose trigger is
  expanded tints like a hovered one.
- **Icons inside `Badge`, `CommandItem` and `TabsTrigger` size themselves**: an
  unsized `<svg>` is 12px in a badge and 16px in a command item or tab trigger, and
  does not intercept the pointer; a command-item icon without its own text colour
  is muted. They used to render at the icon's natural size.


- **Chart gradient stops resolved to `0`** when a series colour was a token that
  pointed at another token (`--chart-1: var(--color-primary)`): the interop's
  colour probe followed one level only. It resolves the chain now.

- **The "dark" and "light" menu colours set the whole sidebar token set.** The theme
  customizer's menu colour used to write only `--color-sidebar` and its foreground, so a
  dark sidebar on a light page kept the light theme's `sidebar-primary` (near-black on
  near-black: the team switcher's logo box vanished), the light accent (every hover a
  light slab), and the light border. All eight `--color-sidebar-*` tokens now follow the
  chosen colour, using lumeo.css's own dark and light sidebar defaults; resetting the
  option clears all eight.
- **`AvatarFallback` sets `text-foreground`.** Its `bg-muted` disc is a page-tone
  surface, so its initials are page-tone too instead of inheriting: inside a dark sidebar
  they were white on a light disc.
- **The sidebar-07 block's avatar is square**, as shadcn's is; `Class="rounded-lg"`
  alone did not reach the avatar's inner clip.

## [5.2.0] - 2026-09-02

The blocks catalogue starts moving onto shadcn's own blocks, ported one to one rather
than re-imagined. Doing that honestly exposed two families the library was missing:
shadcn's form-layout primitive, and half of its sidebar. Both ship here, measured live
on ui.shadcn.com on 2026-09-02.


- **The Field family** — `Field`, `FieldGroup`, `FieldSet`, `FieldLegend`, `FieldLabel`,
  `FieldTitle`, `FieldContent`, `FieldDescription`, `FieldSeparator`, `FieldError` —
  ported from shadcn's `field.tsx`. A `FieldGroup` spaces fields 20px apart, a `Field`
  spaces its label, control and description 8px apart, `Orientation="Horizontal"` puts
  a checkbox or switch beside its text, `FieldSeparator` sits a caption on a rule, and
  `FieldError` renders nothing when it has nothing to say and collapses duplicate
  messages. Every one of shadcn's login and signup blocks is built from these; a Lumeo
  form used to be a Stack with hand-picked gaps.
- **Sidebar members** — `SidebarInset`, `SidebarRail`, `SidebarMenuSub`,
  `SidebarMenuSubItem`, `SidebarMenuSubButton`, `SidebarMenuAction`, `SidebarMenuBadge`,
  `SidebarGroupContent`, `SidebarGroupAction`, `SidebarInput`, `SidebarMenuSkeleton` —
  ported from `sidebar.tsx`. `SidebarMenuButton` gains `Size` (Default 32px, Sm 28px,
  Lg 48px for the two-line team switcher and user chip) and `Variant="Outline"`,
  renders a `<button>` when it has no `Href`, and carries the `data-slot` / `data-size`
  / `data-active` contract the action and badge position against. `SidebarComponent`
  takes `Rail="true"`. Field report 3.1 asked for the sub-menu.
- **Blocks**: shadcn's `login-01`…`login-05`, `signup-01`…`signup-05` and `sidebar-07`,
  each as its own demo with source, replacing the hand-built Sign In / Sign Up pages.


- **`SidebarHeader` and `SidebarFooter` no longer draw a border by default**, and the
  aside drops its permanent edge border: shadcn's sidebar has neither, the `--sidebar`
  tint separates it from the page. Pass `Bordered="true"` for the old look.
- **`SidebarMenuButton` colours are shadcn's**: `text-sidebar-foreground` at rest,
  `bg-sidebar-accent text-sidebar-accent-foreground` on hover, instead of
  `text-muted-foreground` / `hover:text-foreground`.


- **A collapsed icon rail reaches the bottom of its shell.** `h-full` on the in-flow
  aside resolved to `auto` inside a wrapper that only had a `min-height`, which defeated
  the flex stretch: the rail ended at its content and the footer floated mid-way. The
  in-flow variants use `self-stretch` now; the absolute ones keep `h-full`.

## [5.1.1] - 2026-09-02

The other half of "Lumeo looks clunky next to shadcn". After 5.1.0 the geometry
measured equal; what still read as heavier was the resting shadow Lumeo put on every
control, and a card that spaced itself the way shadcn's did two versions ago. Both
measured live on ui.shadcn.com and reui.io on 2026-09-02, and the two agree.


- **BREAKING — controls carry no resting shadow.** Button (every variant),
  AlertDialogAction and AlertDialogCancel, UploadTrigger, Badge, Input, Switch lose
  their `shadow` / `shadow-sm` / `shadow-xs`. shadcn and reui both render
  `box-shadow: none` on all of them; the outline and secondary buttons had been
  carrying shadcn's pre-v4 values. Popover, DropdownMenu and Select content step down
  from `shadow-lg` to `shadow-md`.
- **BREAKING — Card spaces itself with `--card-spacing`.** The card is a flex column
  with `gap` and vertical padding from the token; CardHeader, CardContent and
  CardFooter take their horizontal inset from it, and CardHeader's rows are `gap-1`.
  The token is `1rem`, shadcn's and reui's value, so a card is 16px inside instead of
  the old `p-6` (24px) on each part with no gap between them. Set `--card-spacing` to
  re-space every card at once; `Density` moves it to `0.75rem` / `1.5rem`. A part used
  outside a Card falls back to `1rem`.
- **`TabsContent` has no top margin** (field report 4.3). shadcn's has none; the
  `mt-2` was Lumeo's, and every consumer who put tabs in a flex column had to
  override it.

## [5.1.0] - 2026-09-01

A team rebuilding a dense, production Next.js/shadcn application in Blazor sent a
field report against 5.0.0: 47 findings, each checked against a running app, plus the
2,400-line stylesheet they needed to make Lumeo match their shadcn reference pixel for
pixel. This release works through it. The DataGrid virtualization and server-mode
defects were real and are fixed. The "Lumeo is bigger than shadcn" impression was real
too, but it was never the control scale (5.0 got that right): it was the sidebar
inheriting the page's 16px text, a radius scale two pixels rounder on every rung, and
icons that had to be sized by hand. Each of those is now measured equal to shadcn's live
docs, and most of that stylesheet is no longer needed.


- **BREAKING — the radius scale is shadcn's.** `--radius-sm/md/lg/xl` were
  `0.5r / r / 1.25r / 1.5r` (5 / 10 / 12.5 / 15px); shadcn v4 has `r-4px / r-2px / r / r+4px`
  (6 / 8 / 10 / 14px). Every `rounded-md` corner was 2px rounder than shadcn's, every
  `rounded-lg` 2.5px. Components whose shadcn counterpart sits on a different step were
  re-rung so their computed radius lands where shadcn's does: Input, Textarea and
  SelectTrigger `rounded-lg` (10px), DialogContent and AlertDialogContent `sm:rounded-xl`
  (14px), Command `rounded-xl`, DropdownMenuContent `rounded-lg`, Progress `rounded-full`.
  `.style-new-york` now sets `--radius: 0.625rem`, shadcn's value since v4; `0.5rem` was
  the pre-v4 one.
- **BREAKING — the sidebar is shadcn's sidebar, measured.** `SidebarMenuButton` is
  `h-8 p-2 gap-2 text-sm rounded-md` with 16px icons; it used to be `px-3 py-2 gap-3` with
  an *inherited* font size, which in a 16px page made every entry 40px tall with 16px
  text — the single most visible reason Lumeo read as larger than shadcn. Header and
  footer are `p-2` (was `p-4`), content is `gap-2` with no padding and no reserved
  scrollbar gutter (was `px-2 py-3 gap-1` + `scrollbar-gutter: stable`, which cost every
  consumer 10px of navigation width), group `p-2`, group label `px-2`, menu `gap-1`. The
  44px mobile touch floor stays.
- **BREAKING — `AlertDialogAction` defaults to the primary variant** and gains a
  `Variant` parameter with Button's full set. It used to render destructive
  unconditionally, so "transfer again?" got the same red button as "delete?". shadcn's
  action is `buttonVariants()` at its default; pass `Variant="Destructive"` where red is
  the right answer.
- **Checkbox's unchecked border is `border-input`** (shadcn), not `border-primary/60`.
- **Tabs trigger** reserves `border border-transparent` and fills the track minus 1px,
  so the active tab, which gets a visible edge, no longer grows by 2px against its
  neighbours. Track 32px / 3px inset, trigger 25px: shadcn's numbers.
- **`DropdownMenuItem` and `DropdownMenuSubTrigger` are `text-start`.** They are
  `<button>`s, and the UA centres button text; shadcn's items are start-aligned divs.
- **`NavigationMenuLink` sets `text-sm`** instead of inheriting.
- **DataGrid's sort glyph** is no longer 30% opacity — it read as "a tiny grey dot".
  shadcn's column-header icon inherits the ghost button's colour at full opacity.
- **BREAKING — `CollapsibleTrigger` renders a native `<button>`** (#438) instead of
  `div[role=button]`, so it is focusable, submits nothing, and takes `disabled`. The
  `IAsyncDisposable`/`OnAfterRenderAsync` surface it had for the keyboard shim is gone.
- **BREAKING — Excel and PDF export move to `Lumeo.DataGrid.Export`** (#433).
  `Lumeo.DataGrid` no longer depends on ClosedXML and QuestPDF, which every consumer paid
  for (1.44 MB brotli) whether or not export was on. CSV stays built in; add the new
  package for the other two formats.


- **DataGrid `OnRangeRequest` fires** (#431). The empty-state branch matched the
  deliberately empty `Items` list before the server-virtualization branch, so
  `<Virtualize>` never mounted. A server that reports zero rows still shows the empty
  state, rendered beside the virtualizer rather than instead of it.
- **`PageSize` no longer materialises that many skeleton rows** (#432). The loading
  skeleton is capped at 40 rows; a generous `PageSize` with pagination off used to freeze
  the main thread for seconds, with no spinner and no error.
- **The initial ServerMode request is deterministic** (#442). It fired from
  `OnInitializedAsync` alone; `ServerMode` or the handler arriving a render later left
  the grid empty for good. It now retries on first render, fires when `ServerMode` turns
  on, and **`RefreshAsync()`** lets the host ask again after an external filter change.
- **Shift-click range selection works** (#440), as the `SelectionMode` docs promised.
  Shift extends from the last plain click without moving the anchor.
- **A column resize never moves the columns to its left** (#443). Under
  `table-layout: auto` a width on one cell is only a hint, so with no slack the browser
  took the space from a neighbour. Widths are frozen and the table switched to fixed
  layout on the first drag; the table absorbs the delta.
- **Six German strings were missing** and fell back to English (field report 5.3):
  `Common.Expand`/`Collapse`, the three Gantt keyboard announcements and
  `Kanban.CardRoleDescription`. A test now holds German to full parity with English.


- **Icons size themselves.** Button, DropdownMenuItem, DropdownMenuSubTrigger and
  SidebarMenuButton apply `size-4` to any `<svg>` without an explicit `size-*`/`h-*` class,
  as shadcn's do. An unsized Lucide icon used to render at its native size; consumers had
  to write `class="h-4 w-4"` at every call site (the docs alone did it 252 times).
- **The toolbar is composable** (#441). `DataGridToolbarColumns`, `Export`,
  `CopySelected`, `Layouts` and `Fullscreen` take a `Grid` reference and render anywhere
  in the host page; `DataGrid.ToolbarContext` is public; **`ShowLayouts`** gates the
  layouts menu like `ShowExport` gates export.
- **`PopoverContent.FocusTargetId`** (#439) redirects initial focus to an element
  inside the popover instead of the content wrapper.
- **`lumeo-classes.txt`** ships as a static asset: every class name Lumeo's own
  components use, one per line, for `@source` in a consumer's own Tailwind build.
  Tailwind cannot scan the NuGet DLL, and running a second build *next to*
  `lumeo-utilities.css` is not additive: both put their rules in the same `utilities`
  layer, so whichever loads second wins for a class both contain, and a consumer's plain
  `text-center` silently overrides Lumeo's `sm:text-start`. That was the mechanism behind
  the "dialog headers always centred" report. The CLI copies the file in prebuilt mode.


- The README and the install page now say how to run your own Tailwind build with
  Lumeo — `@source` the safelist and drop the bundle — and why loading both is wrong.
- The CLI page says plainly that `lumeo-utilities.css` contains only the utilities
  Lumeo's own components use (#435), with the grep to check a class.

### Deferred

- Density/size tokens as CSS variables (#434) stay open. Wiring one arm of the size
  ladder to a token makes a half-feature; the whole ladder needs a design of its own.
  With this release the default geometry *is* shadcn's, which is what the report's
  team needed the tokens for.

## [5.0.0] - 2026-08-24

Every control in Lumeo was one step larger than its shadcn counterpart. Measured
against a React project built with the shadcn CLI, that turned out to be exactly
what it was: a systematic one-rung offset rather than scattered drift.


- **BREAKING — `Label` is a flex container.** It now matches shadcn's
  `flex items-center gap-2`, which is what lets a label hold an icon or a required
  marker without per-consumer spacing. The consequence is that a text-alignment
  utility alone no longer moves the label's content: the text becomes a
  shrink-wrapped flex item, so there is no free space for `text-align` to
  distribute. Replace `<Label Class="text-right">` with
  `<Label Class="justify-end text-right">` (and `text-center` with
  `justify-center`). A label that explicitly sets a display utility —
  `Class="text-center block"` — is unaffected, because that utility wins the
  merge and the label stays a block box.

- **BREAKING — the control scale moves down one rung.** shadcn's default button is
  `h-8 px-2.5` today, small `h-7`, large `h-9`, icon `size-8`; their input is `h-8 px-2.5`.
  Lumeo shipped the rung above each of those. Button, Input, Select, Toggle, Avatar,
  Badge, Tabs, Spinner and Progress now sit where shadcn does. Their horizontal padding
  follows shadcn's own change too: one `px-2.5` for small, default and large alike,
  instead of a `px-3 / px-4 / px-6` ladder.
- **BREAKING — the theme radius drops from `0.75rem` to `0.625rem`,** shadcn's value.
  `themeManager.getRadius()` and the preset catalog report and offer it, and a test now
  fails if those three copies of the number ever disagree again.
- **BREAKING — reui takes precedence over shadcn** where both define a component, and
  the two differ: Alert is `px-3 py-2.5` (not shadcn's `px-2.5 py-2`), and Badge
  `px-1.25` with `rounded-sm` (not `px-2` with a pill). Badge's `Pill` parameter is
  unchanged in meaning and mirrors reui's own `radius="full"`.
- **Alert, Popover, Textarea, HoverCard, Card, Label and Kbd** move onto the same
  values: tighter container padding, `min-h-16` on the textarea, a body text size on
  the card, and a label that spaces its own control like shadcn's does.
- The **density ladder is preserved throughout**: `Comfortable` IS shadcn now, `Compact`
  one rung below, `Spacious` one above. Applications that want the previous proportions
  can wrap themselves in `DensityScope` at `Density.Spacious`, which lands within a hair
  of the old values.


- **A badge no longer stretches to its container.** `inline-flex` alone does not prevent
  it; shadcn and reui both carry `w-fit shrink-0 whitespace-nowrap`. Measured in a flex
  column: 300px before, 77.6px after. A caller can still ask for the old behaviour with
  `Class="w-full"`.
- **Every table that mirrors another component's sizes moved with it:** UploadTrigger,
  SplitButton's chevron width (now density-aware), ShimmerButton, ToggleGroupItem and
  the AvatarGroup overflow chip had each been left a rung behind.
- **Density reaches the height,** not just the padding, on Badge and on the non-default
  Tabs variants — both previously rendered identically at `Spacious` and `Comfortable`
  despite their public contract.
- **`AvatarGroup.Size` sizes its members,** not only the chip, and an avatar that later
  renders without a `Size` of its own follows the group again.
- **Compact inputs keep room for their line box** at every breakpoint, and an `Xxs`
  avatar's fallback initials are no longer clipped.


- **`AvatarGroup.Size`** and **`SplitButton.Density`**.
- **`/e2e/shadcn-parity`** — one element per primitive at its defaults, the counterpart
  of the React project these measurements came from.

## [4.3.3] - 2026-07-18

- **Custom number steppers on `Input`.** `<Input Type="number">` now renders themed
  vertical ▲▼ stepper buttons in place of the browser's native spinner arrows (which
  are always hidden for number inputs). They honour `min`/`max`/`step` from the usual
  HTML attributes, disable at bounds, and respect a splatted `readonly`/`disabled`.
  New `ShowStepButtons` parameter (default `true`) renders a clean, spinner-free number
  field when set to `false`. The stepping core is shared with `NumberInput`.

- **Dead `disabled:opacity-50` on wrapped inputs.** The reduced-opacity style sat on the
  wrapper `<div>` (which can never match `:disabled`) instead of the `<input>`, so
  disabled inputs using the prefix/suffix/search/clearable/number layout did not dim.
  Moved onto the input element.

## [4.3.2] - 2026-07-14

- **DataGrid: probabilistic "duplicate key" crash on large grids.** Row keys for
  reference-type items without a user-supplied `RowKey` were derived from the runtime
  identity hash, which is effectively 26-bit on CoreCLR — with ~1,200 distinct row
  objects a render had a ~1.9% chance of two rows colliding (rising to ~53% at 10,000
  rows), crashing the renderer with "More than one sibling of DataGridRow has the same
  key value". Keys are now collision-free per-instance identity objects (weakly cached,
  no leaks); DOM keys use a monotonic counter. Found via a CI test that was long
  misread as flaky.
- Test-only: the toast stacking depth-3 exit-choreography test was hardened against CI
  scheduler starvation (deterministic exit windows instead of wall-clock racing).

## [4.3.1] - 2026-07-14

DataGrid column-header interaction rework, driven by hands-on playground testing.

- **Whole-header column drag.** Click anywhere on a header to sort; drag it (>5px,
  or long-press on touch) to reorder — the grip remains as a hover affordance and
  immediate handle. A completed drag never triggers a sort. Subtle lift animation
  (scale + shadow, reduced-motion aware), edge auto-scroll, and a nudge-and-spring
  cue on non-reorderable columns. `Alt+ArrowLeft/Right` moves the focused column
  with a localized screen-reader announcement (all 14 locales).
- **Unified drag-to-group.** Dragging a header over the group panel highlights it
  and dropping groups by that column — one pointer-driven gesture for reorder and
  grouping (native HTML5 drag-and-drop retired), including a floating chip ghost
  that carries the column name to the panel, and full touch support for
  drag-to-group. Grouping-by-drag now works independently of `Reorderable`.
- Note: the public plumbing type `DataGridDragState` (native-DnD cascading state)
  was removed with the native drag retirement; it was unusable outside the grid's
  internals. New `DataGrid.UpdateColumnFlags` supports runtime column-flag updates.

- **Runtime-frozen column flags**: `Sortable`, `Filterable`, `Resizable`, `Pinnable`
  and row `Hoverable` now react to parameter changes at runtime (previously captured
  once at registration).
- **Docs playground**: real `$` currency formatting (the docs' invariant-globalization
  build rendered `¤` in 41 spots incl. exported CSV/Excel/PDF), accessible names for
  all setting controls, precondition-gated toggles instead of silent no-ops, and the
  site-wide `Lumeo.Docs.styles.css` 404 removed.

## [4.3.0] - 2026-07-13

The trust release: a seven-point maturity campaign hardened over ~13 automated review
waves, plus a full-library screen-reader audit. Everything below shipped through
feature-branch PRs with green CI, a docs-parity gate, and API-stability baselines.

- **Toast stacking (sonner-style).** `ToastProvider.StackToasts` (default `true`):
  when a position group holds more than one toast, the newest renders in front
  at full scale while older toasts collapse behind it with a small directional
  offset away from the group's anchor edge and a progressive scale-down, capped at
  3 visible. Hovering anywhere over the group, or moving keyboard focus into it
  (focus-within), expands it back to the classic gapped list; leaving/blurring
  collapses it again. Pure CSS transforms/transitions driven by `data-index` /
  `data-stacked` / `data-expanded` / `data-stack-edge` attributes — no
  per-frame JS or .NET calls. Set `StackToasts="false"` to always render the
  plain list. Admission (per-position caps, queueing, explicit `Update`
  reclassification) was consolidated into one canonical `TryAdmit`/`ReconcileGroup`
  path with frozen per-toast snapshots and an invariant test suite.
- **.NET 8 + .NET 10 multi-targeting** across all shipped packages, with the test
  suite executed against a genuine .NET 8 runtime in CI (not roll-forward).
- **Real trimming** (#354): `IsTrimmable` on shipped assemblies plus a trim-safe
  source-generated JSON context — a single-component publish drops the Lumeo
  contribution by ~83%. The QueryBuilder value serializer moved to a closed
  `Type.GetTypeCode` switch backed by a 44-case fuzz matrix.
- **SourceLink + symbol packages (snupkg)** for step-into debugging.
- **Public performance benchmarks**: reproducible scripts plus a docs page
  (`/docs/performance-facts`) with measured numbers and honest disclosure of limits.
- **New test legs**: a Blazor Server latency leg (real SignalR circuit under CDP
  network throttling), a three-engine pointer harness (Chromium/Firefox/WebKit),
  visual-regression baselines, a weekly axe-core WCAG A/AA sweep of all 164
  component routes gated on a node-shape baseline, and an automated NVDA
  screen-reader audit (`scripts/sr-audit`, Guidepup) that runs unattended and
  verifies what NVDA actually announces per component.
- **Keyboard-interaction test families** for 42 more components (matrix 61 → 103),
  with ~15 accompanying product fixes (PivotGrid cells, DatePicker toggle/escape,
  ScrollArea region semantics, Dock/Toolbar roving tabindex, and more).
- **New parameters**: `Form.ModelTypeInfo` (trim-safe snapshot round-trip),
  `PopoverTrigger.SuppressActivationKeys`, `ScrollArea.AriaLabel`,
  `ToggleGroupItem.AriaLabel`, `ToastProvider.StackToasts` /
  `ToastViewport.StackToasts`.
- **Docs infrastructure**: 141 component pages now render their API tables directly
  from the generated registry (no hand-maintained drift), enforced by a two-way
  docs-parity CI gate; public API surfaces are locked by PublicApiAnalyzers
  baselines on all shipped packages; a weekly NuGet-free eject gate proves all 164
  components vendor and compile standalone.

- **Toast exits now animate** (previously removed without an exit transition), and
  toast stacking is on by default — see `StackToasts` above to opt out.
- **DataGrid `aria-rowindex`/`aria-rowcount`** are now computed by a single row
  indexer over all rendered rows — group headers, detail rows and items count
  correctly in grouped grids (screen readers previously received wrong indices).
- **Docs facts pass**: every number on the site is now measured from the repo
  (component/block counts, test totals, a11y coverage), and the accessibility page
  documents the real audit state including the tracked axe baseline.

- **Trimmed publish: components crashed at runtime** — under `PublishTrimmed`, the
  linker strips constructor parameter names and removes reflection-only parameterless
  constructors, so any anonymous type or positional record crossing JS interop threw
  `ConstructorContainsNullParameterNames` (hit live on the docs site; keyboard
  scroll-suppression alone affected ~60 components). All interop option bags now
  serialize as plain dictionaries (identical JSON) and incoming payload types carry
  `[DynamicDependency]` so the reflection deserializer keeps working. Verified with a
  real trimmed publish + a crawl of all 165 docs routes.
- **Blazor Server: toasts never rendered** (#363) — `ToastService.Show` produced no
  render batches on a real SignalR circuit and bursts >6 could crash the renderer;
  verified fixed under the new server latency leg (burst caps at the configured
  maximum, no crash).
- **FileManager folder tree was mouse-only** (WCAG 2.1.1) — full WAI-ARIA
  roving-tabindex keyboard navigation (arrows, Home/End, Enter/Space), found by the
  NVDA audit.
- **Cascader trigger announced as a bare button** — now advertises its menu popup
  (`aria-haspopup`/`aria-expanded`/`aria-controls`), found by the NVDA audit.
- **QueryBuilder silently corrupted out-of-range numbers** — an oversized literal
  (e.g. `1e400`) clamped to `Infinity` and round-tripped as a corrupt query; it now
  fails parsing loudly.
- **Grid ARIA contracts + localized accessible names** across the DataGrid family
  in all 14 locales (axe baseline shrink wave 1).
- **ConsentBanner could freeze** after its entrance animation (filled animation
  state overrode later transforms); resolved via animationend handling.
- **CLI vendoring rewrote generic type arguments** (`EventCallback<List<...>>`
  became uncompilable) — namespace rewriting is now argument-aware.
- **Toast: `Update` on an already-leaving toast** is a no-op instead of restarting
  a timer on a toast that still gets removed; a `DismissAll` on a hovered group no
  longer strands the next toast paused.

## [4.2.0] - 2026-07-11

DataGrid column/row interaction suite rebuilt to flagship level (PR #353), hardened
over a 13-round automated review loop plus a 41-scenario real-pointer browser harness.

- **DataGrid column resize, complete.** Always-visible hover handle, a resize
  guideline that tracks the exact cell edge (correct under min/max clamping,
  horizontal scroll, first column, and RTL), double-click auto-fit to intrinsic
  content width, and keyboard resizing (arrow keys on the focused handle or
  Ctrl+Arrow on the header) that never scrolls the page.
- **DataGrid column reorder with live animation.** One unified pointer engine for
  mouse, touch and pen (immediate on the grip, 5px threshold header-wide): sibling
  columns shift out of the way live while dragging, the dropped column glides into
  its slot, Escape glides it back. Locked/pinned columns are skipped in preview and
  preserved on commit; non-primary mouse buttons are ignored.
- **DataGrid row reorder on the same engine.** Handle-only vertical dragging with
  live row shifting and glide settle; expanded detail rows travel with their parent
  as one band. Rows keep stable identity across rebuilds, and commits resolve by
  row key — scoped to flat, non-virtualized grids.
- New programmatic APIs: `ReorderColumnByIdAsync`, `ReorderRowByKeyAsync`, and
  awaited internal commit variants; additive default-interface members on
  `IComponentInteropService` (existing implementations keep compiling); reorder and
  resize strings localized in all 14 locales.

- **Interaction robustness.** A per-grid arbiter serializes every mutating gesture
  (drag, resize, double-click auto-fit, keyboard nudge) and is held until the .NET
  commit — including awaited consumer callbacks like `OnColumnResize` /
  `OnRowReorder` — has fully completed, so slow persistence can never be overtaken
  or overwritten by a later gesture. Every commit runs through one canonical
  validation path whose rejection branch always clears the drag transforms, so
  toggling `Reorderable`/`RowReorderable`, hiding columns, grouping, or enabling
  virtualization mid-gesture leaves no visual residue.
- **Performance.** All per-move work stays in JavaScript (zero .NET interop calls
  during drag/resize movement), measured at ~0.004–0.007 ms per move event.

- **Blazicons is gone everywhere.** The library decoupled in 4.1.0; this release
  removes every remaining live reference across the repo: the docs' third-party
  interop packages, lazy assembly groups and comparison surfaces (the icon page and
  customizer are first-party-only now), the Blazicons-specific `svg[blazicon]`
  sizing rule in `lumeo.css`, and every code sample, doc comment and guide mention.
  The DynamicIcon docs demo third-party interop generically — bring your own SVG via
  an inline `IconSource` (rendered as-is: trusted, static markup only).
- **CLI preset icon catalog is first-party only.** `lumeo preset encode --icons`
  accepts the 16 first-party packs (including all variant ids like
  `fluent-filled` / `heroicons-solid`); the legacy third-party names are rejected
  with the valid list. Stored presets keep decoding: legacy codec indices and
  server-preset strings pass ONE normalization gate before anything is written —
  mappable names rewrite to their first-party equivalent (`fluentui` → `fluent`,
  `google-material` → `material-symbols`), unmappable ones warn and are skipped, so
  a stale preset can no longer overwrite a valid `iconLibrary` or write a dead one.
  `--dry-run` previews exactly what a real apply would write, warnings included.
  A compile-level drift guard ties the CLI's known-id set to the icon pack catalog.

- **Docs/interop cleanup — Blazicons removed everywhere outside history.** The docs site
  no longer references or bundles any Blazicons package: the `/components/icon` browser and
  the customizer's icon-library picker are first-party `Lumeo.Icons.*` only, and the
  DynamicIcon docs page now demonstrates third-party interop with a generic inline
  `IconSource` (bring your own SVG) instead of a Blazicons pack. The `lumeo apply --preset`
  CLI maps icon libraries to first-party packs only; the Tailwind-v4 icon-sizing shim that
  was specific to Blazicons' `svg[blazicon]` rule was dropped. Public library API is
  unchanged (icons already decoupled to `Lumeo.IconSource` in 4.1.0).

## [4.1.1] - 2026-07-10

Bug-fix and TreeView-UX roll-up hardened over twenty-one review rounds (PR #351),
including a structural rebuild of the TreeView's internal state ownership.

- **TreeView row-click expand (VS Code pattern).** Clicking anywhere on a parent row
  now selects it AND toggles its expansion by default; the chevron keeps working and
  still toggles without selecting. New `ExpandOnRowClick` parameter (default `true`) —
  set `false` to restore the strict click-selects/chevron-expands split. Modifier
  clicks (Ctrl/Meta/Shift) only mutate selection and never toggle folders; keyboard
  semantics are unchanged.
- **Docs footer with a Cookie-settings control** wired to
  `ConsentService.RequestOpenPreferences()` (works after the banner was dismissed),
  and the docs self-host ECharts + LiquidFill/WordCloud plugins + the world map
  (version-keyed, immutable-cached) so chart pages make no pre-consent third-party
  requests.

### Changed (behaviour)
- **TreeView tri-state checkboxes derive from seeded state on first render** (and
  after lazy loads) — previously parents rendered unchecked until the first click.
- **The expand chevron is visually integrated into the row** (transparent ghost, row
  carries the hover highlight; focus ring kept for keyboard users).
- **TreeView UI state is tree-owned.** Expansion/loading/loaded state lives in the
  tree keyed by a rebuild-surviving identity; the consumer record's flags act as
  seeds and are mirrored back. Duplicate-valued sibling selections drop on ambiguous
  reloads (identity cannot be proven) while consumer value-seeds keep binding every
  match — both rules are documented in the component.

- **TreeView parent selection (#350).** Selection is tracked by node identity, so
  clicking a container node with a null or duplicate `Value` selects only that node
  instead of every node sharing the value. Identity survives same-content `Items`
  refreshes, empty/lazy reloads, and controlled rebuilds; a failed lazy row-click
  expansion now rolls back to collapsed (selection intact) even when a controlled
  rebuild replaced the node instance.
- **GDPR consent hardening.** A `PolicyVersion` bump after hydration re-evaluates the
  stored decision and re-prompts; malformed/versionless/timestampless proof records
  fail closed; re-deciding after an invalidated record replaces the stale category
  state instead of restamping un-presented grants with the new policy version.
- **Charts self-hosting.** `loadExtension` now honours `window.lumeoCdn` override keys
  and a per-chart `EChartsSource` so LiquidFill / WordCloud plugin charts can avoid a
  pre-consent CDN request. The static `echarts-interop.js` contract change ships with a
  package-version bump so the `?v=` module cache key busts.

## [4.1.0] - 2026-07-07

The stable 4.1.0 roll-up of the `4.1.0-preview.1 … .15` line plus the merged
shadcn-parity campaign. **Additive and opt-in** — no API-signature breaks from
4.0.x except the icon-package decouple noted under BREAKING. Read this section
on its own to upgrade 4.0.0 → 4.1.0; the preview sections below are kept as
development history. Suite 5,600+ green; `dotnet new` is now a real
getting-started path and Lumeo owns its icon story end to end.

Headline work: a **first-party icon family** (16 trimmable packs), **`dotnet new`
templates** (app starter + full-stack), a **device-test fix series** (the B10/B11
overlay-exit saga), and a **shadcn parity campaign** (exit animations + `data-*`
hooks, native form participation, menu-system + NavigationMenu parity, chart/AI
a11y) hardened over ten Codex review rounds.

### BREAKING
- **Blazicons fully decoupled** (from preview.5). `Icon.Svg`, `MegaMenuLink.Icon`,
  `MegaMenuItem.Icon`, `PopConfirm.Icon` and `TreeViewItem.Icon` now take the new
  `Lumeo.IconSource` instead of `Blazicons.SvgIcon`; no Lumeo package references
  Blazicons anymore, and the NuGet-free standalone eject is now truly
  dependency-free. Migration is mechanical: keep `Lucide.X` with `@using
  Lumeo.Icons` + the `Lumeo.Icons.Lucide` package (names match 1:1), or use the
  ~220 built-in `LumeoIcons.X` that now ship in the core for free. Blazicons still
  works in `RenderFragment` slots — it just left Lumeo's dependency graph and
  public API.

### Behaviour changes (read before upgrading)
- **Declarative `Dialog` / `AlertDialog` / `Drawer` now animate their close by
  default** (overlay fade + content zoom, symmetric ~150 ms; Drawer slide 300 ms).
  Opt out per instance with **`PlayExitAnimation="false"`** for the previous
  instant unmount. The awaited `Show*Async` close-intent semantics are byte-
  identical; only the visual dismissal changed.
- **`ToggleGroup Orientation="Vertical"` now lays out as a column** (`flex-col`) to
  match its `aria`/`data-orientation` claim (previously still rendered as a row).
- **Sidebar `Ctrl/Cmd+B` is now the default toggle shortcut.** It is editor-safe:
  shortcuts are skipped inside editable targets unless registered with
  `allowInEditable: true` (the command palette's `Cmd+K` opts in), so it does not
  steal editor bold.
- **Sidebar collapsed rail is 16 px narrower** (`w-12` / 3rem, shadcn
  `SIDEBAR_WIDTH_ICON`) and collapse/expand timing matches shadcn 1:1
  (`duration-200 ease-linear`).

- **First-party icon family — 16 trimmable packs (~51,000 icons), all
  `IsTrimmable`.** `Lumeo.IconSource` + `SvgGlyph` are the native icon
  model/renderer (Stroke/Fill styles, viewBox scaling, `StrokeWidth` override,
  duotone); unused icons trim out of WASM publishes (measured: 3 Tabler icons cost
  7.3 KB, not the 3.4 MB pack — 476× smaller). Packs: Lucide, Tabler (+Filled),
  Phosphor (6 weights), Heroicons (4 cuts), Remix (2), Bootstrap, Iconoir,
  Material Symbols (3 styles + filled), Fluent (+filled). ~220 `LumeoIcons.X`
  ship in the core. Licenses embedded; generated by the new `tools/Lumeo.IconGen`
  pipeline.
- **Icons `/icons` gallery** in the docs (searchable, per-pack, click-to-copy,
  lazy-loaded) and **customizer live icon-library switching** — every first-party
  pack switches the whole docs site live via a data-driven semantic name map.
- **`dotnet new lumeo-app`** — a full Blazor WASM starter that boots styled with
  zero manual steps (`AddLumeo()` + prebuilt CSS + OKLCH theme + collapsible
  sidebar shell + dark-mode toggle + Dashboard/Form/Settings example pages + built-
  in LumeoIcons), with **`--auth <demo|none|oidc>`** (default `demo`): full
  login/register/forgot-password pages, `CascadingAuthenticationState` +
  redirect-to-login guards and a signed-in user card (`demo` = swappable
  localStorage provider, `oidc` = WebAssembly.Authentication wiring).
- **`dotnet new lumeo-fullstack`** — batteries-included starter: Blazor WASM client
  + ASP.NET Core API with Identity (`MapIdentityApi`, real e-mail confirmation),
  EF Core + PostgreSQL (auto-migrate + seed in dev), Scalar OpenAPI UI at
  `/scalar`, SMTP → MailHog in dev, CORS/nginx proxy, `/health`, and a
  docker-compose stack (postgres + mailhog + api + client). Register → confirm →
  login → live grid is template-test-verified.
- **`data-*` styling hooks on nine components** — Accordion, Collapsible,
  Checkbox, Switch, Toggle, ToggleGroup, Slider, Progress and Sidebar now emit
  `data-state` / `data-disabled` / `data-orientation` / `data-collapsible` /
  `data-variant` / `data-side`, so `data-[state=…]` and `group-data-[…]` selectors
  work (Radix attribute placement).
- **Native form participation** — Checkbox and Switch render the Radix bubble-input
  pattern (a hidden native `<input type=checkbox>` carrying `Name`/`Value`, new
  `Value` param default `"on"`) so plain `<form>` posts work; `EffectiveName`
  cascades from `FormField.Name` so composition posts without repeating `Name`.
- **Menu-system parity** — `MenubarCheckboxItem` / `MenubarRadioGroup` /
  `MenubarRadioItem` / `MenubarGroup` + `Inset`/`Variant`/`Shortcut`; a
  destructive item variant, `Inset` and `Shortcut` subcomponents across
  Dropdown/Context menus; and **typeahead** for ContextMenu and dropdown submenus
  (Dropdown root already had it), scoped so sub keystrokes don't bubble to the
  root.
- **Controlled `NavigationMenu`** — `Value` / `ValueChanged` / `DefaultValue` /
  `DelayDuration`, backed by one coherent controlled-value state machine (strict
  Radix: controlled-with-handler emits and renders what the parent pushes back, no
  optimistic flash; an id without a mounted item renders closed and re-opens when
  it re-registers).
- **Symmetric exit animations + `data-state` on the menu-style overlays** —
  DropdownMenu (+Sub), Menubar (+Sub), ContextMenu (+Sub), Tooltip, HoverCard and
  NavigationMenu (+Viewport) now play shadcn/Radix exits (zoom-out / fade-out) and
  emit `data-state=open|closed` on content and trigger, staying mounted through the
  exit (reusable internal `OverlayExitAnimator`).
- **Chart `AccessibilityLayer`** (default on) — renders a visually-hidden SR data
  table + focusable host with a generated `aria-label`; the table is **capped**
  (default 50 rows/series + an "and N more" row via `MaxAccessibilityRows`,
  0 = unlimited) so large series don't inject thousands of DOM rows.
- **AI message surfaces** — `AgentMessageList` gains `ConversationScrollButton`
  (JS scroll observer), an `EmptyState` slot and a messages-to-Markdown export;
  `AgentMessage` gains an Actions toolbar (Copy built-in, Regenerate/Retry
  callbacks — clipboard failures fail silently) and branch navigation (index
  clamped against a shrinking branch list).
- **`DialogContent` / `DrawerContent` / `AlertDialogContent` `PlayExitAnimation`**
  (default now `true`) — opt-out gate for the default close animation; backed by a
  new reduced-motion-aware `animate-zoom-out` keyframe.
- **`TabsVariant.Underline`** — text tabs with an underline indicator (constant-
  geometry border so activation never reflows; combines with `Scrollable`,
  `IconReveal` and `AnimatedIndicator`).
- **`AvatarShape.Themed`** — third avatar shape following the theme radius
  (identical to Circle at stock radii, squares off in sharp themes).
- **DataGrid header-level pin control** — a keyboard-accessible pin button in the
  column header (Pin left / Pin right / Unpin) plus `DataGridColumnDef.Visible` /
  `VisibleChanged` and `DataGrid.SetColumnVisibility` for programmatic column
  visibility.
- **Consumer-feedback APIs** — `TooltipContent.Align`, `Overlay/Dialog/Sheet
  ShowCloseButton`, `DatePicker/TimePicker/DateRangePicker FullWidth`, `Chip.
  IconContent`, `FileUpload.ShowFileList` + `Reset()`, `ThemeToggle.IncludeSystem`,
  `CommandInput.AutoFocus`, `PopoverContent.FocusOnOpen`, `AreaChart.GradientFill`,
  `Gantt.ZoomLevels`/`DefaultZoom`, and Maps cluster/interop APIs
  (`MapMarker.ClusterExclude`/`Properties`, `Map.ClusterProperties`/
  `ClusterColorExpression`/`ClusterRadius`/`ClusterMaxZoom`/`ElementId`).

- **Theme-radius wave — ~75 hardcoded roundings across ~40 components now follow
  the `--radius` token** (Switch, Badge, Tabs Pill, remove/clear buttons, DataGrid
  chips, FABs, and more). Every stock theme renders **pixel-identical** to 4.0;
  only deliberately sharp themes (`--radius: 0`) square these off. Semantic circles
  (radio dots, spinners, circular avatars, …) are deliberately untouched and pinned
  by a source-level guard test.
- **Docs overhaul** — landing rebuilt around the ownable-platform category with two
  live demo apps (Lighthouse mobile 36 → 97, desktop 65 → 100 via deferred
  hydration); ~14 new/lifted API pages covering every wave API; a currency pass
  correcting stale facts (test totals unified to 5,600+, "7 packages" → 10). The
  docs **Changelog page** carries a v4.1 entry.
- **Registry / tooling** — RegistryGen now discovers parameter-referenced
  standalone enums project-wide, so `MenuItemVariant [Default, Destructive]` surfaces
  to registry/MCP consumers; registry + MCP + search index regenerated against
  4.1.0.
- **`IKeyboardShortcutService`** gained ergonomic `RegisterAsync(combo, handler,
  allowInEditable)` overloads as default interface members — additive, legacy
  3-param implementors compile and route unchanged.

- **Overlay exit saga (B11) — declarative Sheets and every service-opened
  Dialog/Drawer/AlertDialog now animate out reliably.** The final structural fix
  ports the exit to the Radix/shadcn **Presence** pattern: on close the
  containing-block guard is stripped so the exit keyframe actually runs (it had
  been freezing the panel with `animation:none`), a JS helper awaits the panel's
  own animation `finished` promise and notifies .NET once, and backdrop + panel
  drop together in one commit. Exit-keyframe **durations are synced** to their
  enter counterparts (fade 0.15 s, slide 0.3 s, zoom 0.15 s) so backdrop and panel
  end in the same frame; a `data-lumeo-exit` latch survives rapid open→close→reopen
  with no zombie backdrop; reduced-motion unmounts immediately. The exit latches
  synchronously in `OnParametersSet`, independent of interop timing (the earlier
  race on slow devices/SignalR is closed).
- **Exiting overlays are inert while they fade** — `pointer-events-none` + `inert`
  on exiting menu and modal surfaces (Tab and clicks can't reach a fading ghost),
  submenus reset when their root closes, and Tooltip `data-side`/arrow retain the
  resolved side through the exit. **Modal backdrops keep `pointer-events-auto`
  through the exit** so a fast double-click can't fall through to the page.
- **Service-opened Sheet backdrop caught no clicks (B10)** — SheetContent's backdrop
  now carries `pointer-events-auto` (it had been dropped by `BackdropClass`), so
  click-outside-to-close works and the page behind is no longer interactive.
- **DataGrid column pinning actually moves columns** — columns are stable-
  partitioned (left-pinned → unpinned → right-pinned) in DOM order at the render
  chokepoint (`position: sticky` alone can't move a cell across siblings), so every
  pin path is correct through full horizontal scroll.
- **DataGrid drag lag (measured)** — the drag hot-path no longer round-trips to
  .NET (was re-rendering the whole grid up to 60×/s, ~198 ms/event on a 640-row
  grid); now ~0.02 ms/event with zero long tasks. **`DataGrid.Compact`** now
  genuinely tightens rows (density flows through context; `VirtualItemSize`
  auto-adjusts), and facet-filter popovers are a clean checkbox list (no doubled
  operator dropdown / Apply-Clear).
- **Fixed-position popovers opened offset inside transformed ancestors** — the
  containing-block compensation was folded twice; the idempotence guard now
  compares the exact serialized string it wrote (measured 0 px trigger offset,
  stable across re-opens). Also fixes popovers-inside-dialogs (B1), popovers
  freezing when their trigger moves, and over-viewport Select/Combobox lists.
- **Avatar** — Square/Themed avatars showing initials rendered as circles;
  `AvatarFallback` no longer paints its own radius and inherits the wrapper clip.
- **Tooltip** — stays dismissed after a mouse/pen/keyboard click on its trigger
  (only a real touch tap pins), and its arrow points at the trigger on an
  edge-clamped box.
- **Templates** — `lumeo-app` shell polish (demo-grade rail, breadcrumb topbar,
  no zoom-induced scrollbar) and the item templates emit the current heading /
  LumeoIcons / `[LumeoForm]` patterns (were emitting removed components).
- **Deflaked** the timing-test family (four were test artifacts, proven stable over
  160 runs) and the sidebar tooltip-reveal poll (dropped a CI-starvation ceiling).

## [4.1.0-preview.15] - 2026-07-06

- **B11, the structural fix — the exiting panel was FROZEN, not mistimed**:
  frame-sampling finally revealed the real culprit behind "the backdrop is
  gone before the sheet". After the slide-in, the containing-block guard
  stamped inline `animation:none !important; transform:none !important` onto
  the panel — which then overrode the exit animation class on close. The
  panel never moved (computed animationName stayed `none`) while the
  never-stamped backdrop faded out; both then popped in the same unmount.
  Four timing patches (preview.12-.14) could not touch this by design.
  The overlay exit is now ported to the Radix/shadcn Presence pattern: on
  close the guard is stripped so the exit keyframe actually runs, a JS
  helper awaits the panel's own exit animation `finished` promise and
  notifies .NET once, and backdrop + panel are dropped together in one
  commit — the timers remain only as an outer safety net. A `data-lumeo-exit`
  latch prevents a still-pending open-helper from re-freezing an in-flight
  exit on rapid close; reduced-motion unmounts both together immediately.
  Verified by rAF frame-sampling (independent double-check): panel animates
  `slide-out-to-right` with live transform for the full 300ms and panel +
  backdrop leave the DOM in the same frame after the animation — across all
  four overlay types, every close path, normal and 6x CPU-throttled; nine
  new regression tests lock the pattern in.

## [4.1.0-preview.14] - 2026-07-06

- **Overlay exit: backdrop and panel now finish in sync** (consumer retest of
  preview.13: "the backdrop is gone before the sheet"). The exit keyframe
  durations were mismatched (fade-out 0.2s vs slide-out 0.25s vs zoom-out
  0.15s), so the backdrop hit opacity 0 while a sheet was still sliding.
  Durations are now unified to mirror their enter counterparts (fade 0.15s,
  slide 0.3s, zoom 0.15s) and the slide-type backdrop carries a matching
  300ms override — every overlay type closes with backdrop and panel ending
  within the same frame (0ms gap by construction). Component exit timers and
  the provider's removal windows were re-buffered accordingly; eight new
  regression tests pin the per-type duration coupling so future drift fails.

## [4.1.0-preview.13] - 2026-07-06

- **B11, the real fix — service overlays could still hard-close without their
  exit animation**: the exit latch was set only AFTER the open-interop chain
  completed in `OnAfterRenderAsync` (scroll lock -> focus trap -> slide-end ->
  swipe). On WASM docs that chain resolves in ~1ms (which is why our previous
  verifications passed); in real apps a dismiss landing inside that window —
  SignalR round-trips or slow devices make it wide — hit `_wasOpen == false`,
  the exiting state never latched, and backdrop + panel unmounted in the same
  mutation batch (~15ms, no exit class), on every close path. All four content
  components now latch the exit synchronously in `OnParametersSet`
  (independent of interop timing); a dismissed-mid-setup undo ensures a focus
  trap/scroll lock is never left bound to a closing panel (previously leaked),
  and the open-interop calls gained JSDisconnected guards. Reproduced and
  regression-locked deterministically (six tests that block the scroll-lock
  interop to force the race); the before/after matrix covers every
  ShowSheet/Dialog/Drawer/AlertDialog variant x every close path (~205-290ms
  animated removals, previously 2-8ms in the race window).

## [4.1.0-preview.12] - 2026-07-06

- **Service-opened Dialog, Drawer and AlertDialog had no exit animation
  (B11)**: closing one (X button, backdrop click, Escape, or a programmatic
  `Close`/`Cancel`) removed the backdrop and panel in the same DOM mutation
  ~35–50 ms after the click — they vanished instantly while a declarative
  Sheet already slid out. `OverlayProvider` now defers every overlay's
  unmount through the same mechanism it already used for Sheets: on close it
  flips the hosted content's `Open` to `false`, the content plays its exit
  (panel zoom-out/slide-out + backdrop fade-out, in parallel), and the entry
  is removed only after the exit window (a `DelayedDispatch` safety-timeout, so
  a missed `animationend` can never leak a zombie overlay). `DialogContent`,
  `DrawerContent` and `AlertDialogContent` gained an opt-in `PlayExitAnimation`
  parameter (set by the provider) mirroring `SheetContent`'s exit machinery;
  declarative usage is unchanged (default `false` → immediate unmount).
  Browser-verified: exit classes now apply and DOM removal lands ~240–330 ms
  after the click (was ~35–50 ms with no class), backdrop fades while the panel
  leaves, and a rapid open→close→reopen leaves no zombie backdrop or double
  panel. The awaiting `Show*Async` task still resolves immediately on
  close-intent — the exit is purely visual.

- **`DialogContent.PlayExitAnimation` / `DrawerContent.PlayExitAnimation` /
  `AlertDialogContent.PlayExitAnimation`** (default `false`) — when `true`, the
  panel stays mounted for a zoom-out / slide-out (with the backdrop fading in
  parallel) before unmounting, instead of vanishing instantly. Set
  automatically by `OverlayService`-driven overlays; declarative consumers can
  opt in for the same dismissal animation. Backed by a new `animate-zoom-out`
  keyframe (the close counterpart to `animate-zoom-in`, reduced-motion aware).

## [4.1.0-preview.11] - 2026-07-06

Device-testing feedback wave.

- **Service-opened Sheet backdrop caught no clicks (B10)**: every overlay
  backdrop lives inside an intentionally `pointer-events-none` host (so idle
  overlays never block the page) and must re-enable its own pointer events —
  Dialog/Drawer/AlertDialog hardcode `pointer-events-auto` on their backdrops,
  but SheetContent drove its backdrop through `BackdropClass`, which omitted
  the class. The page behind a service-opened Sheet stayed fully interactive
  and click-outside-to-close was dead. Fixed in both the live and exiting
  branches; browser-verified (backdrop is the hit-target and closes the
  sheet); consumers can drop their CSS workaround.

- **`Chip.IconContent`** — a leading slot (TabsTrigger/SegmentedItem pattern)
  that keeps sized content intact (`shrink-0` wrapper): color dots stay dots,
  icons sit beside the label instead of stacking. ChildContent-only chips
  render byte-for-byte as before (asserted); priority stays Avatar > Icon >
  IconContent.
- **`FileUpload.ShowFileList`** (default `true`) and **`FileUpload.Reset()`**
  for immediate-upload flows: hide the internal file card while all events
  keep firing, and programmatically clear the selection so the same file can
  be re-picked (fresh input via keyed remount — no more consumer-side `@key`
  hacks).

### Notes
- The mobile overlay-from-drawer report (B4) could not be reproduced this
  round and stays on the watch list — a device/browser + step sequence would
  help pin it down.

## [4.1.0-preview.10] - 2026-07-05

Templates, round two: the app starter grew up and got a full-stack sibling.

- **`dotnet new lumeo-fullstack`** — the batteries-included starter: a Blazor
  WASM client plus an ASP.NET Core API with Identity (`MapIdentityApi`, real
  e-mail confirmation), EF Core + PostgreSQL (auto-migrate + seed in dev),
  Scalar OpenAPI UI at `/scalar`, SMTP wired to MailHog in dev, CORS/nginx
  proxy, `/health`, a seeded sample endpoint feeding the dashboard grid,
  docker-compose (postgres + mailhog + api + client) with `.env` ports and a
  documented hybrid dev mode. The full journey is template-test-verified:
  register -> confirmation mail in MailHog -> confirm -> login -> live grid.
- **`lumeo-app --auth <demo|none|oidc>`** (default `demo`) — Microsoft-style
  auth option: full login/register/forgot-password pages built from Lumeo's
  auth blocks, CascadingAuthenticationState + redirect-to-login guards and a
  signed-in user card with sign-out; `demo` uses a swappable localStorage
  provider (the seam is documented), `oidc` wires
  Microsoft.AspNetCore.Components.WebAssembly.Authentication with placeholder
  config.
- **`ThemeToggle.IncludeSystem`** — opt out of the three-way System cycle for
  a binary Light/Dark toggle (an invisible System->Dark first click read as
  a broken button in app shells).

- `lumeo-app` shell polish from hands-on review: sidebar ported to the
  demo-grade rail (centered icon anchors, tooltips, clip animation), topbar
  is a breadcrumb instead of duplicating the page title, brand row content
  vertically centered (the SidebarHeader default is a column flex — the
  centering axis was wrong), header lines align across rail and topbar, one
  continuous background tone, the rail can no longer grow a scrollbar under
  browser zoom/display scaling (overflow moved to the nav region only), and
  the FocusOnNavigate heading outline is gone for good.

## [4.1.0-preview.9] - 2026-07-04

The templates release: `dotnet new` is a real getting-started path now.

- **`dotnet new lumeo-app`** — a full Blazor WASM starter that boots styled
  with zero manual steps: `AddLumeo()` wired, prebuilt CSS + OKLCH default
  theme + `theme.js`/`components.js` linked, collapsible Sidebar shell with a
  dark-mode toggle, three example pages (Dashboard with KPI cards and a
  DataGrid, a validated Form, Settings with Tabs), icons via the built-in
  LumeoIcons, and a README covering the three ownership paths (NuGet /
  vendor via CLI / eject). Lumeo package versions are stamped from the
  lockstep version at pack time.

- **Item templates were stale and partly broken**: they emitted the removed
  `PageHeader` component (titles silently rendered as nothing), taught the
  removed Blazicons pattern, and the form scaffold's validation never
  displayed. Now: current heading pattern, LumeoIcons/SvgGlyph guidance, and
  the form scaffold uses `[LumeoForm]` with working validation (verified at
  runtime).
- **Documented template commands actually work**: `--ModelName` /
  `--ComponentName` are real symbols now (previously only `-n` worked and
  two of three documented commands failed); the templates docs page and pack
  README were rewritten to match what ships, including the previously
  undocumented `AddLumeo()` + CSS setup.

## [4.1.0-preview.8] - 2026-07-04

- **DataGrid column pinning actually moves columns now** (consumer report:
  Pin right / Unpin always appeared to land left). The grid set the correct
  sticky offsets but never reordered pinned columns in DOM order — and
  `position: sticky` can only anchor a cell to an edge, not move it across
  siblings, so a right-pinned column stayed wherever it was declared. Columns
  are now stable-partitioned (left-pinned → unpinned → right-pinned) at the
  single render chokepoint, making every pin path correct: chooser submenu,
  header control, declared `Pin=` parameters and restored layouts; multiple
  right pins stack from the right edge, mixed left+right coexist, unpin
  restores normal flow — all verified through full horizontal scroll.

- **Header-level pin control**: `Pinnable` columns now show a keyboard-
  accessible pin button in the column header (reveals on hover/focus, lit
  while pinned) with a Pin left / Pin right / Unpin menu — no more detour
  through the Columns popover.

## [4.1.0-preview.7] - 2026-07-04

- **Fixed-position popovers opened offset inside transformed ancestors**
  (consumer report: the DataGrid group-panel "Add group level" menu and the
  Columns popover opened ~a sidebar-width away from their trigger). Root
  cause: `positionFixed`'s containing-block compensation guarded idempotence
  by comparing a parsed float against the CSSOM-reserialized value; the
  precision difference made an already-folded position read as fresh, so the
  offset was folded twice within one update. The guard now compares the
  exact serialized string it wrote. Affects every fixed popover rendered
  under `transform`/`filter`/`will-change` ancestors — anchoring is now
  exact (measured 0px trigger offset, stable across re-opens). Covered by a
  real-browser E2E anchor test and a new chooser reorder regression test.

## [4.1.0-preview.6] - 2026-07-04

Ten component fixes surfaced by battle-testing the library through the new
full-page demo apps — every fix consumer-verified by removing the demos'
workarounds and adopting the real API.

- **DataGrid drag lag (MEASURED)**: `@ondragover` was bound to .NET on the
  header cells and the grid root, re-rendering the whole grid up to 60x/s
  while dragging — the group-panel path cost ~198 ms PER EVENT on a 640-row
  grid (main thread ~99% frozen). The drag hot-path no longer round-trips to
  .NET (preventDefault stays native; indicators driven by dragenter/leave):
  ~0.02 ms per event, zero long tasks, all drag semantics preserved.
- **DataGrid.Compact was dead**: cells hard-coded their padding, so the
  parameter only shrank the font. Density now flows through the context
  (live-toggleable), rows genuinely tighten, and `VirtualItemSize` auto-adjusts
  under Compact unless explicitly set.
- **DataGrid facet filters**: Select-type filter popovers showed a redundant
  operator dropdown and doubled Apply/Clear buttons — facet mode is now a
  clean checkbox list with a single Apply/Clear row.
- **DataGrid column chooser**: rows wrapped the checkbox in a label that
  cannot activate a button — the whole row is now the toggle.
- **Select/Textarea width collapse**: both shrink-wrapped by default (an
  items-start wrapper), so `w-full` on the trigger/inner did nothing. They now
  behave like block-level form controls (consumer width classes still win).
- **DatePicker**: with keyboard input enabled, clicking the input body now
  opens the calendar (previously only the small trailing icon did) — typing,
  Esc and blur-commit unchanged.
- **Gantt today-line** rendered over task-bar labels; now a subtle guide
  behind the bars with stable CSS override hooks.

- `DataGridColumnDef.Visible`/`VisibleChanged` (programmatic column
  visibility) and `DataGrid.SetColumnVisibility`.
- `CommandInput.AutoFocus` — focus the palette input on open.
- `AreaChart.GradientFill`/`GradientStops` + `EChartLinearGradient` — real
  gradient area fills (default rendering unchanged).
- `Gantt.ZoomLevels`/`DefaultZoom` — configure which zoom levels the toolbar
  offers.
- `PopoverContent.FocusOnOpen` (default true) — opt out of focus capture,
  used by the typeable DatePicker.

## [4.1.0-preview.5] - 2026-07-03

The icons release: Lumeo owns its icon story end to end.

- **Blazicons fully decoupled.** `Icon.Svg`, `MegaMenuLink.Icon`, `MegaMenuItem.Icon`,
  `PopConfirm.Icon` and `TreeViewItem.Icon` now take the new `Lumeo.IconSource` instead of
  `Blazicons.SvgIcon`; no Lumeo package references Blazicons anymore. Migration is
  mechanical: `Lucide.X` stays `Lucide.X` with `@using Lumeo.Icons` + the
  `Lumeo.Icons.Lucide` package (names match 1:1), or use the ~220 built-in `LumeoIcons.X`
  that now ship inside the core for free. Blazicons remains usable in RenderFragment
  slots — it just left Lumeo's dependency graph and public API. The NuGet-free standalone
  eject is now truly dependency-free (it previously force-installed Blazicons.Lucide).

- **`Lumeo.IconSource` + `SvgGlyph`** — the native icon model/renderer (Stroke/Fill
  styles, viewBox-driven scaling, `StrokeWidth` override, duotone content supported);
  `Icon` gained a `StrokeWidth` passthrough and renders natively.
- **16 first-party icon packs (~52,000 icons), all IsTrimmable** — unused icons trim out
  of WASM publishes (measured: 3 Tabler icons cost 7.3 KB instead of the 3.4 MB pack,
  476x smaller; regression-checked by samples/IconTrimDemo):
  Lucide 1,746 · Tabler 5,093 + 1,053 filled · Phosphor 6 weights x 1,248 ·
  Heroicons 324 x 4 cuts · Remix 1,539 x 2 · Bootstrap 2,078 · Iconoir 1,383 ·
  Material Symbols 3 styles x (3,892 + 3,892 filled) · Fluent 2,449 + 2,485 filled.
  Licenses (ISC/MIT/Apache-2.0) embedded with third-party notices; upstream versions
  pinned; generated by the new tools/Lumeo.IconGen pipeline.
- **/icons gallery** in the docs: searchable, per-pack, click-to-copy, lazy-loaded.
- **Customizer: live icon-library switching (shadcn-style)** — all 25 first-party pack
  classes switch the entire docs site live, resolved by a data-driven semantic map
  (185 names, verified against the pack manifests).
- CLI theme installer now installs first-party packs for
  lucide/bootstrap/tabler/phosphor/heroicons/remix/iconoir.

## [4.1.0-preview.4] - 2026-07-02

- **Avatar — Square/Themed avatars with initials rendered as circles**: `AvatarFallback`
  painted its `bg-muted` surface with its own hardcoded full-circle radius; the avatar's
  shape clip is larger than that circle, so clipping never took effect and any
  non-circle avatar showing fallback initials stayed round (reported against the new
  `Themed` shape in a sharp theme; had silently affected `Shape=Square` since its
  introduction). The fallback now carries no own border-radius and inherits the
  wrapper's clip — verified across Circle/Square/Themed at default and sharp radii.

## [4.1.0-preview.3] - 2026-07-02

- **Theme radius wave — ~75 hardcoded roundings across ~40 components now follow the
  radius token** (consumer report, starting from the Switch): Switch track/thumb, Badge
  (Pill variant, ping/pulse, dot, dismiss), Tabs Pill variant, Chip/TagInput/Combobox/
  Select/Cascader/TreeSelect/FilterPill remove+clear buttons, DataGrid filter chips +
  sort badge + drop indicator, Chart loading pill, AudioPlayer seekbar, FileUpload
  progress, PasswordInput strength segments, Stepper/Steps/StepsProgress/Timeline
  indicators, Result status circle, BackToTop/SpeedDial/BottomNav FABs + pill bar,
  Carousel/ImageGallery navigation, ImageCompare drag handles, Scheduler legend dots,
  Delta/Kanban/AgentMessage/ToolCallCard/ReasoningDisplay pills+dots, ThemeSwitcher
  swatches (now self-demonstrating), Avatar presence dot, QueryBuilder combinator pills
  (`rounded-[5px]` → `rounded-md`). Mechanic: `rounded-[calc(var(--radius)*N)]` with N
  sized per element so every stock theme renders PIXEL-IDENTICAL to before (the value
  exceeds half the element height and clamps to the old pill/circle) — only deliberately
  sharp themes (`--radius: 0`) square these elements off along with the rest of the UI.
  Semantic circles are deliberately untouched (radio indicators, spinners, ColorPicker
  pick-point, drawer grabber, circular avatars and their embedded followers, map-marker
  legend) and pinned by a new source-level guard test with an audited allowlist.

- **`AvatarShape.Themed`** — third avatar shape following the theme radius (identical to
  Circle at stock radii, squares off in sharp themes). `Circle` and `Square` remain
  literal contracts: a consumer who asked for a circle keeps a circle in every theme.

## [4.1.0-preview.2] - 2026-07-02

- **`TabsVariant.Underline`** — text tabs with an underline indicator: the classic compact
  style for detail pages with many tabs (previously only Default/Card/Pill existed, so
  consumers hand-built this with raw buttons + CSS). The list draws a shared baseline
  border and no background box; every trigger carries the 2px indicator border with
  constant geometry (inactive = transparent) so activation never reflows the row, and the
  underline seats exactly on the baseline. Vertical orientation moves the indicator to the
  trailing edge. Combines with the existing `TabsList.Scrollable` (arrows + overflow) for
  horizontally scrollable tab rows, with `IconReveal`, and with `AnimatedIndicator` — the
  variant then uses the sliding underline bar (the trigger's own static underline yields,
  so the indicator is never doubled).

## [4.1.0-preview.1] - 2026-07-02

Preview release bundling a full consumer-feedback wave: five bug-fix clusters and four
feature additions. Please battle-test before the stable 4.1.0.

> Changelog correction (post-release): the first three entries below shipped in this
> preview but were initially missing from this section — they landed in the preparatory
> commit directly before the release commit and the section was written against the
> release commit's diff only. Nothing about the package changed; only this document.

- **Tooltip stays open after clicking its trigger (B8, the click/pin path)** — HandleTap
  toggled the touch tap-to-pin state on EVERY click, so a desktop mouse click pinned the
  tooltip open (mouseleave only clears the hover bit; the pin kept it visible until a
  click landed elsewhere — and with the cursor resting, not even that). Now only a real
  TOUCH tap pins (`pointerdown.pointerType == "touch"`); mouse/pen/keyboard activation
  CLOSES the tooltip (Radix parity — clicking a trigger dismisses its hint), and a cursor
  resting on the trigger keeps it closed until an actual leave + re-enter. Verified with
  the exact reported automation scenario (CDP click, virtual cursor left resting).
  Together with 4.0.4's focus-visible fix and this preview's rAF watchdog, all three
  reported B8 aspects are closed; the `@key`-remount and synthetic-event workarounds are
  obsolete.
- **Tooltip arrow points into empty space when the box is clamped at the viewport edge
  (B9)** — the arrow was hardcoded box-centered (`left-1/2`/`top-1/2`); `positionFixed`
  now writes `--lumeo-arrow-x/y` (the trigger's center within the FINAL box, clamped 12px
  from the box corners — floating-ui arrow-middleware equivalent) on every reposition and
  the arrow renders at `var(--lumeo-arrow-x, 50%)`. Browser-measured: arrow-to-trigger
  delta 0px on an edge-clamped box.
- **Sidebar collapse/expand now matches shadcn 1:1** — container
  `transition-[width,translate] duration-200 ease-linear` (was 300ms eased), menu-button
  labels hard-clipped by the collapsing width instead of opacity-faded (span stays
  mounted, `truncate`), `SidebarGroupLabel` slides out via `-mt-8 + opacity-0` over
  `transition-[margin,opacity] duration-200 ease-linear` (was an untransitioned `sr-only`
  pop) and now also reveals on MiniRail hover-expand, collapsed rail is `w-12` (3rem,
  shadcn `SIDEBAR_WIDTH_ICON`) with `p-2` icon-square buttons — note the rail is 16px
  narrower than before.
- **Popovers inside dialogs land offset (B1)** — root-caused to a DOUBLE containing-block
  compensation in `positionFixed`: the fold-back ran twice per placement and subtracted the
  transformed ancestor's origin again from the already-corrected value, so a Select/
  DropdownMenu/DatePicker inside a service dialog rendered at exactly
  `intended − dialogOrigin` (empirically proven in Chromium against the shipped 4.0.4
  assets). The fold is now idempotent per axis (re-folds only when the flip/clamp logic
  wrote a fresh viewport value). Additionally: `positionAtPoint` (ContextMenu root menu)
  had NO compensation at all — added; and the Dialog/AlertDialog panel's `animate-zoom-in`
  (fill-mode:both) left a permanent identity-matrix transform making the panel a containing
  block forever — the panels now neutralize the entry animation once finished (the same
  rc.25 `getAnimations()+.finished` pattern Sheets/Drawers already used). Consumer-side
  portal/measurement workarounds are obsolete.
- **Popovers freeze when their trigger moves (Tooltip stuck at old coordinates)** —
  `positionFixed` only repositioned on scroll/resize; a trigger moving through a CSS layout
  animation (e.g. the sidebar toggle riding the collapsing sidebar) left the box at its
  opening position. An always-on rAF reference-rect watchdog (floating-ui
  `autoUpdate({animationFrame})` semantics — one rect read per idle frame, full reposition
  only on frames where the trigger actually moved) now keeps every positioned surface
  glued to its trigger.
- **Select/Combobox lists grow past the viewport and can't scroll** — two layers: a
  `max-h-96 overflow-y-auto` default on SelectContent/ComboboxContent (shadcn parity;
  Select's pinned search input stays outside the new inner scroll region), and a
  ResizeObserver in `positionFixed` re-running the viewport clamp when content grows
  after opening (async items, search filtering) — previously the clamp only ran against
  the small initial box.
- **OverlayForm/Sheet scroll bodies grew a spurious horizontal scrollbar (B3)** — the
  deliberate `-mx-1 px-1` focus-ring gutter makes the body 8px wider than its parent;
  `overflow-x-clip` now rides along at all three gutter sites (OverlayForm,
  OverlayProvider ScrollableBody, ScrollArea FocusRingGutter) plus `overflow-x-hidden`
  on DialogContent's Scrollable wrapper. Rings still render (the clip edge is the padding
  box, 4px outside the fields).
- **DatePicker/TimePicker can't shrink below ~238px (B2, bugfix half)** — the inner
  keyboard input now carries `min-w-0`, collapsing the flex min-content chain that blew
  out narrow grid columns.
- **Overlay input-hardening (B4)** — every overlay shell's full-viewport wrapper is now
  `pointer-events-none` (backdrop + panel restore `pointer-events-auto`), so a panel that
  wedges mid-animation can never leave an invisible input-eating layer over the app; and
  the focus trap now focuses the PANEL itself instead of auto-focusing the first input
  (Radix/vaul parity — no more mobile keyboard summoned mid slide-in). The reported
  drawer-over-drawer break did not reproduce on 4.0.4 in desktop or touch-emulated
  Chromium; these changes structurally remove its most likely failure shape. A device
  repro on 4.x is welcome if it still occurs.
- **Stale `Map.Cluster` XML doc (B5)** — claimed leaflet.markercluster + CDN fallback;
  clustering has been native MapLibre GL layers for a long time. Rewritten (registry/MCP
  regenerate from it).

- **`TooltipContent.Align`** (Start/Center/End, default Center) — matches
  Popover/HoverCard/DropdownMenu; RTL-aware via the existing interop; renders
  `data-align`. The 4.1 arrow anchoring is align-agnostic and keeps pointing at the
  trigger for any alignment.
- **`OverlayOptions.ShowCloseButton` / `DialogContent.ShowCloseButton` /
  `SheetContent.ShowCloseButton`** (bool?, default null = legacy `!PreventClose`
  coupling) — force the X on a modal overlay or hide it for custom chrome. The X now
  carries stable hook classes (`lumeo-dialog-close` / `lumeo-sheet-close`) and `z-10` so
  consumer sticky headers can't paint over it.
- **`DatePicker.FullWidth` / `TimePicker.FullWidth` / `DateRangePicker.FullWidth`**
  (Button precedent) — threads `w-full` through the previously shrink-wrapped
  Popover/PopoverTrigger wrapper chain; `Popover` itself gained a `Class` parameter.
- **Maps cluster + interop APIs (W4)**: `MapMarker.ClusterExclude` (render a marker as a
  DOM marker outside the cluster source — highlighted markers no longer vanish into
  cluster bubbles), `MapMarker.Properties` (custom GeoJSON feature properties for
  cluster aggregation), `Map.ClusterProperties` / `Map.ClusterColorExpression` /
  `Map.ClusterRadius` / `Map.ClusterMaxZoom` (raw MapLibre passthrough, defaults
  byte-for-byte unchanged), and `Map.ElementId` + the `getMap(elementId)` JS export for
  direct MapLibre instance access.
- **Self-hosting docs for the CDN-loaded engines (B5)** — `window.lumeoCdn` override now
  documented on the Map docs page (mirroring the PdfViewer page) and surfaced to the MCP
  via `<gotcha>` annotations on Map and PdfViewer.

## [4.0.4] - 2026-07-01

- **Tooltip — a Tooltip-wrapped clickable trigger stays open after a click**: clicking any
  Tooltip-wrapped clickable element (a button, an icon action, a sidebar toggle, ...) left
  its tooltip visibly stuck open until focus happened to move elsewhere for an unrelated
  reason, long after the mouse moved away. Root cause: `Tooltip`'s `focusin` handler opened
  on ANY DOM focus — but a native `<button>` keeps DOM focus after a mouse click (nothing
  clears it), so the tooltip stayed open on plain `:focus`, not the browser's own
  `:focus-visible` signal (true for keyboard navigation, false for a mouse-click focus, in
  supporting browsers). Fixed by gating `Tooltip.HandleFocusIn` on a new
  `IComponentInteropService.IsActiveElementFocusVisible()` check — real keyboard/
  programmatic focus still opens the tooltip immediately (unchanged), a click-driven focus
  no longer does. Confirmed via `document.activeElement` DOM inspection that a manual
  `.blur()` alone (no mouse movement) hid the tooltip — pinpointing plain `:focus`, not
  `:focus-visible`, as the trigger.

## [4.0.3] - 2026-07-01

- **Sidebar — asymmetric label-fade timing against the container's collapse/expand
  transition**: `SidebarMenuButton`'s label faded with `duration-150` and an asymmetric
  delay per direction (`delay-0` collapsing, `delay-150` expanding) and its own explicit
  `ease-out` curve, while `SidebarComponent`'s width transition always used
  `duration-300` with no delay, in either direction, using Tailwind's implicit default
  timing function. Collapsing, the label finished fading at t=150ms while the container
  kept shrinking until t=300ms — two visibly sequential steps. Expanding, the 150ms delay
  plus 150ms duration happened to sum to exactly 300ms, matching the container's finish
  time by coincidence — so expanding only *looked* synced at the very end, not throughout.
  Fixed by matching the label's duration, delay (none), and easing (falls back to the
  same Tailwind default the container implicitly uses) to the container exactly, in both
  directions — reported and confirmed via decompiling the shipped 4.0.1 package.

## [4.0.2] - 2026-07-01

- **DataGrid — ServerMode grouping: expand/collapse "reloads" the whole grid**: after the
  4.0.1 fix restored expand/collapse *state*, a follow-up report showed every toggle
  visibly rebuilding the grid. Root cause: `RegroupServerItems()` regrouped from
  `_displayedItems` — the expand-filtered OUTPUT of the previous regroup — instead of the
  raw server page. A purely local toggle (no `Items` reassignment) therefore regrouped an
  ever-shrinking subset: collapsing group A removed A's rows from `_displayedItems`, so
  collapsing a second, different group B then regrouped from a list that no longer
  contained A's rows at all, making A's group *row* vanish entirely (not just its
  children). With `GroupsExpandedByDefault=false`, the very first expand click saw
  `_displayedItems` already empty and fell into the "no items" branch, wiping grouping
  outright — a full empty-state subtree swap that looked like the whole grid reloaded.
  Fixed by tracking the raw server page in a dedicated field (`_serverPageItems`), set
  only on a genuine `Items` refresh, and regrouping from that instead.
- **Select — Multiple mode trigger tags show the raw value, not the item's label**: with
  composition-mode `<SelectItem>` children (not the data-bound `Items` prop) and
  pre-selected `Values`, the closed trigger's removable tags echoed the raw selected value
  (e.g. a Guid) instead of the matching `SelectItem`'s rendered label — even though the
  open dropdown showed the correct labels for the same options. Root cause: the tag
  markup never resolved a label at all, and an explicit `<SelectTrigger ChildContent="…">`
  meant to override it was silently ignored whenever `Multiple=true` and a value was
  selected (the tag branch's condition was structurally identical to the ChildContent
  branch's guard, making the latter unreachable — not overridden at runtime, dead code).
  Fixed: an explicit `ChildContent` now always wins when a value is selected, and the
  default tag rendering resolves each value's label — from `ItemText`/`ItemValue` in
  data-bound mode, or from a composition-mode `SelectItem`'s registered content once the
  dropdown has been opened at least once in the session (a value pre-selected before the
  popover has ever been opened has no label to look up yet; `ChildContent` remains the
  reliable override for that case). Also fixed a related layout bug: once Multiple-mode
  tags wrapped to 2+ lines, the trigger's `items-center` vertically centered the
  chevron/clear icon across the full wrapped height instead of aligning it with the first
  tag row.

## [4.0.1] - 2026-07-01

- **DataGrid — ServerMode + grouping**: a manually collapsed group was silently re-expanded
  (and the rows the user was looking at could appear to vanish) on the next page turn, sort,
  filter, or search. `RegroupServerItems()` ran after every server refresh and intersected the
  tracked expand/collapse state against the CURRENT page's group keys — a new server page's
  keys are almost never identical to the previous page's, so the intersection wiped out nearly
  all of the user's manual choices and re-seeded every "new" key from `GroupsExpandedByDefault`
  (default `true`). Fixed for both single-level and multi-level (`GroupByFields`) grouping: a
  group key/path, once seen, keeps its expand state for the life of the grid instead of being
  forgotten the moment it's not on the current page. Also fixed a compounding issue where
  `RequestServerData` sent the static `GroupBy` parameter to `OnServerRequest` instead of the
  actual runtime grouping (group-panel / `GroupByFields`), so a consumer's server callback never
  saw what the user was really grouping by. Verified with new regression tests simulating a real
  multi-page server refresh (not the single static batch prior tests used), and independently
  against a real ASP.NET Core API + Blazor WASM client serving 5,000 rows across 200 pages.

## [4.0.0] - 2026-06-26

Two things in one release: a Radix/Base-UI/shadcn **parity audit** (accessibility, RTL, theming, the FormGenerator, and the MCP/CLI — additive and opt-in; the OKLCH and logical-utility changes are visually/behaviourally identical in LTR) **and** a library-wide **correctness hardening** pass — an adversarial "battle-test" of all 164 components that fixed ~355 confirmed bugs, each with a bUnit regression test (suite 4,983 green). There are **no API-signature breaks**; the major bump signals the scope and the handful of observable **behaviour** changes listed under **Changed** below (and in `MIGRATION.md`).

This release also ships the CLI's headline **NuGet-free "standalone" eject**: `lumeo add` can now vendor a component *and its full runtime closure* as source, so a project compiles and runs with **zero Lumeo/satellite `PackageReference`** — proven across all 164 components.

- **CLI — NuGet-free "standalone" eject**: `lumeo init --standalone` (or `lumeo eject` on an existing project) makes `add` vendor each component **plus the shared runtime it needs** (the `Internal`/`Services`/`Theming`/interop closure, once, into `_LumeoRuntime/`) as source under the `Lumeo` namespace, so the project builds and runs with **zero Lumeo/satellite `PackageReference`**. Satellites (DataGrid, Editor, …) vendor their source + JS too; external NuGet deps a component genuinely uses (e.g. QRCoder, Mammoth) are still installed. Validated by building **all 164 components** standalone (164/164 green).
- **DirectionProvider**: new component — `<DirectionProvider Direction="LayoutDirection.Rtl">` sets the native `dir` (and cascades it) so descendant layout mirrors for RTL.
- **Tabs**: `IconReveal` — inactive triggers collapse to icon-only and the active trigger smoothly animates its text label open next to the icon (CSS grid `0fr → 1fr`).
- **Card**: `CardTitle` (`<h3>`) + `CardDescription` (`<p>`) sub-components (shadcn composition parity).
- **Avatar**: `StatusLabel` — accessible name for the status dot so the status isn't conveyed by colour alone (WCAG 1.4.1).
- **Chart**: `AriaLabel` — exposes the canvas as `role="img"` with a text alternative (WCAG 1.1.1).
- **AlertDialogTrigger / DrawerTrigger**: `AsChild` — fold the trigger onto a single child element (no `div[role=button]` wrapping a real `<button>`, WCAG 4.1.2).
- **`Lumeo.Cx`**: the class-merge helper (shadcn `cn()` equivalent) is now public for use in consumer components.
- **DataTable**: `ItemKey` — decide row selection by a stable key, so selection survives an `Items` refresh that re-supplies value-equal but reference-distinct rows (the mainstream async reload).
- **RadioGroup**: the `Name` parameter now emits a hidden input carrying the selected value, so the group participates in native form submission.
- **Gantt**: the init-only options `Readonly` / `TodayHighlight` / `BarHeight` / `ColumnWidth` now apply to a live chart when changed after init (new `gantt.refresh` interop path).
- **Interop (internal/advanced)**: `IComponentInteropService.GetOrderedDescendantIds` (DOM-order roving navigation) and `GanttRefreshAsync` — both default-implemented, additive.

### Improved
- **RTL**: migrated the component library's directional Tailwind utilities to logical ones (`ml-→ms-`, `left-→start-`, `text-left→text-start`, `rounded-l→rounded-s`, `border-l→border-s`, …) — identical in LTR, mirrored in RTL.
- **FormField a11y**: `aria-describedby` (help/error) now reaches every form control (Checkbox, Switch, RadioGroup, Slider, Select, Combobox, …) — not just Input; single-focus controls also adopt the field's `ControlId` so `<label for>` resolves.
- **Accessibility**: `aria-current` on the active Sidebar nav item + Scrollspy link; overlay entry animations now honour `prefers-reduced-motion`; Cascader gained arrow-key roving; Menubar trigger/item roles.
- **Overlays**: scroll-lock compensates for the scrollbar width so opening a Dialog/Sheet/Drawer no longer shifts page content.
- **LumeoFormGenerator**: TimeOnly/TimeSpan→TimePicker, `List<string>`→TagInput, MultilineText→Textarea, Phone/Url→typed Input; `[Range]`→Min/Max, `[StringLength]/[MaxLength]`→MaxLength+counter; `bool` no longer implicitly required; nullable numerics clear to null; `[Display(Order)]` field ordering.
- **MCP**: type-bound enum validation (`Size="Large"` is now caught), cascading-gated parent-child rule, per-component test-coverage and `[EditorRequired]` surfaced, and a new `lumeo_get_a11y` tool (roles, keyboard keys, focus).

- **Theme**: the entire colour palette (base + all 8 themes, 878 tokens) migrated from HSL to **OKLCH** — exact 1:1 conversion (brand identity unchanged), matching Tailwind v4 / current shadcn.
- **Badge (behaviour)**: a removable badge no longer optimistically hides itself on remove-click — visibility is now fully controlled (data-driven), matching the controlled-component model. Remove the item from your own model in `OnRemove` (and `@key` your list). See `MIGRATION.md`.
- **Progress / Gauge / RingProgress (behaviour)**: out-of-range values are clamped (`Value=150, Max=100` → `100`; negative → `0`); the indeterminate state reports `aria-busy="true"` and omits `aria-valuenow` instead of rendering a stale determinate value.
- **Internal state survival (behaviour, library-wide)**: selection / checked / expand-collapse / active index / page / search / scroll / in-progress edit state now **survives** a same-content data refresh, an empty→refill async load and unrelated parent re-renders. If any code relied on that state *resetting* on an unrelated re-render, it no longer does.
- **Roving keyboard order (behaviour)**: RadioGroup / ToggleGroup / Segmented / Stepper / Splitter / Steps / Accordion track the **live DOM order** for arrow-key navigation, numbering and neighbour resolution after a keyed reorder (previously mount-order).

### Fixed (battle-test campaign — ~355 bugs across all 164 components, each with a bUnit regression test)
- **State-on-data-change**: internal UI state survives same-content `Items`/`Value` refreshes, empty→refill async loads, sort/filter/reorder/add/remove, and unrelated `[Parameter]` changes — across DataGrid, DataTable, Select, Combobox, Cascader, TreeView, TreeSelect, Transfer, PickList, Calendar, Carousel, Pagination, Scheduler, Tabs, Tour, NavigationMenu, MegaMenu, Menubar, Form, FileManager, Sortable, and ~40 more.
- **Keyboard & ARIA**: correct roving tabindex, focus restore/trap, and ARIA (`aria-expanded`/`selected`/`current`/`busy`/`pressed`, roles, accessible names, `aria-hidden` on decorative icons, `inert` on aria-hidden clones) across forms, overlays, menus, data widgets and presentational components.
- **Edge data**: empty/null/whitespace/single/duplicate-key/out-of-range/huge inputs no longer crash or misrender — guards, clamps, bounds checks and culture-invariant number/decimal formatting in inline styles and SVG, library-wide.
- **Lifecycle**: timers, `IntersectionObserver`/`ResizeObserver`, `requestAnimationFrame` animations, `DotNetObjectReference`s and event subscriptions are torn down on dispose; no `StateHasChanged` after dispose; first-render registrations no longer latch on a not-yet-ready render (late-arriving ids/data now register).
- **Reorder class**: the keyed-reorder-with-reuse, middle-insert, Steps renumbering and Splitter neighbour legs are closed via a DOM-order interop probe consulted at navigation/render time (`GetOrderedDescendantIds`).

### Fixed (real-browser docs-QA pass — verified with agent-browser)
- **Render crash — Razor comment inside an element tag** (Cascader, PdfViewer): a multi-line `@* … *@` comment placed BETWEEN an `<input>`'s attributes was emitted as a literal attribute name, throwing `setAttribute` InvalidCharacterError in a real browser and taking the whole page down. Moved the comments outside the tags; added a lint test (`RazorCommentInTagGuardTests`) — this class is invisible to bUnit and to a `pageerror`-only sweep.
- **Input `Type="file"` crash**: `value` was bound on the `<input>` unconditionally, and a file input rejects any non-empty `value`, so picking a file threw InvalidStateError. The value binding is now dropped for file inputs.
- **Form-field layout (parity)**: every standalone form field (Input, Mention, Cascader, Select, Combobox, the date/time pickers, Slider, Switch, Checkbox, … 23 components) rendered its label, control and helper/error as loose root-level siblings, so inside a flex / centered container they splayed into a row (label beside the control). Each now renders as one self-contained vertical block; inline-label fields keep their inline label.
- **FormField + FormMessage** no longer render the validation error twice — `<FormMessage>` defers to FormField's own error by default; the new `FormField.AutoRenderMessage="false"` hands error rendering to a child FormMessage for pure composition.
- **Gauge (Arc)**: the value sits centred in the semicircle instead of pushed up toward the apex (size-independent).
- **ImageGallery**: the grid no longer collapses to zero — cells gained an aspect ratio and the grid keeps a definite width as a content-sized flex child.
- **TagInput**: the "max tags reached" helper showed the raw key `TagInput.MaxTagsReached`; added the missing EN + DE localization defaults.
- **Form demo**: removed a redundant `<FormMessage/>` that duplicated the validation error.

## [3.19.0] - 2026-06-18

Two P1 audit features from the backlog. Additive and opt-in.

- **Drawer (#218)**: vaul-style snap points — `SnapPoints` (ascending fractions, e.g. `[0.4, 0.75, 1]`) + two-way `ActiveSnapPoint`/`ActiveSnapPointChanged`. A Top/Bottom drawer rests at fractional heights, drags between them, and dismisses below the lowest snap; programmatically setting `ActiveSnapPoint` moves it. A `PreventClose` drawer still snaps but never dismisses.
- **Drawer (#218)**: velocity/flick dismiss — a fast flick in the dismiss direction closes even below the distance threshold, tunable via `LumeoGestureOptions.SwipeDismissVelocity` (default `0.4` px/ms; `0` = distance-only).

- **Drawer (#218)**: the backdrop now uses the `--color-overlay-backdrop` theme token instead of a hardcoded `bg-black/80`, matching Sheet/Dialog (light + dark).
- **RichTextEditor (#320)**: the floating bubble toolbar is keyboard-operable — `Alt+F10` moves focus into it (ARIA Authoring-Practices pattern), arrow/Home/End rove between buttons, `Escape`/`Tab` return focus to the editor (staying inside any modal focus trap), and it hides once focus leaves.
- **RichTextEditor (#320)**: the slash/mention suggestion listbox wires `aria-activedescendant` (+ `aria-controls`/`aria-expanded`) on the editor so screen readers announce the highlighted option as you arrow through.

## [3.18.0] - 2026-06-18

Bundled audit-backlog batch closing the remaining cleanly-doable component gaps in a single release. All additive and opt-in — existing usage is unchanged.

- **Pagination (#210)**: opt-in data-driven mode — set `Page` + `TotalPages` (or `TotalItems` + `PageSize`) and the component renders the full page list itself (prev/next, first/last boundaries, sibling window, `…` ellipsis gaps) and raises `PageChanged`. `SiblingCount`/`BoundaryCount` tune the window. The original `ChildContent` composition still works.
- **Button (#269)**: `Href` — when set, the button renders as an `<a>` link-button (shared loading/icon/content), with `aria-disabled` + `pointer-events-none` when disabled or loading.
- **Heading (#295)**: `As` — render the heading as any element (e.g. a `div` styled as a heading) while keeping the visual `Level`/`Size`, for correct document outline without forcing an `h1`–`h6` tag.
- **Code (#196)**: `Source` + `Language` (emitted as `data-language`) and a pluggable `Highlighter` hook (`Func<string, string?, MarkupString>`) so a syntax highlighter can be supplied; without one, `Source` is HTML-escaped.
- **Filter (#319)**: `FilterBar` gains a data-driven `Filters` model (`FilterDescriptor` = Field / Operator / Value) that auto-renders a dismissable `FilterPill` per descriptor and raises `OnRemoveFilter`; `FilterPill` gains an `Operator` (renders `Field op Value`, or `Field: Value` without one). The `Pills` slot still composes alongside.
- **Sparkline (#275)**: opt-in `ShowTooltips` — a marker dot at every point with a native SVG `<title>` (hover value), no JS. Off by default.
- **Statistic (#273)**: `ValueContent` slot overriding the formatted value — drop in a `NumberTicker` (Lumeo.Motion) for an animated count-up instead of duplicating animation in core.
- **Gauge (#277)**: `LabelContent` slot overriding the center/label text — same composable pattern for an animated value.

- **SparkCard (#276)**: the inline chart now delegates to the full `Sparkline`, so it gains `Type` (Line/Area/Bars), `ShowArea`, `ShowLast`, `ShowTooltips` and `SparkColor` instead of a hardcoded less-capable polyline. A single data point still renders no chart (two-point minimum preserved).

## [3.17.0] - 2026-06-18

Bundled feature batch (component capability gaps), all additive and opt-in.

- **Barcode (#291)**: `OnError` callback (fires the encoding error message, or `null` on a successful encode) as a validation hook; the quiet zone now scales with `BarWidth` (10× the narrow module) instead of a fixed 10px.
- **Highlighter (#293)**: opt-in `RegexMode` — `Highlight`/`HighlightTerms` are treated as regular-expression patterns instead of literal text; invalid patterns fall back to plain rendering.
- **Grid (#250)**: opt-in `Responsive` — collapses to 1 column (mobile) / 2 (sm) and expands to `Columns` at `lg`, using purge-safe static utility strings for 1–6 columns. Off by default.

## [3.16.0] - 2026-06-18

a11y / i18n polish and small improvements following 3.15.0.

- **ReasoningDisplay (#305)**: opt-in `Markdown` rendering (+ a `MarkdownRenderer` hook), mirroring `StreamingText` — reasoning traces render as markdown via the built-in XSS-safe renderer (or a supplied one). Plain-text default unchanged.
- **ButtonGroup (#270)**: `AriaLabel` parameter; the group now exposes `role="group"` (roving tabindex stays a `Toolbar` concern, by design).

- **Stepper (#245)**: Next/Back/Finish nav labels are localized (fall back to `L["Stepper.*"]`, shipped for every locale); explicit `*Label` params still override.
- **Result (#284)**: `role` is `alert` (assertive) for Error/Forbidden/ServerError and `status` otherwise, so assistive tech interrupts on failures.
- **BackToTop (#247)**: the scroll handler is throttled to one check per animation frame and only crosses the JS↔.NET interop boundary when visibility actually flips.

- **Collapsible (#238)**: in controlled mode (`@bind-Open`), `Toggle` no longer mutates its own `Open` parameter — it fires `OpenChanged` and renders from the parent's value, fixing a desync when the parent rejected/ignored the change.

## [3.15.0] - 2026-06-18

Follow-up to the 3.14.0 audit pass: the two P0 cascade/layout fixes (browser-verified by new Playwright e2e coverage) plus a small a11y/i18n polish batch.

- **Overlays (#172)**: `positionFixed` — the shared positioner for Popover, Select, DropdownMenu, ContextMenu, Menubar and Tooltip — now positions with explicit `top`/`left` only and never sets a CSS `transform`. A transformed overlay established a containing block for its `position:fixed` descendants, so a nested overlay (`DropdownMenuSubContent`, popover-in-popover, ContextMenu/Menubar submenu) resolved against the transformed parent instead of the viewport and opened off-screen. All viewport flip/clamp guards are preserved; visually identical for the existing cases.
- **Icon (#173)**: size utilities (`h-/w-/size-`) now win under Tailwind v4. Blazicons injects an unlayered `svg[blazicon]{width:1em}` rule that beat `@layer utilities`, silently collapsing every icon to the font size; an unlayered, higher-specificity `revert-layer` reset defers sizing back to the utilities layer (a consumer's own `Class` override still wins). Effective on the unlayered `<link>` path; layered-import consumers add the reset themselves (documented inline).
- **RingProgress (#278)**: `aria-valuenow` is clamped/rounded into `[aria-valuemin, aria-valuemax]`.

- **Hero (#297) / CTASection (#298) / FeatureGrid (#299)**: the `<section>` landmark now carries an accessible name via `aria-labelledby` → its heading, so assistive tech exposes it as a named region.

- **Spinner (#282) / Skeleton (#281)**: `AriaLabel` parameter (defaults to "Loading") so the screen-reader name is localizable without a visible label.

## [3.14.0] - 2026-06-17

Library-wide audit-remediation release. Building on the 3.13.0 audit, this release closes accessibility (keyboard/ARIA), lifecycle, interop-safety, culture and motion gaps across ~60 components, adds several audit-flagged feature gaps, and honors `prefers-reduced-motion` across the Motion package. Full per-component detail is tracked in audit issues #171–#335.

- **Calendar / DatePicker**: multiple-date selection (`IsMultiple` + `Values`/`ValuesChanged`).
- **Transfer**: per-panel select-all and per-item `Disabled`.
- **PickList**: within-list keyboard reordering + listbox ARIA.
- **Carousel**: autoplay with pause-on-hover/focus and indicator dots.
- **Table**: `TableFooter`, `TableEmpty`, `TableSkeleton` and a striped helper.
- **Tabs**: manual activation mode and overflow scroll arrows.
- **Sidebar**: mobile off-canvas sheet and a keyboard toggle shortcut.
- **Resizable**: collapsible panels, `OnLayout`, and persisted/saved layout round-trip.
- **Text**: `LineClamp` plus a wider semantic-element set.
- **Watermark**: optional image-source watermark mode.
- **ToolCallCard**: copy-to-clipboard for input/output.
- **AudioPlayer**: playback-rate, skip and volume controls.
- **ThemeSwitcher**: live OS `prefers-color-scheme` and cross-tab sync.

- **prefers-reduced-motion** is now honored across `Lumeo.Motion` (AnimatedBeam, BlurFade, BorderBeam, Marquee, NumberTicker, ShimmerButton, Sparkles, TextReveal, Confetti, Dock, TouchRipple) and overlay exit animations; NumberTicker now formats with the current culture's group/decimal separators.
- **Keyboard / ARIA**: roving-tabindex, arrow/Home/End navigation, typeahead and focus management added or hardened across Select, Combobox, TreeSelect, Cascader, Mention, Command, Menubar, MegaMenu, DropdownMenu, ToggleGroup, Segmented, Calendar, Accordion, Steps, Toolbar, SpeedDial and Sortable — disabled items are skipped consistently and keyboard activation no longer double-fires.
- **Overlays**: Sheet/Dialog/Popover/Tooltip focus management, theme-token backdrops, exit animations and Escape handling hardened (a pinned Tooltip now dismisses on Escape; the Sheet no longer flickers on close).
- **Interop safety**: JS-disconnect/disposal guards added across component teardown; the `prefers-reduced-motion` query and theme listeners are pruned/guarded on async failure.

- **Splitter**: dead `Collapsible` wired up, late-added panes are now sized, drags clamp at min/max (instead of being rejected), a collapsed pane re-expands on drag-out, and an `OnAfterRender` redistribution loop that could overflow the stack is closed.
- **CodeEditor**: core/language/theme/minimap module caches are keyed by resolved ESM base, so two editors pointing at different bases no longer share modules.
- **DataGrid**: a Select column's operator is preserved on Apply instead of being reset to `Contains`.
- **FileViewer**: `IHttpClientFactory` resolution honors a registered factory for the default (unnamed) client.
- **Calendar / DateTimePicker / TimePicker**: Min/Max enforcement on the time columns, keyboard grid/list navigation, and AM/PM inference when no value is selected.
- **SignaturePad**: real per-stroke SVG export, tap-only signatures are no longer dropped, and clear/keyboard a11y.
- **Avatar**: image→fallback chain and a working `AvatarGroup` `Max`/`+N` overflow.
- **BottomNav**: active-route matching ignores query/fragment.
- **Scrollspy / Affix / ScrollArea / Timeline / Separator**: offset-aware click-scroll, resize-aware fixed width, cross-browser scrollbars, alternate layout and semantic `role`.
- **Markdown**: link URLs containing `_`/`*` are no longer corrupted by the emphasis passes.
- **Window**: shared z-index is assigned atomically.

- **#172** (nested-overlay positioning under a transformed parent) and **#173** (Icon sizing under Tailwind v4) require real-browser verification and ship in a dedicated follow-up PR.
- **#320** (RichTextEditor TipTap extensions) and **#196** (Code syntax highlighting) pend an npm/bundle build step.

## [3.13.2] - 2026-06-12

- **DataGrid (ServerMode)**: group expand/collapse now regroups the server-delivered page — the toggles previously dispatched to the client pipeline, which re-applied client filtering/sorting/paging over the server page and could corrupt the row set on every collapse.
- **DataGrid (layouts)**: filter values restored from JSON (persisted layouts, the `SavedLayout` parameter, named layouts) are normalized from `JsonElement` to CLR primitives — number/date filters compared lexicographically before (`">5"` dropped `10`), and ServerMode consumers now receive comparable descriptor values in `OnServerRequest`.
- **DataGrid (layouts)**: removing a group chip after a layout restore unhides its auto-hidden column — the un-group snapshot is now seeded for restored chips.

## [3.13.1] - 2026-06-12

- **Tabs (Card variant)**: the active tab now fuses with the list's edge border — axis-aware seam (bottom for horizontal, right for vertical) with squared seam corners; previously the card floated above the border line with the base rounding peeking through.
- **Tabs (Card variant)**: switching tabs no longer flickers — every card tab carries identical box metrics (inactive tabs render a transparent border), so activation swaps colors only instead of animating a 2px layout reflow.

## [3.13.0] - 2026-06-11

Component-audit hardening release: a full-library audit benchmarked against shadcn/ui, Blueprint, MudBlazor and Ant Design, fixing keyboard/ARIA, lifecycle and culture defects across ~25 components, plus a consumer-reported DataGrid grouping regression.

> Changelog entries between `1.0.0-beta.5` and this release were tracked via git history and GitHub Releases rather than this file.

- **DataGrid**: `GroupBy`/`GroupByFields` silently rendered a flat grid with declarative `<DataGridColumnDef>` children (regression since 3.10.0) — the grouping seed validated against an empty column list before the children registered. It now re-seeds when the matching column arrives, and warns when a group field matches no column.
- **Select / Combobox**: data-bound keyboard navigation (Arrow/Home/End/Enter) was dead; search cleared registrations and showed a spurious empty state; the trigger click could not reliably close the popup.
- **Menus** (Menubar / DropdownMenu / ContextMenu / MegaMenu): Enter/Space double-fire made keyboard activation a no-op; click-outside couldn't dismiss via the trigger; ContextMenu key handlers were unreachable; Menubar gained full WAI-ARIA navigation.
- **DatePicker**: typed input bypassed `MinDate`/`MaxDate`/`IsDateDisabled`; range presets set only the start date. Calendar now follows external value changes, DateTimePicker keeps a pending time, and wheel pickers resync on external change.
- **TreeView**: selection two-way binding never fired, children were keyboard-unreachable, and check state corrupted under search.
- **Tabs**: arrow keys activated disabled tabs and navigated to removed (closable) tabs; Delete now closes a closable tab.
- **Stepper**: blank first render, duplicated steps on re-render, ghost steps after removal; `KeepMounted` now works.
- **RadioGroup**: arrow keys selected disabled radios; removed radios stayed keyboard targets.
- **Accordion / Collapsible**: collapsed content kept focusable children in the tab order; `div`-button triggers scrolled the page on Space.
- **Overlays** (Dialog / AlertDialog / Sheet / Drawer): focus is restored to the trigger on close; Escape no longer closes all nested overlays at once; AlertDialog focuses Cancel first.
- **FileUpload** drop now adds files; **Window** drag/resize uses pointer capture; **Resizable** seeds from panel default sizes and supports keyboard resize; **Command** has full keyboard navigation.
- **Barcode**: the `Format` parameter is honored — real Code 39 and EAN-13 encoders (previously rendered as Code 128 regardless); encoding errors are now visible instead of blank.
- **Culture / locale**: AspectRatio and Watermark emitted invalid CSS/SVG on comma-decimal cultures; Statistic mis-parsed localized decimals; Progress now clamps negative values; Grid/Container shipped the missing `grid-cols-9..12` / `max-w-*` utilities.
- Mention no longer throws on empty results or mangles inserted text; QRCode logo scales to the code; Tour scrolls off-screen targets into view; Carousel and Splitter keyboard traps removed; Input `Clearable` no longer drops focus while typing; Form can submit again after a fixed validation error.

- `IComponentInteropService.RegisterPreventDefaultKeys` — key-selective, IME-safe `preventDefault` applied synchronously in the native event dispatch (replaces the render-time `@onkeydown:preventDefault` flag pattern).
- Localization keys for ConfirmButton, PickList, FileManager, AudioPlayer, ThemeSwitcher, Stepper and Breadcrumb strings across all 14 locales.
- Wired previously-inert parameters: Tooltip `Offset`, HoverCard `Side` (Left/Right), PopConfirm `Placement`, SpeedDial `Icon`/`Variant`, Highlighter `Tag`.
- DevX: a `SessionStart` hook installs the .NET SDK + npm dependencies in Claude Code remote containers.

## [1.0.0-beta.5] - 2026-03-19

- Updated NuGet package description (90+ → 103 components, added test count and feature list)
- Updated README with accurate component count, themes, and install command

## [1.0.0-beta.4] - 2026-03-19

- Checkbox: Label, Description parameters with auto-Id for form association
- RadioGroupItem: Description text support
- Steps: Error state per step with red X icon, custom Icon slot
- Popover: Arrow support (ShowArrow parameter matching Tooltip pattern)
- 48 new unit tests covering all upgraded component features (1,124 total)
- Form Validation guide with DataAnnotations, custom validation, and complete examples
- Contributing guide with setup, component creation, testing, and code style docs
- "When to Use" and "Related Components" sections on 62 more component pages (82 total)
- API reference tables now on all 136 component documentation pages

- Home page stats updated (75→103 components, 7→8 themes)
- Chart patterns integrated into Patterns page with filter category
- All hardcoded colors replaced with CSS variables (Avatar, Statistic, Result, KanbanCard)

- MentionPage Razor escape for @user syntax
- Statistic and Result test assertions updated for CSS variable colors

## [1.0.0-beta.3] - 2026-03-19

- Dialog size variants (Sm, Default, Lg, Xl, Full) and Scrollable content mode
- Drawer multi-position support (Top, Right, Bottom, Left) with direction-aware swipe
- Alert: Title, Description, Icon slots, ShowIcon with default icons per variant, AutoDismiss timer
- Input: 3 size variants (Sm, Default, Lg) and Clearable mode with X button
- Tooltip arrow support with configurable Offset and fade animation
- Badge: Pulse ping animation on dot variant and Icon slot
- Accordion: DefaultValues for initially open items and Disabled items
- Skeleton: Wave/Shimmer animation variant
- Spinner: Dots and Bars variants, Label text, and Color override
- HoverCard: Side parameter (Top/Bottom positioning)
- Tabs: Disabled tabs with aria-disabled and TrailingContent on TabsList
- Button: FullWidth mode, LeftIcon and RightIcon slots
- Progress: Circular SVG variant with ShowValue and configurable StrokeWidth
- Avatar: Square shape option and Status indicator (Online, Offline, Away, Busy)
- Switch: Loading spinner state and OnLabel/OffLabel text
- Select: Disabled items and Placeholder text on trigger
- Combobox: EmptyText for no-results state and Creatable mode
- NumberInput: Arrow key and mouse wheel support, Prefix/Suffix text
- Textarea: Character count display, MaxLength indicator, Resize control
- Accessibility guide page with ARIA roles, keyboard patterns, and focus management docs
- Changelog page in docs site with full release history
- API reference tables for 30 additional component documentation pages (55 total)
- "When to Use", "Keyboard Interactions", and "Related Components" sections on 20 component pages

- Animation keyframes and utility classes now ship in lumeo.css for NuGet package consumers
- Production-quality spring easing curves on all animations
- Rating colors now use themeable `--color-rating` CSS variable instead of hardcoded yellow

- Broken animations for NuGet package consumers (keyframes were only in docs site)
- Missing `animate-toast-in` — Toast slide-in animation was never defined
- Added aria-labels to PasswordInput toggle, TagInput close buttons, DatePicker clear button, Carousel navigation
- Rating keyboard navigation with Arrow keys and improved star labels

## [1.0.0-beta.2] - 2026-03-12

- 14 new components: Cascader, ColorPicker, DateRangePicker, DateTimePicker, Filter, ImageCompare, InplaceEditor, InputMask, Kanban, MegaMenu, Mention, NumberInput, PasswordInput, SortableList
- Keyboard shortcuts: R to shuffle themes, Ctrl+D for dark mode, Ctrl+/ for shortcuts help
- Redesigned WASM loader with animated splash screen and ripple animation
- Floating navbar and floating sidebar design for docs site
- NuGet package icon (Lumeo logo)

- All UI corners respect CSS radius variable for zero-radius presets like Lyra
- Customizer sidebar moved to header button with Ctrl+B toggle shortcut
- CommandEmpty now always renders regardless of Command context

- Customizer radius bug where radius values did not apply correctly
- Mobile docs improvements and API table horizontal scrolling
- Floating nav sticky positioning
- Splash screen CSS compatibility with Tailwind CDN
- Em dash encoding issues in page titles

## [1.0.0-beta.1] - 2026-03-12

- 90+ Blazor components built on Tailwind CSS v4
- Layout primitives: Stack, Flex, Grid, Container, Center, Spacer
- Typography primitives: Text, Heading, Link, Code
- 30 chart types via ECharts integration (Bar, Line, Area, Pie, Donut, Radar, Scatter, Heatmap, TreeMap, Sankey, Funnel, Gauge, WordCloud, GeoMap, and more)
- DataGrid with sorting, filtering, column resize, inline editing, row selection, and CSV/JSON export
- Programmatic OverlayService for opening Dialog, Sheet, Drawer, AlertDialog from C# code with awaitable results
- ToastService with success, error, warning, info variants and promise support
- ThemeService for runtime theme and dark mode switching
- KeyboardShortcutService for global keyboard shortcuts
- 7 color themes: Zinc (default), Blue, Green, Rose, Orange, Violet, Amber, Teal
- Dark mode via CSS variable swaps
- Comprehensive documentation site with live demos and API reference
- 45+ pattern examples showing real-world component compositions
- GitHub Pages deployment at lumeo.nativ.sh

- Chart color resolution for modern CSS color formats (oklch, hsl, color())
- WordCloud extension race condition causing render failures
- Bar chart rendering broken by NaN borderRadius from CSS variable parsing
- Chart label text stroke artifacts on Sankey, Graph, Area, and Funnel charts
