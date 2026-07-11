# Changelog

All notable changes to Lumeo will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [4.2.0] - 2026-07-11

DataGrid column/row interaction suite rebuilt to flagship level (PR #353), hardened
over a 13-round automated review loop plus a 41-scenario real-pointer browser harness.

### Added
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

### Changed
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

### Changed
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

### Changed
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

### Added
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

### Fixed
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

### Added
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

### Changed
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

### Fixed
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

### Fixed
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

### Fixed
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

### Fixed
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

### Fixed
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

### Added
- **`DialogContent.PlayExitAnimation` / `DrawerContent.PlayExitAnimation` /
  `AlertDialogContent.PlayExitAnimation`** (default `false`) — when `true`, the
  panel stays mounted for a zoom-out / slide-out (with the backdrop fading in
  parallel) before unmounting, instead of vanishing instantly. Set
  automatically by `OverlayService`-driven overlays; declarative consumers can
  opt in for the same dismissal animation. Backed by a new `animate-zoom-out`
  keyframe (the close counterpart to `animate-zoom-in`, reduced-motion aware).

## [4.1.0-preview.11] - 2026-07-06

Device-testing feedback wave.

### Fixed
- **Service-opened Sheet backdrop caught no clicks (B10)**: every overlay
  backdrop lives inside an intentionally `pointer-events-none` host (so idle
  overlays never block the page) and must re-enable its own pointer events —
  Dialog/Drawer/AlertDialog hardcode `pointer-events-auto` on their backdrops,
  but SheetContent drove its backdrop through `BackdropClass`, which omitted
  the class. The page behind a service-opened Sheet stayed fully interactive
  and click-outside-to-close was dead. Fixed in both the live and exiting
  branches; browser-verified (backdrop is the hit-target and closes the
  sheet); consumers can drop their CSS workaround.

### Added
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

### Added
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

### Fixed
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

### Added
- **`dotnet new lumeo-app`** — a full Blazor WASM starter that boots styled
  with zero manual steps: `AddLumeo()` wired, prebuilt CSS + OKLCH default
  theme + `theme.js`/`components.js` linked, collapsible Sidebar shell with a
  dark-mode toggle, three example pages (Dashboard with KPI cards and a
  DataGrid, a validated Form, Settings with Tabs), icons via the built-in
  LumeoIcons, and a README covering the three ownership paths (NuGet /
  vendor via CLI / eject). Lumeo package versions are stamped from the
  lockstep version at pack time.

### Fixed
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

### Fixed
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

### Added
- **Header-level pin control**: `Pinnable` columns now show a keyboard-
  accessible pin button in the column header (reveals on hover/focus, lit
  while pinned) with a Pin left / Pin right / Unpin menu — no more detour
  through the Columns popover.

## [4.1.0-preview.7] - 2026-07-04

### Fixed
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

### Fixed
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

### Added
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

### BREAKING
- **Blazicons fully decoupled.** `Icon.Svg`, `MegaMenuLink.Icon`, `MegaMenuItem.Icon`,
  `PopConfirm.Icon` and `TreeViewItem.Icon` now take the new `Lumeo.IconSource` instead of
  `Blazicons.SvgIcon`; no Lumeo package references Blazicons anymore. Migration is
  mechanical: `Lucide.X` stays `Lucide.X` with `@using Lumeo.Icons` + the
  `Lumeo.Icons.Lucide` package (names match 1:1), or use the ~220 built-in `LumeoIcons.X`
  that now ship inside the core for free. Blazicons remains usable in RenderFragment
  slots — it just left Lumeo's dependency graph and public API. The NuGet-free standalone
  eject is now truly dependency-free (it previously force-installed Blazicons.Lucide).

### Added
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

### Fixed
- **Avatar — Square/Themed avatars with initials rendered as circles**: `AvatarFallback`
  painted its `bg-muted` surface with its own hardcoded full-circle radius; the avatar's
  shape clip is larger than that circle, so clipping never took effect and any
  non-circle avatar showing fallback initials stayed round (reported against the new
  `Themed` shape in a sharp theme; had silently affected `Shape=Square` since its
  introduction). The fallback now carries no own border-radius and inherits the
  wrapper's clip — verified across Circle/Square/Themed at default and sharp radii.

## [4.1.0-preview.3] - 2026-07-02

### Fixed
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

### Added
- **`AvatarShape.Themed`** — third avatar shape following the theme radius (identical to
  Circle at stock radii, squares off in sharp themes). `Circle` and `Square` remain
  literal contracts: a consumer who asked for a circle keeps a circle in every theme.

## [4.1.0-preview.2] - 2026-07-02

### Added
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

### Fixed
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

### Added
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

### Fixed
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

### Fixed
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

### Fixed
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

### Fixed
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

### Added
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

### Changed
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

### Added
- **Drawer (#218)**: vaul-style snap points — `SnapPoints` (ascending fractions, e.g. `[0.4, 0.75, 1]`) + two-way `ActiveSnapPoint`/`ActiveSnapPointChanged`. A Top/Bottom drawer rests at fractional heights, drags between them, and dismisses below the lowest snap; programmatically setting `ActiveSnapPoint` moves it. A `PreventClose` drawer still snaps but never dismisses.
- **Drawer (#218)**: velocity/flick dismiss — a fast flick in the dismiss direction closes even below the distance threshold, tunable via `LumeoGestureOptions.SwipeDismissVelocity` (default `0.4` px/ms; `0` = distance-only).

### Improved
- **Drawer (#218)**: the backdrop now uses the `--color-overlay-backdrop` theme token instead of a hardcoded `bg-black/80`, matching Sheet/Dialog (light + dark).
- **RichTextEditor (#320)**: the floating bubble toolbar is keyboard-operable — `Alt+F10` moves focus into it (ARIA Authoring-Practices pattern), arrow/Home/End rove between buttons, `Escape`/`Tab` return focus to the editor (staying inside any modal focus trap), and it hides once focus leaves.
- **RichTextEditor (#320)**: the slash/mention suggestion listbox wires `aria-activedescendant` (+ `aria-controls`/`aria-expanded`) on the editor so screen readers announce the highlighted option as you arrow through.

## [3.18.0] - 2026-06-18

Bundled audit-backlog batch closing the remaining cleanly-doable component gaps in a single release. All additive and opt-in — existing usage is unchanged.

### Added
- **Pagination (#210)**: opt-in data-driven mode — set `Page` + `TotalPages` (or `TotalItems` + `PageSize`) and the component renders the full page list itself (prev/next, first/last boundaries, sibling window, `…` ellipsis gaps) and raises `PageChanged`. `SiblingCount`/`BoundaryCount` tune the window. The original `ChildContent` composition still works.
- **Button (#269)**: `Href` — when set, the button renders as an `<a>` link-button (shared loading/icon/content), with `aria-disabled` + `pointer-events-none` when disabled or loading.
- **Heading (#295)**: `As` — render the heading as any element (e.g. a `div` styled as a heading) while keeping the visual `Level`/`Size`, for correct document outline without forcing an `h1`–`h6` tag.
- **Code (#196)**: `Source` + `Language` (emitted as `data-language`) and a pluggable `Highlighter` hook (`Func<string, string?, MarkupString>`) so a syntax highlighter can be supplied; without one, `Source` is HTML-escaped.
- **Filter (#319)**: `FilterBar` gains a data-driven `Filters` model (`FilterDescriptor` = Field / Operator / Value) that auto-renders a dismissable `FilterPill` per descriptor and raises `OnRemoveFilter`; `FilterPill` gains an `Operator` (renders `Field op Value`, or `Field: Value` without one). The `Pills` slot still composes alongside.
- **Sparkline (#275)**: opt-in `ShowTooltips` — a marker dot at every point with a native SVG `<title>` (hover value), no JS. Off by default.
- **Statistic (#273)**: `ValueContent` slot overriding the formatted value — drop in a `NumberTicker` (Lumeo.Motion) for an animated count-up instead of duplicating animation in core.
- **Gauge (#277)**: `LabelContent` slot overriding the center/label text — same composable pattern for an animated value.

### Improved
- **SparkCard (#276)**: the inline chart now delegates to the full `Sparkline`, so it gains `Type` (Line/Area/Bars), `ShowArea`, `ShowLast`, `ShowTooltips` and `SparkColor` instead of a hardcoded less-capable polyline. A single data point still renders no chart (two-point minimum preserved).

## [3.17.0] - 2026-06-18

Bundled feature batch (component capability gaps), all additive and opt-in.

### Added
- **Barcode (#291)**: `OnError` callback (fires the encoding error message, or `null` on a successful encode) as a validation hook; the quiet zone now scales with `BarWidth` (10× the narrow module) instead of a fixed 10px.
- **Highlighter (#293)**: opt-in `RegexMode` — `Highlight`/`HighlightTerms` are treated as regular-expression patterns instead of literal text; invalid patterns fall back to plain rendering.
- **Grid (#250)**: opt-in `Responsive` — collapses to 1 column (mobile) / 2 (sm) and expands to `Columns` at `lg`, using purge-safe static utility strings for 1–6 columns. Off by default.

## [3.16.0] - 2026-06-18

a11y / i18n polish and small improvements following 3.15.0.

### Added
- **ReasoningDisplay (#305)**: opt-in `Markdown` rendering (+ a `MarkdownRenderer` hook), mirroring `StreamingText` — reasoning traces render as markdown via the built-in XSS-safe renderer (or a supplied one). Plain-text default unchanged.
- **ButtonGroup (#270)**: `AriaLabel` parameter; the group now exposes `role="group"` (roving tabindex stays a `Toolbar` concern, by design).

### Improved
- **Stepper (#245)**: Next/Back/Finish nav labels are localized (fall back to `L["Stepper.*"]`, shipped for every locale); explicit `*Label` params still override.
- **Result (#284)**: `role` is `alert` (assertive) for Error/Forbidden/ServerError and `status` otherwise, so assistive tech interrupts on failures.
- **BackToTop (#247)**: the scroll handler is throttled to one check per animation frame and only crosses the JS↔.NET interop boundary when visibility actually flips.

### Fixed
- **Collapsible (#238)**: in controlled mode (`@bind-Open`), `Toggle` no longer mutates its own `Open` parameter — it fires `OpenChanged` and renders from the parent's value, fixing a desync when the parent rejected/ignored the change.

## [3.15.0] - 2026-06-18

Follow-up to the 3.14.0 audit pass: the two P0 cascade/layout fixes (browser-verified by new Playwright e2e coverage) plus a small a11y/i18n polish batch.

### Fixed
- **Overlays (#172)**: `positionFixed` — the shared positioner for Popover, Select, DropdownMenu, ContextMenu, Menubar and Tooltip — now positions with explicit `top`/`left` only and never sets a CSS `transform`. A transformed overlay established a containing block for its `position:fixed` descendants, so a nested overlay (`DropdownMenuSubContent`, popover-in-popover, ContextMenu/Menubar submenu) resolved against the transformed parent instead of the viewport and opened off-screen. All viewport flip/clamp guards are preserved; visually identical for the existing cases.
- **Icon (#173)**: size utilities (`h-/w-/size-`) now win under Tailwind v4. Blazicons injects an unlayered `svg[blazicon]{width:1em}` rule that beat `@layer utilities`, silently collapsing every icon to the font size; an unlayered, higher-specificity `revert-layer` reset defers sizing back to the utilities layer (a consumer's own `Class` override still wins). Effective on the unlayered `<link>` path; layered-import consumers add the reset themselves (documented inline).
- **RingProgress (#278)**: `aria-valuenow` is clamped/rounded into `[aria-valuemin, aria-valuemax]`.

### Improved
- **Hero (#297) / CTASection (#298) / FeatureGrid (#299)**: the `<section>` landmark now carries an accessible name via `aria-labelledby` → its heading, so assistive tech exposes it as a named region.

### Added
- **Spinner (#282) / Skeleton (#281)**: `AriaLabel` parameter (defaults to "Loading") so the screen-reader name is localizable without a visible label.

## [3.14.0] - 2026-06-17

Library-wide audit-remediation release. Building on the 3.13.0 audit, this release closes accessibility (keyboard/ARIA), lifecycle, interop-safety, culture and motion gaps across ~60 components, adds several audit-flagged feature gaps, and honors `prefers-reduced-motion` across the Motion package. Full per-component detail is tracked in audit issues #171–#335.

### Added
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

### Improved
- **prefers-reduced-motion** is now honored across `Lumeo.Motion` (AnimatedBeam, BlurFade, BorderBeam, Marquee, NumberTicker, ShimmerButton, Sparkles, TextReveal, Confetti, Dock, TouchRipple) and overlay exit animations; NumberTicker now formats with the current culture's group/decimal separators.
- **Keyboard / ARIA**: roving-tabindex, arrow/Home/End navigation, typeahead and focus management added or hardened across Select, Combobox, TreeSelect, Cascader, Mention, Command, Menubar, MegaMenu, DropdownMenu, ToggleGroup, Segmented, Calendar, Accordion, Steps, Toolbar, SpeedDial and Sortable — disabled items are skipped consistently and keyboard activation no longer double-fires.
- **Overlays**: Sheet/Dialog/Popover/Tooltip focus management, theme-token backdrops, exit animations and Escape handling hardened (a pinned Tooltip now dismisses on Escape; the Sheet no longer flickers on close).
- **Interop safety**: JS-disconnect/disposal guards added across component teardown; the `prefers-reduced-motion` query and theme listeners are pruned/guarded on async failure.

### Fixed
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

### Deferred
- **#172** (nested-overlay positioning under a transformed parent) and **#173** (Icon sizing under Tailwind v4) require real-browser verification and ship in a dedicated follow-up PR.
- **#320** (RichTextEditor TipTap extensions) and **#196** (Code syntax highlighting) pend an npm/bundle build step.

## [3.13.2] - 2026-06-12

### Fixed
- **DataGrid (ServerMode)**: group expand/collapse now regroups the server-delivered page — the toggles previously dispatched to the client pipeline, which re-applied client filtering/sorting/paging over the server page and could corrupt the row set on every collapse.
- **DataGrid (layouts)**: filter values restored from JSON (persisted layouts, the `SavedLayout` parameter, named layouts) are normalized from `JsonElement` to CLR primitives — number/date filters compared lexicographically before (`">5"` dropped `10`), and ServerMode consumers now receive comparable descriptor values in `OnServerRequest`.
- **DataGrid (layouts)**: removing a group chip after a layout restore unhides its auto-hidden column — the un-group snapshot is now seeded for restored chips.

## [3.13.1] - 2026-06-12

### Fixed
- **Tabs (Card variant)**: the active tab now fuses with the list's edge border — axis-aware seam (bottom for horizontal, right for vertical) with squared seam corners; previously the card floated above the border line with the base rounding peeking through.
- **Tabs (Card variant)**: switching tabs no longer flickers — every card tab carries identical box metrics (inactive tabs render a transparent border), so activation swaps colors only instead of animating a 2px layout reflow.

## [3.13.0] - 2026-06-11

Component-audit hardening release: a full-library audit benchmarked against shadcn/ui, Blueprint, MudBlazor and Ant Design, fixing keyboard/ARIA, lifecycle and culture defects across ~25 components, plus a consumer-reported DataGrid grouping regression.

> Changelog entries between `1.0.0-beta.5` and this release were tracked via git history and GitHub Releases rather than this file.

### Fixed
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

### Added
- `IComponentInteropService.RegisterPreventDefaultKeys` — key-selective, IME-safe `preventDefault` applied synchronously in the native event dispatch (replaces the render-time `@onkeydown:preventDefault` flag pattern).
- Localization keys for ConfirmButton, PickList, FileManager, AudioPlayer, ThemeSwitcher, Stepper and Breadcrumb strings across all 14 locales.
- Wired previously-inert parameters: Tooltip `Offset`, HoverCard `Side` (Left/Right), PopConfirm `Placement`, SpeedDial `Icon`/`Variant`, Highlighter `Tag`.
- DevX: a `SessionStart` hook installs the .NET SDK + npm dependencies in Claude Code remote containers.

## [1.0.0-beta.5] - 2026-03-19

### Improved
- Updated NuGet package description (90+ → 103 components, added test count and feature list)
- Updated README with accurate component count, themes, and install command

## [1.0.0-beta.4] - 2026-03-19

### Added
- Checkbox: Label, Description parameters with auto-Id for form association
- RadioGroupItem: Description text support
- Steps: Error state per step with red X icon, custom Icon slot
- Popover: Arrow support (ShowArrow parameter matching Tooltip pattern)
- 48 new unit tests covering all upgraded component features (1,124 total)
- Form Validation guide with DataAnnotations, custom validation, and complete examples
- Contributing guide with setup, component creation, testing, and code style docs
- "When to Use" and "Related Components" sections on 62 more component pages (82 total)
- API reference tables now on all 136 component documentation pages

### Improved
- Home page stats updated (75→103 components, 7→8 themes)
- Chart patterns integrated into Patterns page with filter category
- All hardcoded colors replaced with CSS variables (Avatar, Statistic, Result, KanbanCard)

### Fixed
- MentionPage Razor escape for @user syntax
- Statistic and Result test assertions updated for CSS variable colors

## [1.0.0-beta.3] - 2026-03-19

### Added
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

### Improved
- Animation keyframes and utility classes now ship in lumeo.css for NuGet package consumers
- Production-quality spring easing curves on all animations
- Rating colors now use themeable `--color-rating` CSS variable instead of hardcoded yellow

### Fixed
- Broken animations for NuGet package consumers (keyframes were only in docs site)
- Missing `animate-toast-in` — Toast slide-in animation was never defined
- Added aria-labels to PasswordInput toggle, TagInput close buttons, DatePicker clear button, Carousel navigation
- Rating keyboard navigation with Arrow keys and improved star labels

## [1.0.0-beta.2] - 2026-03-12

### Added
- 14 new components: Cascader, ColorPicker, DateRangePicker, DateTimePicker, Filter, ImageCompare, InplaceEditor, InputMask, Kanban, MegaMenu, Mention, NumberInput, PasswordInput, SortableList
- Keyboard shortcuts: R to shuffle themes, Ctrl+D for dark mode, Ctrl+/ for shortcuts help
- Redesigned WASM loader with animated splash screen and ripple animation
- Floating navbar and floating sidebar design for docs site
- NuGet package icon (Lumeo logo)

### Improved
- All UI corners respect CSS radius variable for zero-radius presets like Lyra
- Customizer sidebar moved to header button with Ctrl+B toggle shortcut
- CommandEmpty now always renders regardless of Command context

### Fixed
- Customizer radius bug where radius values did not apply correctly
- Mobile docs improvements and API table horizontal scrolling
- Floating nav sticky positioning
- Splash screen CSS compatibility with Tailwind CDN
- Em dash encoding issues in page titles

## [1.0.0-beta.1] - 2026-03-12

### Added
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

### Fixed
- Chart color resolution for modern CSS color formats (oklch, hsl, color())
- WordCloud extension race condition causing render failures
- Bar chart rendering broken by NaN borderRadius from CSS variable parsing
- Chart label text stroke artifacts on Sankey, Graph, Area, and Funnel charts
