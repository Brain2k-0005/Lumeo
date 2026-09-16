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
