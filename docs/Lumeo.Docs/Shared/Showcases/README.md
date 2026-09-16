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
