# Changelog

All notable changes to Lumeo will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [4.0.0] - 2026-06-26

Two things in one release: a Radix/Base-UI/shadcn **parity audit** (accessibility, RTL, theming, the FormGenerator, and the MCP/CLI — additive and opt-in; the OKLCH and logical-utility changes are visually/behaviourally identical in LTR) **and** a library-wide **correctness hardening** pass — an adversarial "battle-test" of all 164 components that fixed ~355 confirmed bugs, each with a bUnit regression test (suite 4825 green). There are **no API-signature breaks**; the major bump signals the scope and the handful of observable **behaviour** changes listed under **Changed** below (and in `MIGRATION.md`).

### Added
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
