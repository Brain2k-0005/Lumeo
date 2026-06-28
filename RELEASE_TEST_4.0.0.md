# Lumeo 4.0.0 — Manual Test Checklist

Docs dev server: **http://localhost:5287** (`dotnet run --project docs/Lumeo.Docs --launch-profile http`).

This release has **no API-signature breaks**. The major bump signals scope: the parity
audit + the battle-test hardening of all 164 components (~355 fixed bugs). So testing time
should target the **observable behaviour changes** (Section A) and the **new opt-in features**
(Section B) first — those are where a regression could actually surprise a consumer. Sections
C–E are breadth/smoke.

**How to use it:** open each page in the docs app, exercise the interaction in the box, and
confirm the **Expected** result. Keep the browser **DevTools Console open** the whole time —
a clean console (no red errors / no "is not a function" / no NRE) is itself a pass criterion
on every page (Section D).

Legend: ⬜ = to test · ✅ = pass · ❌ = fail (note page + what you saw)

---

## A. Behaviour changes (4.0.0) — verify these explicitly

These changed on purpose. A consumer who upgrades will see the new behaviour, so confirm each
is correct.

### A1 — Badge: removable badge is now fully controlled ⬜
- Page: `/components/badge`
- Do: click the **×** on a removable/dismissable badge.
- Expected: the badge does **not** vanish on its own. It disappears only because the demo
  removes the item from its model in `OnRemove`. If a demo's list is `@key`'d, removing one
  badge must not visually "shift" or duplicate the others.
- Why it matters: previously the badge optimistically hid itself; now visibility is data-driven
  (see `MIGRATION.md`). A consumer who *only* relied on the auto-hide and never removed from
  their model would now see the badge stay — that's the intended new contract.

### A2 — Progress / Gauge / RingProgress: out-of-range clamping + indeterminate ARIA ⬜
- Pages: `/components/progress`, `/components/gauge`, `/components/ring-progress`
- Do: find demos with values, and (if a demo exposes it) push value past Max or negative.
- Expected:
  - `Value=150, Max=100` renders as **full** (100%), not overflowing the track.
  - Negative value renders as **empty** (0%), not inverted/NaN.
  - Indeterminate/loading variant: in DevTools, the element has `aria-busy="true"` and **no**
    `aria-valuenow` (no stale frozen number).

### A3 — Internal state survives a same-content data refresh (library-wide) ⬜
This is the single biggest behaviour change. Pick a few stateful widgets and confirm UI state
is **not** wiped when data is re-supplied or the parent re-renders.
- Suggested pages: `/components/data-grid`, `/components/data-table`, `/components/select`,
  `/components/combobox`, `/components/tree-view`, `/components/tabs`, `/components/pagination`,
  `/components/carousel`, `/components/accordion`.
- Do, on each: establish some state (select a row/value, expand a node, switch to tab 3, go to
  page 4, open a section), then trigger anything that re-renders the demo (toggle a nearby
  control, change a prop in the demo, re-run an async "reload" if present).
- Expected: selection / checked / expanded / active-tab / current-page / scroll / open-section
  **survive**. They must not snap back to the default.
- Edge: an **empty → refill** async load (a list that starts empty then gets data) should land
  on the data without losing a selection the user made meanwhile.

### A4 — Roving keyboard order tracks live DOM order after reorder ⬜
- Pages: `/components/radio-group`, `/components/toggle-group`, `/components/segmented`,
  `/components/stepper`, `/components/splitter`, `/components/steps`, `/components/accordion`.
- Do: focus the group, then use **Arrow keys** (←/→ or ↑/↓) to move between items. On Steps,
  check the visible **numbering**. If a demo can reorder/insert items, do that first, then arrow.
- Expected: arrow navigation visits items in **visual order**, Home/End jump to first/last
  visually, and Steps renumber 1..n in visual order — even after a reorder or a middle-insert.
  Focus must never "skip" or land on the wrong neighbour.

---

## B. New features (4.0.0) — verify they work

### B1 — Icon / DynamicIcon (issue #348 docs fix) ⬜
- Page: `/components/dynamic-icon`
- Expected: the **new top section "How to use it (in your app)"** is present and shows the
  **public `<Icon>`** component (`Name="Search"`, `Svg="Lucide.Star"`, `Title="Download"`).
  The page makes clear `DynamicIcon` is a **docs-only** helper and real apps use `<Icon>`.
- Do: confirm `<Icon Name="...">` renders a Lucide glyph; an unknown `Name` falls back to a
  circle (not a crash); `Svg=` wins over `Name=`; `Title=` makes it non-decorative.
- Also `/components/icon` for the full `<Icon>` reference.

### B2 — DataTable.ItemKey (selection survives reference-distinct reload) ⬜
- Page: `/components/data-table`
- Do: select some rows; if a demo offers a "reload"/"refresh" that re-supplies equal-but-new
  row instances, trigger it.
- Expected: selection **sticks** to the same logical rows after the reload (that's what
  `ItemKey` buys). Without it, selection would clear.

### B3 — RadioGroup.Name participates in form submission ⬜
- Page: `/components/radio-group` (and `/components/form`)
- Do: in DevTools, inspect a RadioGroup that has `Name` set.
- Expected: a **hidden `<input>`** carrying the selected value is present, so the group posts
  with a native `<form>`.

### B4 — Gantt live options refresh ⬜
- Page: `/components/gantt`
- Do: if a demo toggles `Readonly` / `TodayHighlight` / `BarHeight` / `ColumnWidth` after the
  chart is shown, toggle it.
- Expected: the **already-rendered** chart updates (bar height changes, today highlight
  appears/disappears, etc.) — it no longer requires a remount. No console error from the
  `gantt.refresh` interop path.

### B5 — AsChild triggers (no nested-button) ⬜
- Pages: `/components/alert-dialog`, `/components/drawer`
- Do: inspect an `AsChild` trigger demo in DevTools.
- Expected: the trigger is a **single real `<button>`** (or the child element), **not** a
  `div[role="button"]` wrapping a `<button>`. Activating it still opens the dialog/drawer.

### B6 — Other additive parity items (spot-check) ⬜
- Tabs `IconReveal` (`/components/tabs`): inactive triggers collapse to icon-only; the active
  one animates its label open.
- Card `CardTitle`/`CardDescription` (`/components/card`): render as `<h3>`/`<p>`.
- DirectionProvider (`/components/direction-provider` if present): wrapping in
  `Direction="Rtl"` mirrors descendant layout (sets native `dir`).

---

## C. RTL / theming (parity audit — should be visually identical in LTR) ⬜
- Theme switch (`/` header or `/components/theme-switcher`): cycle all 8 themes + light/dark.
  Expected: colours look right (OKLCH migration is a 1:1 conversion — **no** visible brand
  shift), no flash of unstyled / wrong colour.
- RTL: if a DirectionProvider/RTL demo exists, flip to RTL and confirm a few layouts mirror
  (icons, paddings, start/end alignment) without breaking LTR pages.

---

## D. Per-area smoke (breadth) — console must stay clean ⬜
Open each area's pages, interact briefly, watch the **Console**. Any red error, "is not a
function", or unhandled NRE on mount/interact = ❌ (note the page).

- ⬜ **Forms**: input, textarea, select, combobox, checkbox, switch, radio-group, slider,
  date/time pickers, tag-input, form, form-field, input-mask.
- ⬜ **Overlays**: dialog, alert-dialog, sheet, drawer, popover, tooltip, hover-card, toast,
  dropdown-menu, context-menu, menubar. (Open → focus-trap → Esc closes → focus returns.)
- ⬜ **Data**: data-grid, data-table, tree-view, tree-select, transfer, pick-list, pagination,
  kanban, timeline, calendar, scheduler, gantt.
- ⬜ **Navigation**: tabs, sidebar, navigation-menu, mega-menu, breadcrumb, steps, stepper.
- ⬜ **Display**: card, badge, alert, avatar, table, list, chart, gauge, ring-progress,
  progress, sparkline, kpi-card, statistic.
- ⬜ **Media / misc**: image, image-compare, file-viewer, pdf-viewer, code-editor, watermark,
  carousel, tour, signature-pad.
- ⬜ Resize the window narrow (mobile): overlays become drawers/sheets where designed; faceted
  filters use the popover/drawer shell; nothing overflows or clips.

> Automated backstop already green: a bUnit smoke renders **all 174 component docs pages** with
> the real docs DI and asserts none throw (`tests/Lumeo.Docs.Tests/AllComponentPagesRenderTests.cs`).
> That covers *render-time* errors headlessly; this manual pass covers *interaction* + visuals.

---

## E. Release gates (before tagging v4.0.0) ⬜
- ⬜ `Directory.Build.props` `<Version>` = **4.0.0** (confirmed).
- ⬜ `CHANGELOG.md` has a dated `[4.0.0]` section (confirmed) and `[Unreleased]` is empty.
- ⬜ `MIGRATION.md` has the "Migrating to Lumeo 4.0" section (the 4 behaviour changes).
- ⬜ Full solution build is clean: `dotnet build Lumeo.slnx -c Release --arch x64` (warnaserror).
- ⬜ Full test suite green (~4825 tests).
- ⬜ RegistryGen produced no *content* drift beyond intended changes (revert cdn-deps
  timestamp-only churn before committing).
- ⬜ Tag/merge: `feat/parity-campaign` → `main`, tag **v4.0.0**. (Merging to the default branch
  auto-closes issue #348.)

---

### Quick smoke (10 min, if short on time)
1. `/components/badge` → remove a badge (A1).
2. `/components/progress` → over-range clamps (A2).
3. `/components/data-grid` → select a row, re-render, selection survives (A3).
4. `/components/radio-group` → arrow keys rove in visual order (A4) + hidden input for `Name` (B3).
5. `/components/dynamic-icon` → "How to use it" `<Icon>` section present (B1, #348).
6. Cycle 2–3 themes + dark mode (C).
7. Console clean throughout (D).
