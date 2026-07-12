# Screen-reader test protocol — top-20 interactive components

Repo-internal test protocol. **Not a docs page** — this lives under `docs/superpowers/`
so it never gets picked up by the docs-site nav/registry, unlike the shadcn-style
component pages under `docs/Lumeo.Docs/`.

## Scope and how the top-20 was chosen

Ranked by keyboard-interaction depth from the registry's static a11y extraction
(`tools/lumeo-mcp/src/components-api.json` → `components.<Name>.a11y`, produced by
`ComponentsApiEmitter.ExtractA11y` — a regex scan of each component's `.razor` source
for `@onkeydown`/`KeyboardEventArgs` handling, `role="…"`, and `aria-*` attributes).
This is the same signal the MCP's `lumeo_get_a11y` tool serves, so "top 20" here means
the 20 components with the most distinct `KeyboardEventArgs.Key` values handled —
a reasonable proxy for "richest keyboard/SR surface, most to break."

Regenerate the ranking after component changes with:

```bash
dotnet run --project tools/Lumeo.RegistryGen
node -e "const j=require('./tools/lumeo-mcp/src/components-api.json'); \
  Object.entries(j.components).map(([n,c])=>[n,(c.a11y?.keys||[]).length]) \
  .sort((a,b)=>b[1]-a[1]).slice(0,20).forEach(([n,k])=>console.log(n,k))"
```

The 20 covered here (component — category — keys handled):

1. DataGrid — Data Display — 14
2. FileManager — Data Display — 10
3. Tabs — Navigation — 10
4. Calendar — Data Display — 9
5. Cascader — Forms — 9
6. ColorPicker — Forms — 9
7. Combobox — Forms — 9
8. ContextMenu — Overlay — 9
9. SpeedDial — Navigation — 9
10. TreeSelect — Forms — 9
11. Select — Forms — 8
12. Splitter — Navigation — 8
13. TreeView — Data Display — 8
14. DropdownMenu — Overlay — 7
15. MegaMenu — Navigation — 7
16. Mention — Forms — 7
17. Menubar — Navigation — 7
18. Kanban — Drag & Drop — 6
19. NavigationMenu — Navigation — 6
20. OtpInput — Forms — 6

## Setup

**NVDA (Windows)**
1. Install NVDA (free): https://www.nvaccess.org/download/
2. Launch it, then launch **Firefox** (NVDA's browse-mode behavior is best documented
   against Firefox; Chrome/Edge are acceptable fallbacks but expect minor announcement
   differences, note them in the Result column rather than treating them as failures).
3. Start the docs site: `dotnet run --project docs/Lumeo.Docs -c Release --urls http://localhost:5290`
   and open `http://localhost:5290/components/<slug>` in Firefox.
4. NVDA starts each page in **Browse mode** (reads content, arrow keys move a virtual
   cursor). Form fields/widgets that trap focus (comboboxes, grids, trees) switch NVDA
   into **Focus mode** automatically, or press `NVDA+Space` to toggle manually. Each
   walkthrough below says which mode each step expects.
5. Useful NVDA commands used throughout: `Tab`/`Shift+Tab` (next/prev focusable),
   `NVDA+Down` (read from cursor), `H` (next heading, browse mode), `NVDA+T` (read title).

**VoiceOver (macOS)**
1. Enable: System Settings → Accessibility → VoiceOver, or `Cmd+F5`.
2. Use **Safari** (VoiceOver + Safari is Apple's primary-supported combination; Chrome
   on macOS has known VoiceOver gaps, so treat Safari results as authoritative).
3. Open the same `http://localhost:5290/components/<slug>` route.
4. VoiceOver commands used throughout: `VO` = `Control+Option`. `VO+Right/Left` (next/
   prev item), `Tab` (next focusable — same as any browser), `VO+Space` (activate),
   `VO+Shift+Down`/`VO+Shift+Up` (enter/exit a group or interact with a widget).

## Result legend

Fill the **Result** column per row: `PASS`, `FAIL — <what was announced/missing instead>`,
or `N/A` (feature not present in that browser/SR combo). A component only "passes SR
testing" once every row across both SRs is `PASS` or an explicitly justified `N/A`.

---

### 1. DataGrid — `/components/data-grid`

| # | Action | Expected NVDA (Firefox) | Expected VoiceOver (Safari) | Result |
|---|---|---|---|---|
| 1 | Tab to the grid | Browse→Focus mode auto; "table, N columns, N rows" or "grid" with column/row count (`aria-colcount`/`aria-rowcount`) | "table, N rows N columns" | |
| 2 | `ArrowRight`/`ArrowLeft` across a row | Each cell's column header name + value announced (`aria-colindex`) | Column header + cell value announced | |
| 3 | `ArrowDown`/`ArrowUp` across a column | Row changes announced; `aria-rowindex` / row number if present | Row position announced | |
| 4 | `Home` / `End` on a row | "column 1" / last column reached | Jumps to first/last cell in row | |
| 5 | `Enter` or `F2` on an editable cell | "edit mode" / editable field role announced, e.g. "editable text" | Enters edit; "text field, editing" | |
| 6 | `Escape` while editing | Returns to grid navigation, no value committed announced as such | Exits edit mode | |
| 7 | `Space` on a row with a checkbox column | "checked"/"not checked" toggle announced (`aria-checked`) | "checked"/"unchecked" | |
| 8 | Trigger sort on a column header (`Enter` on header) | `aria-sort="ascending"` change announced on next visit to that header | Sort state announced when re-visiting header | |
| 9 | Open a row's context menu (if present) | `role="menu"`/`menuitem` list announced, arrow-navigable | Menu announced, items navigable with VO+arrows | |
| 10 | Pin/unpin a column (if exposed via keyboard) | State change announced via `aria-*` or live region (`role="status"`) | Status update announced | |

### 2. FileManager — `/components/file-manager`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab into the folder tree | "tree" role announced, current treeitem + level | "tree", item + level announced | |
| 2 | `ArrowDown`/`ArrowUp` | Moves between tree items, name announced | Same | |
| 3 | `ArrowRight` on a collapsed folder | "expanded" announced, children now reachable | "expanded" | |
| 4 | `ArrowLeft` on an expanded folder | "collapsed" announced | "collapsed" | |
| 5 | `Enter` on a file/folder | Opens/selects; selection state announced | Same | |
| 6 | `Home`/`End` in the tree | Jumps to first/last visible item | Same | |
| 7 | Tab to the file list region | List/grid of files announced with item count | Same | |
| 8 | Open context menu on a file (`Escape` to dismiss) | `role="menu"`, `menuitem`s announced; Escape closes and returns focus to trigger | Same | |
| 9 | Rename inline (`Enter`, edit, `Escape`/`Enter`) | Edit field announced, focus returns to item on commit/cancel | Same | |

### 3. Tabs — `/components/tabs`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to the tablist | "tab list", N tabs; first/selected tab announced with "selected" | "tab group", selected tab announced | |
| 2 | `ArrowRight`/`ArrowLeft` | Moves selection between tabs (or just focus, per activation pattern), tab name + selected state announced | Same, VO+arrows if in a group | |
| 3 | `Home`/`End` | Jumps to first/last tab | Same | |
| 4 | Confirm the visible panel changes and `aria-controls`/`aria-labelledby` link the tab to its panel | Panel content read is the NEW panel's content, not stale | Same | |
| 5 | `Delete` on a closable tab (if enabled) | Tab removal announced (focus moves predictably to a neighboring tab) | Same | |
| 6 | Tab (real Tab key) out of the tablist into panel content | Focus lands inside the panel, panel role/label announced once | Same | |

### 4. Calendar — `/components/calendar`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab into the grid | "grid" or "table", month/year announced (via a visible/aria-label heading) | "table"/"grid" with date context | |
| 2 | `ArrowRight`/`ArrowLeft` | Day-by-day, full date announced (e.g. "Tuesday, July 14 2026") — not just the number | Same full-date announcement | |
| 3 | `ArrowUp`/`ArrowDown` | Same date, one week earlier/later | Same | |
| 4 | `PageUp`/`PageDown` | Moves one month; new month/year context announced | Same | |
| 5 | `Home`/`End` | First/last day of the visible week or month | Same | |
| 6 | `Space`/`Enter` to select a date | "selected" state announced (`aria-selected`) | Same | |
| 7 | Range mode: select start then end date | Both endpoints announced distinctly; in-between days optionally announced as "in range" | Same | |

### 5. Cascader — `/components/cascader`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to the trigger | Button/combobox role, current value (if any), `aria-haspopup` announced | Same | |
| 2 | `Enter`/`ArrowDown` to open | First-level `role="menu"` opens, first item focused/announced | Same | |
| 3 | `ArrowDown`/`ArrowUp` within a level | Item name announced, "has submenu" if it expands another level | Same | |
| 4 | `ArrowRight` on an item with children | Next-level menu opens, focus moves in, first child announced | Same | |
| 5 | `ArrowLeft` | Returns focus to the parent level, parent item re-announced | Same | |
| 6 | `Enter`/`Space` on a leaf item | Selection committed, menu closes, trigger now announces the full path/value | Same | |
| 7 | `Escape` at any level | Closes the whole cascade, focus returns to trigger | Same | |

### 6. ColorPicker — `/components/color-picker`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to the swatch trigger | Button announces current color (via `aria-label`, not just a visual swatch) | Same | |
| 2 | `Enter` to open | `role="dialog"` announced, focus moves inside (hue/SV area) | "dialog" announced | |
| 3 | Tab to the hue slider | `role="slider"`, current value announced (`aria-valuenow`/`aria-valuetext`, ideally a human hue name/degrees, not a raw 0–1 float) | Slider + value announced | |
| 4 | `ArrowLeft`/`ArrowRight` on the slider | Value changes announced incrementally | Same | |
| 5 | `Home`/`End` on the slider | Min/max value announced | Same | |
| 6 | Tab to the hex text input | Editable text field, current hex value announced | Same | |
| 7 | `Escape` | Dialog closes, focus returns to trigger, trigger announces the newly-picked color | Same | |

### 7. Combobox — `/components/combobox`

| # | Action | Expected NVDA (Focus mode) | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to the input | `role="combobox"`, `aria-expanded="false"`, editable text announced | "combo box, collapsed" | |
| 2 | Type a filter character | `aria-controls` listbox updates; NVDA does NOT auto-announce every keystroke's result — announce only on `ArrowDown` per step 3 (verify no double-announcement spam) | Same restraint expected | |
| 3 | `ArrowDown` | `aria-expanded="true"`, first filtered option announced via `aria-activedescendant` (option text + position "1 of N" if exposed) | Option announced | |
| 4 | `ArrowUp`/`ArrowDown` through options | Each option announced once, no repeats/skips | Same | |
| 5 | `Enter` on a highlighted option | Selection committed, listbox closes, input shows/announces the chosen value | Same | |
| 6 | `Escape` while open | Closes listbox without changing the value, focus stays in the input | Same | |
| 7 | `Backspace` clearing to empty (if clearable) | Value-cleared state announced, not silent | Same | |
| 8 | Multi-select variant: `Space` on an option | "checked"/"not checked" toggle announced, listbox stays open | Same | |

### 8. ContextMenu — `/components/context-menu`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Focus the trigger area, open via `Shift+F10` or the `ContextMenu` key | `role="menu"` announced, first `menuitem` focused/announced | Same | |
| 2 | `ArrowDown`/`ArrowUp` | Each item announced (checkbox/radio items announce their checked state too) | Same | |
| 3 | `ArrowRight` on an item with a submenu | Submenu opens, first item announced | Same | |
| 4 | `ArrowLeft` in a submenu | Returns to parent item | Same | |
| 5 | `Enter`/`Space` on an item | Action fires, menu closes, focus returns to a sane place (the trigger or the acted-on element) | Same | |
| 6 | `Escape` | Menu closes without firing an action, focus returns to trigger | Same | |
| 7 | `Home`/`End` | Jumps to first/last item at the current level | Same | |

### 9. SpeedDial — `/components/speed-dial`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to the FAB trigger | Button announced with `aria-expanded="false"`, `aria-haspopup` | Same | |
| 2 | `Enter`/`Space`/`ArrowDown` to open | `aria-expanded="true"`, fanned-out actions become reachable, `role="menu"`/`menuitem` announced | Same | |
| 3 | `ArrowDown`/`ArrowUp` (or Left/Right if horizontal) | Each action's label announced | Same | |
| 4 | `Home`/`End` | First/last action | Same | |
| 5 | `Enter` on an action | Action fires, dial closes, focus returns to trigger | Same | |
| 6 | `Escape` | Closes without firing, focus returns to trigger | Same | |

### 10. TreeSelect — `/components/tree-select`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to the trigger | Button/combobox, current selection(s) announced | Same | |
| 2 | `Enter`/`ArrowDown` to open | `role="tree"` opens, first `treeitem` announced with level (`aria-level`) | Same | |
| 3 | `ArrowRight`/`ArrowLeft` on a parent node | Expand/collapse announced | Same | |
| 4 | `ArrowDown`/`ArrowUp` | Moves between visible nodes only (collapsed children skipped) | Same | |
| 5 | `Space`/`Enter` on a node | Selection (or check state, if multi-select) announced | Same | |
| 6 | `Escape` | Closes tree, focus returns to trigger, trigger announces the new value(s) | Same | |

### 11. Select — `/components/select`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to the trigger | `role="combobox"`/button, current value announced | Same | |
| 2 | `Enter`/`Space`/`ArrowDown` to open | `role="listbox"` opens, selected (or first) option announced | Same | |
| 3 | `ArrowDown`/`ArrowUp` | Each option announced, grouped options announce their group label once | Same | |
| 4 | Type-ahead (type a letter) | Jumps to next option starting with that letter, announced | Same | |
| 5 | `Enter`/`Space` to commit | Listbox closes, trigger announces new value | Same | |
| 6 | `Escape` | Closes without changing value | Same | |
| 7 | Disabled option present | Announced as "unavailable"/"dimmed", skipped by arrow nav or announced-but-not-selectable | Same | |

### 12. Splitter — `/components/splitter`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to the divider | `role="separator"`, `aria-orientation`, current position (`aria-valuenow`/min/max) announced | Same | |
| 2 | `ArrowLeft`/`ArrowRight` (horizontal) or `ArrowUp`/`ArrowDown` (vertical) | Value change announced incrementally | Same | |
| 3 | `Home`/`End` | Min/max collapse of a pane, announced | Same | |
| 4 | `PageUp`/`PageDown` | Larger-step value change announced | Same | |
| 5 | Confirm panel content is still reachable via Tab on both sides after resize | Focus order unaffected by the resize | Same | |

### 13. TreeView — `/components/tree-view`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab into the tree | `role="tree"`, first `treeitem` announced with `aria-level`/`aria-posinset`/`aria-setsize` ("item 2 of 5, level 1") | Same | |
| 2 | `ArrowDown`/`ArrowUp` | Next/prev visible item, level change announced when it changes | Same | |
| 3 | `ArrowRight` on collapsed parent | Expands, "expanded" announced | Same | |
| 4 | `ArrowLeft` on expanded parent | Collapses, "collapsed" announced; on a leaf, moves focus to parent | Same | |
| 5 | `Space`/`Enter` for selection | "selected" state announced; multi-select announces count if exposed | Same | |
| 6 | `Home`/`End` | First/last visible item in the whole tree | Same | |

### 14. DropdownMenu — `/components/dropdown-menu`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to the trigger button | `aria-haspopup="menu"`, `aria-expanded="false"` | Same | |
| 2 | `Enter`/`Space`/`ArrowDown` to open | Menu opens, first item announced, `aria-expanded="true"` | Same | |
| 3 | `ArrowDown`/`ArrowUp` | Item-by-item announced, incl. checkbox/radio item states | Same | |
| 4 | `ArrowRight` on submenu item | Submenu opens and is announced | Same | |
| 5 | `Home`/`End` | First/last item | Same | |
| 6 | `Enter`/`Space` | Fires action, closes, focus returns to trigger | Same | |
| 7 | `Escape` | Closes without firing | Same | |

### 15. MegaMenu — `/components/mega-menu`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to a top-level trigger | `aria-haspopup`, `aria-expanded="false"` | Same | |
| 2 | `Enter`/`ArrowDown` to open | Wide panel opens; focus moves to first item/column; column groupings announced (`role="group"`) | Same | |
| 3 | `ArrowRight`/`ArrowLeft` across columns | Moves between columns, column heading context preserved | Same | |
| 4 | `ArrowDown`/`ArrowUp` within a column | Item-by-item | Same | |
| 5 | `Escape` | Closes panel, focus returns to the top-level trigger | Same | |
| 6 | Tab past the open panel | Panel closes (focus leaving it doesn't strand it open) and next top-level item receives focus | Same | |

### 16. Mention — `/components/mention`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to the textarea, type `@` | Editable text field with `aria-haspopup`/`aria-expanded` toggling on `@` | Same | |
| 2 | Type a filter fragment after `@` | Popup `role="listbox"` updates via `aria-controls`; announced only on navigation, not per keystroke | Same | |
| 3 | `ArrowDown`/`ArrowUp` | Option announced via `aria-activedescendant` | Same | |
| 4 | `Enter`/`Tab` to accept a mention | Popup closes, inserted mention text is read back as part of the field content | Same | |
| 5 | `Escape` | Popup closes, `@` fragment left as literal text, no selection made | Same | |

### 17. Menubar — `/components/menubar`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to the menubar | `role="menubar"`, first top-level `menuitem` announced | Same | |
| 2 | `ArrowRight`/`ArrowLeft` across top-level items | Moves focus between top-level menus without opening them (unless one is already open) | Same | |
| 3 | `ArrowDown`/`Enter` to open one | Its `role="menu"` opens, first item announced | Same | |
| 4 | `ArrowRight` while a menu is open, on a sibling top-level item | Closes current menu, opens the sibling's menu (menu-to-menu nav) | Same | |
| 5 | `Escape` | Closes open menu, focus returns to its top-level menubar item (not out of the menubar entirely) | Same | |
| 6 | `Home`/`End` at top level | First/last top-level item | Same | |

### 18. Kanban — `/components/kanban`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to a card | Card content announced; `aria-roledescription` (e.g. "draggable card") announced if set | Same | |
| 2 | `Space`/`Enter` to pick up (keyboard DnD) | Grabbed state announced ("grabbed"/instructions), since native drag events are pointer-only | Same | |
| 3 | `ArrowLeft`/`ArrowRight` while grabbed | Moves the card between columns/swimlanes, target column name announced | Same | |
| 4 | `ArrowUp`/`ArrowDown` while grabbed | Moves the card up/down within a column, new position announced | Same | |
| 5 | `Enter`/`Space` to drop | Drop confirmed, "grabbed" state cleared, final position announced | Same | |
| 6 | `Escape` while grabbed | Cancels the move, card returns to its original position, cancellation announced | Same | |

### 19. NavigationMenu — `/components/navigation-menu`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to a top-level nav trigger | `aria-haspopup="menu"`/`aria-expanded="false"`, `aria-current` announced if it's the active page | Same | |
| 2 | `Enter`/`ArrowDown` to open | Dropdown panel opens, first link/item announced | Same | |
| 3 | `ArrowDown`/`ArrowUp` within panel | Item-by-item | Same | |
| 4 | `Home`/`End` within panel | First/last item | Same | |
| 5 | `Escape` | Closes panel, focus returns to the trigger | Same | |
| 6 | Tab through to a plain (non-dropdown) top-level link | Announced as a plain link, no `aria-haspopup` | Same | |

### 20. OtpInput — `/components/otp-input`

| # | Action | Expected NVDA | Expected VoiceOver | Result |
|---|---|---|---|---|
| 1 | Tab to the first box | Editable text field, `aria-label` identifies it as e.g. "digit 1 of 6" | Same | |
| 2 | Type a digit | Auto-advances to the next box; NVDA announces the NEW box's label, not silence | Same | |
| 3 | `ArrowRight`/`ArrowLeft` | Moves between boxes without altering values, each box's label + current value announced | Same | |
| 4 | `Backspace` on an empty box | Moves focus to the previous box (common OTP pattern) — confirm this doesn't strand focus silently | Same | |
| 5 | `Home`/`End` | Jumps to first/last box | Same | |
| 6 | Paste a full code (`Ctrl+V`) | All boxes fill, focus lands on the last box or a submit control, and the fill is announced (e.g. via a live region) rather than silent | Same | |

---

## After a run

- File a GitHub issue per `FAIL` row, tagged `a11y` + the component name, quoting the
  expected vs. actual announcement.
- If a `FAIL` traces back to a missing `aria-*` attribute or wrong `role`, cross-check
  `tools/lumeo-mcp/src/components-api.json` → `components.<Name>.a11y` — if the registry's
  static extraction *also* missed it, the underlying regex heuristic
  (`PerComponentEnricher.cs` keyboard scan, `ComponentsApiEmitter.ExtractA11y`) may need
  a matching fix so the a11y data stays trustworthy for the next protocol run.
- Re-run only the failed components after a fix; no need to re-walk all 20 for a
  single-component patch.
