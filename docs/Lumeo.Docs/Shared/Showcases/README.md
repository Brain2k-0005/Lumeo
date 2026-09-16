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
   showcase for an overlay component (Dropdown/Popover/Dialog/Sheet-style —
   MegaMenu, Menubar, NavigationMenu here) shows its trigger and opens on
   click exactly like it would anywhere else in the app; the open panel may
   visually extend outside the 16:9 box the way a real dropdown does.
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
| MegaMenu | 2 top items; click "Products" opens a 1-column panel |
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
