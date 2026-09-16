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

Corrected rule 4 for this wave: only overlay content positioned through
`Interop.PositionFixed` for a full-viewport modal backdrop (AlertDialog, Dialog,
Sheet, Drawer) or an anchored floating panel (Popover, Tooltip, HoverCard,
ContextMenu, DropdownMenu, PopConfirm) is allowed to actually use `position:
fixed` live in the catalog grid — for these 10 components the real fixed-
positioned content would either take over the whole page (the modal group) or
depend on floating-ui JS running correctly against a catalog card mid-scroll
(the anchored group), so each shows its trigger plus a **static replica of the
open panel** — the same background/border/shadow/text tokens as the real
`*Content` component, laid out in normal document flow instead of fixed —
rendered inline immediately rather than behind a click/hover. `Window` turned
out to need the same treatment: its root panel is unconditionally
`position: fixed` (drag coordinates are viewport-relative), so it also gets a
static chrome replica instead of the live component.

| Component | Showcase state |
| --- | --- |
| AlertDialog | Trigger + static replica of the "are you sure?" panel (title, description, Cancel/Continue) |
| Command | Real component, compact height, 3 suggestions — search input filters live |
| ContextMenu | "Right click here" trigger area + static replica menu (Back/Forward/Reload/Print) below it |
| Dialog | Trigger + static replica of the "Share this document" panel with a link input |
| Drawer | Trigger + static replica bottom sheet (drag handle, Move Goal, Cancel/Submit) |
| DropdownMenu | Trigger + static replica menu (My Account label, Profile/Settings/Log out) |
| HoverCard | Trigger + static replica card (@@blazor bio) |
| PopConfirm | Trigger + static replica confirm panel (Delete this item?, Cancel/Confirm) |
| Popover | Trigger + static replica "Dimensions" panel with two field rows |
| Sheet | Trigger + static replica right-edge panel (Edit Profile, Name field, Save Changes) |
| Tooltip | Trigger + static replica bubble ("Add to library") beside it |
| Alert | Default info alert, title + description |
| EmptyState | Icon + title + description + "Create Project" action |
| Progress | Two labeled bars, 60% and 85% (success variant) |
| Result | Success status, compact size, "View Order" action |
| RingProgress | Two rings, 72% and 45% (default/success colors) |
| Skeleton | Avatar + two text-line placeholders |
| Spinner | Sm/Default/Lg side by side |
| Toast | Two stacked toasts (success "Changes saved", default "New message") composed from the real `Toast`/`ToastTitle`/`ToastDescription`/`ToastClose` primitives (no service/provider) |

### Exceptions

- **Overlay** — `OverlayService`/`OverlayProvider` is a DI-registered service with
  no visual primitive of its own; calling it opens a real Dialog/Sheet at the
  app's root-mounted provider, which would take over the whole page from inside
  a catalog card. Shows the same "Open a Dialog" trigger as the component's own
  docs page plus a static replica of the confirm panel `ShowConfirmAsync`
  renders (Dialog-shaped, per the component's own architecture).
- **Tour** — steps target real DOM elements elsewhere on the page and the
  spotlight ring is measured against their live bounding rects; there is
  nothing to miniaturise inside a self-contained card. Shows a faithful static
  rendition of the component's own visible control — the step tooltip card
  (title, description, step counter, Back/Next) exactly as `Tour.razor` renders
  it — with no spotlight/targeting wiring.
- **Window** — its root panel renders unconditionally `position: fixed` with
  viewport-relative drag coordinates (not relative to any wrapper), so the live
  component floats away from the card exactly like a modal would. Shows a
  static replica of the window chrome (title bar, body, resize handle) in
  normal flow instead.
