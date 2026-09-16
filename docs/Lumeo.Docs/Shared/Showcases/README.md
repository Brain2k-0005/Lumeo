# Catalog showcases

A showcase is a live, interactive mini-demo of a component shown inside its
`/components` catalog card, in place of a static OG-image thumbnail. Every
`/components` card whose registry name has a matching `<Name>Showcase.razor`
here renders that showcase instead of the thumbnail (`CatalogCard` resolves it
by convention via `ShowcaseResolver` — there is no central list to edit).

## Convention

- File: `docs/Lumeo.Docs/Shared/Showcases/<Name>Showcase.razor`, where `<Name>`
  is the exact registry component name (`Accordion` -> `AccordionShowcase`,
  `NavigationMenu` -> `NavigationMenuShowcase`).
- `@namespace Lumeo.Docs.Shared.Showcases`.
- No `[Parameter]`s are required — `CatalogCard` renders it via
  `<DynamicComponent Type="...">` with no parameters, so every showcase must
  be fully self-contained.

## Rules (binding for every wave)

1. **Self-contained, deterministic, in-memory data.** No registry/HTTP calls,
   no `RegistryService`, no random values, no `DateTime.Now` — the same
   markup every render. Local `@code` state (e.g. which tab/step is active)
   is fine and expected — that's what makes it interactive.
2. **Fits the 16:9 preview box without scrolling.** The card's preview area is
   `aspect-[16/9]` at desktop card width (roughly 420x236px). Use
   `w-full max-w-[22rem]`, compact component sizes (`Sm`/`Xs` where
   available), and 2-4 items/rows/columns max — never the full "kitchen sink"
   demo from the component's own docs page.
3. **Shows a recognisable, useful state immediately** — not an empty shell.
   An Accordion with one item already open, a Breadcrumb with three crumbs, a
   Tabs with its first tab active, a Stepper on step 1 of 3.
4. **Stays interactive.** No `pointer-events-none`, no disabled wrapper. A
   showcase for an overlay component (Dropdown/Popover/Dialog/Sheet-style)
   shows its trigger and opens on click exactly like it would anywhere else
   in the app — but whether the open content may visually extend past the
   16:9 box depends on how the REAL component positions it, and the preview
   box has `overflow-hidden`:
   - **`position: fixed` content escapes** any ancestor's `overflow-hidden`
     regardless of nesting, so it's fine for it to overlay past the box the
     way a real dropdown does — no special handling needed. Tell it apart by
     checking the component's own source for `Interop.PositionFixed` (Lumeo's
     floating-position interop, which sets `position: fixed` under the hood)
     or a literal `position: fixed` / `fixed` Tailwind class on its content
     element. Menubar and NavigationMenu are both like this.
   - **`position: absolute` content (no JS repositioning) gets clipped** by
     the box exactly like any other content that doesn't fit — it is NOT
     exempt just because it's an overlay. For that case, either (a) override
     it to lay out in NORMAL DOCUMENT FLOW instead of floating (`static!`
     — Tailwind v4's trailing-`!` important-modifier syntax, needed because
     the component's own position class otherwise wins; see
     `MegaMenuShowcase.razor`, which also opens by default via a post-mount
     click simulation so the showcase is useful without a hover/click
     mid-interaction step), sized to actually fit the box, or (b) keep the
     showcase closed by default so nothing needs to fit. MegaMenu is like
     this (its panel is `position: absolute; top-full` with no JS
     repositioning).
   Check before assuming: `grep -n "position:\|Interop.PositionFixed"` in the
   component's own `.razor` source.
5. **No headings, no prose, no "demo"/"preview" labels.** The component IS
   the content — no surrounding explainer text.
6. **Never scrolls the page or traps the wheel.** No inner `overflow-auto`
   panes; use small, fixed, non-overflowing data instead of a scrollable list.
7. **No external network calls.** Local placeholder text/icons only.
8. **Theme variables only** (`bg-card`, `text-muted-foreground`,
   `border-border`, ...) — no raw hex, no `dark:` prefixes, per the repo's
   normal component rules.
9. **Every showcase must render standalone with no JS** (bUnit, no
   `IJSRuntime` calls resolving) — the `AllShowcasesRenderTests` gate mounts
   every discovered `*Showcase` type on its own and asserts no exception.
   Lean on components whose JS interop degrades to a no-op rather than
   throwing (true for all current Lumeo interop) and avoid anything that
   requires a real DOM measurement to render its initial state.

## Wave 0 — Navigation category

All 19 `hasDocsPage: true` Navigation-category components got a showcase.

| Component | Showcase state |
| --- | --- |
| Accordion | Single-mode, 3 items, one already open |
| AppBar | Static bar with brand + two actions (`Sticky="false"`) |
| BottomNav | 4 tabs, one active — tap another to switch |
| Breadcrumb | 3 crumbs (Home / Components / current page) |
| Carousel | 3 slides, Previous/Next controls, click-through |
| Collapsible | One collapsible panel, trigger toggles it |
| MegaMenu | 2 top items; "Products" opens by default — a 2-column panel laid out in normal flow inside the box (its real panel is `position: absolute`, not `fixed`, so it's clipped otherwise; see rule 4) |
| Menubar | 1 menu ("File") with a few items; click opens it |
| NavigationMenu | 1 trigger; click opens a small link grid |
| Pagination | 5 pages, current page highlighted, click to change |
| Sidebar | Icon-collapsible rail, 3 menu items, trigger toggles width |
| SpeedDial | Closed FAB; click to fan out 3 actions |
| Splitter | 2 panes, drag the divider to resize |
| Stepper | 3 steps, step 1 active, Next advances |
| Tabs | 2 tabs, first active, click switches content |
| Toolbar | A row of icon buttons with one separator |

### Exceptions — page-scroll components

These three fundamentally observe real page/container scroll to do anything
(`Affix`/`BackToTop` watch window scroll position; `Scrollspy` tracks which
scrolled section is in view). Miniaturising "scroll the page" inside a static
catalog card isn't meaningful, so each showcase instead renders a faithful
**static rendition of the component's own visible control** — the exact pill/
button/link markup it produces once affixed/visible/active — with no scroll
wiring:

- **Affix** — the pinned badge exactly as it renders once stuck.
- **BackToTop** — the round back-to-top button exactly as it renders once
  visible past the scroll threshold.
- **Scrollspy** — the nav link list with the middle link shown in its
  `data-active="true"` state, exactly as it looks mid-scroll.

## Wave 1 — Overlay and Feedback categories

All 22 `hasDocsPage: true` Overlay (14) and Feedback (8) components got a showcase.

| Component | Showcase state |
| --- | --- |
| AlertDialog | Real component — trigger opens the real full-viewport AlertDialog; a small marked-as-preview static mock sits next to the trigger so the card isn't empty before it's clicked |
| Command | Real component, compact height, 3 suggestions — search input filters live |
| ContextMenu | Real component — right-click the trigger area opens the real menu (escapes the box per rule 4) |
| Dialog | Real component — trigger opens the real full-viewport Dialog; marked-as-preview static mock beside the trigger |
| Drawer | Real component — trigger opens the real bottom-sheet Drawer; marked-as-preview static mock beside the trigger |
| DropdownMenu | Real component — trigger opens the real menu (escapes the box per rule 4) |
| HoverCard | Real component — hover/focus (or click, which toggles pin) opens the real card (escapes the box per rule 4) |
| Overlay | Real button calling the real `IOverlayService.ShowAlertDialogAsync` — opens a real AlertDialog over the page, same as the component's own docs page |
| PopConfirm | Real component — trigger opens the real confirm popover (escapes the box per rule 4) |
| Popover | Real component — trigger opens the real popover (escapes the box per rule 4) |
| Sheet | Real component — trigger opens the real edge-panel Sheet; marked-as-preview static mock beside the trigger |
| Tooltip | Real component — hover/focus the trigger opens the real tooltip (escapes the box per rule 4) |
| Tour | Real component targeting a real button inside the card — "Start Tour" opens the real full-viewport spotlight + tooltip; marked-as-preview static mock of the tooltip card beside the trigger |
| Window | Static replica (exception, see below) |
| Alert | Default info alert, title + description |
| EmptyState | Icon + title + description + "Create Project" action |
| Progress | Two labeled bars, 60% and 85% (success variant) |
| Result | Success status, compact size, "View Order" action |
| RingProgress | Two rings, 72% and 45% (default/success colors) |
| Skeleton | Avatar + two text-line placeholders |
| Spinner | Sm/Default/Lg side by side |
| Toast | Real button raising a real toast through `ToastService` + two inline toasts composed from the real `Toast`/`ToastTitle`/`ToastDescription`/`ToastClose` primitives directly (no service, for the always-visible pair) |

Full-viewport overlays (AlertDialog, Dialog, Sheet, Drawer, Tour) pair the real
trigger with a small static preview of the open panel next to it, explicitly
commented `PREVIEW ONLY` in the source and carrying no ARIA roles — it's a
picture, not a second dialog — so the card isn't just a lonely button before
it's clicked, without faking the real component's full-viewport takeover
(which is expected, correct behaviour, same as anywhere else in the app).

### Exceptions

- **Window** — its root panel renders unconditionally `position: fixed` with
  viewport-relative drag coordinates (not relative to any wrapper), so the live
  component floats away from the card exactly like a modal would. Shows a
  static replica of the window chrome (title bar, body, resize handle) in
  normal flow instead.
## Wave "rest" — Motion, Typography, Dashboard, AI, Marketing, Drag & Drop

All 32 `hasDocsPage: true` components across these six categories got a showcase.
No exceptions were needed — every component miniaturises cleanly.

| Component | Showcase state |
| --- | --- |
| AnimatedBeam | 2 nodes (CPU/Cloud icons), beam animates between them |
| BlurFade | 3 tiles, staggered `ForceHidden` reveal on mount |
| BorderBeam | A small pricing card with the beam looping around it |
| Confetti | "Celebrate!" button — click fires a burst |
| Dock | 4 icons, cursor-proximity magnify on hover |
| Marquee | 4 brand chips scrolling left, pauses on hover |
| NumberTicker | Two KPI-style counters, count up on mount |
| ShimmerButton | 2 buttons with the looping shimmer sweep |
| Sparkles | Looping sparkle field around two short labels |
| TextReveal | One heading line, word-by-word reveal on scroll-into-view |
| Code | Inline + block code snippet |
| Heading | A compact section header (eyebrow + subheading) |
| Highlighter | One sentence with 3 highlighted terms |
| Link | Default / underline / external variants |
| Text | Three size/color/weight combinations |
| Bento | One tile (KPI-style), the natural minimum unit of the grid |
| Delta | 3 trend chips (up/down/inverted-good) |
| KpiCard | One KPI tile with icon, value, and delta |
| PickList | 4 source items / 1 target, move buttons swap panels |
| SparkCard | One KPI tile with an inline area sparkline |
| Kanban | 2 columns × 2 cards, draggable |
| Sortable | 2 items, drag-to-reorder with a handle |
| Transfer | 2 source / 1 target item, arrow buttons move between panels |
| AgentMessageList | A 2-message user/assistant exchange |
| PromptInput | Empty textarea with placeholder + send button |
| ReasoningDisplay | One reasoning trace, expanded by default |
| StreamingText | One line with the blinking streaming caret |
| ToolCallCard | One tool call, expanded, with input/output |
| CTASection | Compact heading (via `TitleSlot`) + one button |
| FeatureGrid | 2 `FeatureItem`s, no grid title/subtitle |
| FeatureItem | One icon + title + description |
| Hero | Compact centered headline (via `TitleSlot`) + one button |

CTASection/FeatureGrid/Hero bake fixed vertical padding (and, for Hero, fixed
`h1` sizing) into an *inner* wrapper div that isn't exposed via `Class` — only
the root element's classes are overridable. Each uses `TitleSlot`/`Actions`
to swap in compact markup instead of the oversized default heading; the
padding itself still clips at the bottom of the 236px preview box on some
viewports, the same accepted tradeoff as any oversized showcase (see rule 2
above and `CatalogCard`'s own overflow-hidden note) — verified via screenshot
to hold up in practice at 1440px in both themes.
## Wave 1 — Data Display category

25 of the 32 `hasDocsPage: true` Data Display-category components got a live showcase.

| Component | Showcase state |
| --- | --- |
| Avatar | 3 overlapping fallback avatars in a stack, plus a "+3 more" count |
| Badge | 6 badges across the variant set, one pill with a count |
| Barcode | One Code 128B barcode (inline SVG, no JS) |
| Calendar | A single month, denser cell size via `--lumeo-calendar-*` vars, one date pre-selected — click any day to change it |
| Card | Header + content + footer, compact text |
| Chart | `BarChart`, 4 categories, `Height="160px"` (ECharts via CDN — paints in the real browser, is a no-op under bUnit's loose JS mode, never throws) |
| Chip | 3 closable tags — click × to remove one |
| DataGrid | 3 rows, 2 columns, no pagination footer |
| DataTable | 3 rows with a status `Badge` cell |
| Descriptions | 2-column key/value grid, 4 items |
| Filter | `FilterBar` with 2 dismissible pills — click × to remove one |
| Filters | The query-builder chip row, `Size="Sm"`, one preset rule already applied |
| Gauge | 2 small radial gauges (CPU/RAM) |
| Image | A local inline-SVG data-URI "photo" (no external image host — deterministic, no network) |
| ImageCompare | Local inline-SVG before/after data URIs, drag the divider |
| List | 3 items with leading icons, one trailing badge |
| PivotGrid | 2 regions × 2 years, one measure, `Compact="true"` |
| QRCode | One small QR code (inline SVG, no JS) |
| Sparkline | An inline trend line next to a value + delta |
| Statistic | 2 stats with trend arrows |
| Steps | 3 steps, step 2 active, `Clickable="true"` — click a step to jump to it |
| Table | 3-row invoice table |
| Timeline | 3 vertical events |
| TreeView | 2 folders, `ExpandAll="true"`, `Size="Sm"` |
| Watermark | "Confidential" tiled over two lines of body text |

### Exceptions — viewport/external-engine components

These 7 components fundamentally need a real network resource, a JS charting/mapping
engine, or a viewport taller than a catalog card to show anything meaningful. Each
showcase instead renders a faithful **static rendition** of the component's own visible
chrome (matching its real classes/markup where practical), with no live wiring:

- **FileManager** — the exact breadcrumb bar and list-row markup (icon, name, size) for a
  small fixed set of entries, without the folder tree pane or interactivity (a real
  explorer needs the tree pane to mean anything, which doesn't fit at card width).
- **FileViewer** — the header bar (file icon, name, kind label, download button — same
  classes as the real component) over a centered file-icon body, matching the unresolved/
  unsupported-preview state; its PDF/Code kinds delegate to PdfViewer/CodeEditor, both
  external-engine viewers.
- **Gantt** (legacy SVG engine) — a day-scale header and 3 proportional task bars; the
  real component needs real height/width to lay out day columns (420px in its own docs
  demos) and has no tree pane to shrink further.
- **GanttChart** (v3 engine) — same treatment; the real component virtualizes rows
  against the actual scroll viewport (`GanttViewportReconciler`), which a static card
  can't reproduce.
- **Map** — a theme-tinted dot-grid backdrop with a `MapInfoChip` overlay (the docs'
  own reusable info-chip component) and a pin marker, in place of MapLibre GL tiles
  fetched from a real network.
- **PdfViewer** — the toolbar chrome (page nav + zoom, same classes as the real
  component) over a page-shaped placeholder, in place of pdf.js rendering a real PDF to
  canvas.
- **Scheduler** — the exact agenda-row markup `SchedulerAgendaView` itself produces
  (color dot, title, time) for 3 fixed events; every real view needs 320px-640px of
  height in its own docs demos to show more than an empty grid.
## Wave 1 — Forms category

All 35 `hasDocsPage: true` Forms-category components got a showcase.

| Component | Showcase state |
| --- | --- |
| Button | Default / Secondary / Outline / Destructive, small size |
| Cascader | Closed trigger, path pre-selected ("United States / California") |
| Checkbox | 3 items — one checked, one unchecked, one disabled |
| ColorPicker | Closed trigger, swatch + hex pre-filled |
| Combobox | Single-select, "React" pre-selected as a removable chip |
| ConfirmButton | Delete + Archive triggers, real OverlayService dialog on click |
| DatePicker | Closed trigger, date pre-filled |
| DateTimePicker | Closed trigger, date + time pre-filled |
| FileUpload | Compact Button-variant trigger (the dropzone variant doesn't fit the box) |
| Form | Name/Email fields, one already showing a validation error |
| IconPicker | Closed trigger, "House" icon pre-selected, clearable |
| InplaceEditor | Starts in edit mode (input + Save/Cancel) — see exceptions below |
| Input | Email field with label, plus a search input with a leading icon |
| InputMask | Phone mask pre-filled, ZIP mask empty |
| Mention | Empty textarea with a "Type @ to mention..." placeholder and 3 people wired up as the mention list (its dropdown is absolutely positioned, so it stays closed by default rather than opening into a clip) |
| NumberInput | Quantity stepper + a `$` prefixed price field |
| OtpInput | 4-box code, pre-filled |
| OverlayForm | Name/Email body + Cancel/Save footer, fixed-height wrapper |
| PasswordInput | Pre-filled password with the strength meter shown |
| QueryBuilder | One rule ("Active equals true") over 2 fields |
| RadioGroup | 3 options, "Comfortable" selected |
| Rating | Half-star value pre-set |
| Segmented | 3 options, "Weekly" active — click another to switch |
| Select | Closed trigger, "Banana" pre-selected — click opens the real dropdown |
| Slider | Single thumb at 60%, with a live percentage readout |
| Switch | 2 settings rows, one on one off |
| TagInput | 2 tags pre-filled, removable |
| Textarea | Labeled bio field, pre-filled |
| TimePicker | Closed trigger, time pre-filled |
| Toggle | 3 icon toggles, one pressed |
| ToggleGroup | Single-select alignment group, "center" active |
| TreeSelect | Closed trigger, nested value pre-selected ("Phones") |
| UploadTrigger | Two Button-styled pick triggers (default + image filter) |

### Exceptions — heavy JS engine

These two mount a large third-party JS editor engine (CodeMirror 6 /
TipTap-ProseMirror) via dynamic JS interop — not something a catalog card
should bootstrap just to render a preview. Each showcase instead renders a
faithful **static rendition** of the editor's chrome, in theme tokens, with no
engine mounted:

- **CodeEditor** — a language pill, line-number gutter, and a few lines of
  syntax-colored JSON, exactly as the real editor's chrome looks.
- **RichTextEditor** — the real `Toolbar`/`Button` components (genuinely
  interactive chrome) above a static rendition of typical WYSIWYG output
  standing in for the ProseMirror document body.

### Note — InplaceEditor's idle state

`InplaceEditor`'s non-editing display state has no visual "editable" affordance
until `:hover` (an opacity-0 pencil icon), which reads as inert plain text in a
static preview. Like `MegaMenu`/`SpeedDial` in wave 0, a real click on its own
display element is simulated once after mount (`window.lumeo.clickElement`) so
the showcase lands on the actual editing UI (input + Save/Cancel) — the
recognisable, useful state rule 3 calls for.
## Wave 1 — Utility and Layout categories

All 10 Layout-category and 18 Utility-category `hasDocsPage: true` components
got a showcase (28 total).

### Layout (10)

| Component | Showcase state |
| --- | --- |
| AspectRatio | Two ratios side by side (16:9, 1:1), each labelled |
| Center | A dashed box with an icon + "Centered content" centered on both axes |
| Container | Three stacked bordered boxes at `xs`/`sm`/`md` max-widths |
| Flex | A nav-bar row: brand + `Spacer` + two buttons |
| Grid | 3-column grid, 6 numbered placeholder boxes |
| Resizable | Two panels with a draggable handle — drag to resize |
| ScrollArea | A short tag list in a custom-scrollbar box, scrolled by mouse wheel |
| Separator | A horizontal divider under a heading + a vertical-divider link row |
| Spacer | An avatar/name row pushed apart from an Edit button by two spacers |
| Stack | 3 items; Vertical/Horizontal buttons toggle the stack's direction live |

### Utility (18)

| Component | Showcase state |
| --- | --- |
| AudioPlayer | Compact player (cover, title/artist, play, scrub bar) — skip/rate/volume hidden to fit |
| ButtonGroup | 3 icon buttons (Bold/Italic/Underline) joined into one segmented bar |
| DensityScope | Two scoped rows (Compact, Spacious) with the same Save/Cancel buttons |
| DirectionProvider | Ltr vs Rtl rows — same markup, icon/button mirror sides |
| DropdownButton | "Actions" trigger; click opens a menu (position\:fixed, escapes the box like Menubar) |
| Field | An email `Field` + a horizontal checkbox `Field` |
| Icon | 5 icons across sizes/colors in one row |
| Kbd | `Ctrl`+`K`, `Esc`, `Enter` shortcut glyphs |
| Label | A `Label`+`Input` pair + a checkbox with its `Label` |
| SignaturePad | Disabled/read-only with a deterministic captured-signature fixture |
| SplitButton | "Save" primary half + chevron half; click opens the secondary-actions menu |
| SwipeActions | Two rows with trailing actions behind them, "Swipe left" hint shown |
| TouchRipple | Two buttons wrapped in `TouchRipple` — click either to see the ripple |

### Exceptions — real browser feature (5)

These need a real device/browser capability (touch drag, viewport insets, a
persisted per-browser decision) or would mutate global site-wide state if
wired live from inside a catalog card. Each renders a faithful **static
rendition** of its own real markup instead, with local `@code` state only:

- **ConsentBanner** — only mounts once per browser and is `position: fixed`
  to the viewport; wiring the live `ConsentService` would pop the real
  site-wide banner. Static rendition of its card content (icon/title/
  description + Customize/Reject/Accept), in normal flow.
- **PullToRefresh** — reacts to a real touch/pointer drag a static card can't
  simulate. Static rendition of the component's own "engaged" mid-pull state
  (the spinner tile pulled below the top edge) with no drag wiring.
- **SafeArea** — `env(safe-area-inset-*)` is `0px` on desktop and most
  Android (per the component's own docs page); only notched iPhones see a
  real inset. Static rendition of its canonical use, a bottom tab bar.
- **ThemeSwitcher** — calls the live `ThemeService` on every click, which
  would flip the whole docs site's color scheme/mode for anyone previewing
  the catalog, not a contained effect like every other showcase. Static
  rendition of the same swatch/mode markup with local `@code` state instead of
  `ThemeService`, using `ThemeService.AvailableSchemes`' own preview colors.
- **ThemeToggle** — same hazard as ThemeSwitcher: it calls
  `ThemeService.ToggleModeAsync()` on click, which would flip the whole docs
  site's light/dark mode from inside a catalog card. Not listed in the wave
  brief's exception set but carries the identical global-side-effect problem,
  so treated the same way: a static rendition of its exact button/icon markup
  with a local `bool` instead of `ThemeService`.
