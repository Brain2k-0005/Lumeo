# Lumeo Component Audit — Working Document

Full-library audit of all 140 components in `src/Lumeo/UI/` against the
shadcn/ui (Radix), Blueprint.js, MudBlazor and Ant Design behavior standards.
Goal: Lumeo as the only UI library a Blazor app needs.

- **Audit date:** 2026-06-11 (source: 6 parallel deep-read passes over every
  `.razor`/`.razor.cs` + the interop JS)
- **Severity:** P0 crash/data-loss · P1 broken behavior or hard a11y fail ·
  P2 wrong-but-survivable · P3 nice-to-have
- This file is the backlog for the ongoing hardening loop. Items move to
  "Fixed" with the iteration that shipped them.

---

## Fixed in iteration 1 (this PR)

| Component | Was | Fix |
|---|---|---|
| Input | P1 — Clearable flipped render branch on first/last char → `<input>` recreated, focus+caret lost | wrapper branch keyed off `Clearable` itself |
| Form | P1 — `Validate()` returns only failures; fixed fields stayed in `Errors` forever → `OnValidSubmit` unreachable after one invalid submit | `_syncErrorKeys` tracking; async-sourced errors still survive |
| Slider | P1 — range mode: end input overlaid full track with `pointer-events:auto` → start thumb un-grabbable | hit-testing moved to thumb pseudo-elements |
| OtpInput | P1 — JS paste stripped `\D` before C# ever saw it → alphanumeric codes destroyed | JS forwards raw text; C# `FilterInput` filters per `InputMode` |
| Mention | P1 — ArrowDown with 0 matching options → `DivideByZeroException` (circuit killer) | count guard on both arrow paths |
| FileUpload | P1 — `GetMultipleFiles(MaxFiles + 1)` throws when user picks more | passes actual `e.FileCount`; MaxFiles handling unchanged |
| Splitter | P1 — blanket `@onkeydown:preventDefault` on `tabindex=0` divider swallowed Tab → WCAG 2.1.2 keyboard trap | new key-selective JS preventDefault (arrows only) |
| Carousel | P1 — same blanket preventDefault on the region killed Tab + typing in slide content; Loop prev-wrap sent `int.MaxValue`, JS bounds-check made it a no-op | selective preventDefault (orientation arrows, skips editable targets); JS clamps wrap sentinel to last slide |
| PromptInput | P1×2 — render-time preventDefault one event late (ghost newline on send, next key swallowed); no `IsComposing` guard (IME-confirm Enter submitted) | JS-side Enter suppression (`RequireNoModifiers`+`SkipComposing`) + `e.IsComposing` guard |
| AspectRatio | P1 — `padding-bottom: 56,25%` on comma-decimal locales (invalid CSS → zero height) | invariant formatting + non-positive ratio fallback |
| Watermark | P1 — same culture bug inside the data-URL SVG (`width="236,8"`) → watermark vanished | `FormattableString.Invariant` |
| Progress | P2 — negative `Value` → `width: -N%` → browser drops decl → FULL bar | `Math.Clamp(0..100)` |
| Grid/Container | P1/P2 — `grid-cols-{9..12}`, `max-w-6xl/7xl` missing from shipped bundle (runtime interpolation invisible to Tailwind) | `@source inline()` safelist + bundle rebuild |

New shared infrastructure: `IComponentInteropService.RegisterPreventDefaultKeys`
(`PreventDefaultKeyRule`: per-key, `RequireNoModifiers`, `SkipComposing`,
`SkipEditable`) — use it to migrate the remaining stale-flag
`@onkeydown:preventDefault="@_flag"` sites (RadioGroup, Tabs, Rating, Card,
CollapsibleTrigger, AudioPlayer).

---

## P1 backlog (next iterations, highest value first)

### Selection / pickers (flagship-critical)
- [ ] **Select + Combobox**: data-bound mode never calls `Context.RegisterItem` (SelectContent.razor:131-197, ComboboxContent.razor:147-221) → Arrow/Home/End/Enter dead whenever `Items` is used. Searchable variants: `_items.Clear()` on search but surviving items keep `_registered=true` → keyboard dead after first keystroke (Select.razor:221-227, Combobox.razor:244-251; also mis-reports `ItemCount`→ spurious `ComboboxEmpty`).
- [ ] **Command**: `CommandItem.Disabled` dead (not rendered, not gated); NO keyboard navigation at all (cmdk's core feature: arrows/Enter/listbox roles/aria-activedescendant).
- [ ] **DatePicker**: typed input bypasses `MinDate`/`MaxDate`/`IsDateDisabled` (CommitBufferAsync:599-614). Range presets broken end-to-end: `HandlePresetSelected` sets only `Value`; DateRangePicker maps presets dropping `End` (716-722; DateRangePicker.razor:53-54).
- [ ] **TreeView** cluster: selection binding inert (`OnSelectionChanged` never invoked); children keyboard-unreachable (tabindex=-1, no Up/Down movement); checking a node in filtered view mutates clones → checks lost (TreeView.razor:166-170).
- [ ] **Mention**: no preventDefault on Enter/arrows while dropdown open → newline inserted + caret moves before `SelectOption` reads it (mangled insertion). Migrate to `RegisterPreventDefaultKeys`.

### Overlays / menus
- [ ] **Menu family Enter/Space double-fire**: keydown opens, native click toggles closed — MenubarTrigger:53-78, DropdownMenuSubTrigger:66-89, ContextMenuSubTrigger:56-76, MegaMenuItem:132-139. Keyboard activation is a net no-op.
- [ ] **DropdownMenu/Menubar click-outside**: registered with `null` trigger exclusion → clicking the open trigger closes-then-reopens; menu can't be dismissed via its own trigger (DropdownMenuContent.razor:70, MenubarContent.razor:30; Select/Combobox same at SelectContent.razor:206/ComboboxContent.razor:228).
- [ ] **ContextMenu**: content never focused on open → all keyboard handlers unreachable (ContextMenuContent.razor:27-43 — DropdownMenuContent:78-81 contains the exact fix); no viewport collision clamp for the root menu.
- [ ] **Focus return on close**: no overlay restores focus to its trigger (components.js setupFocusTrap never saves `activeElement`). Radix standard.
- [ ] **Nested overlays**: Escape closes ALL ancestor dialogs/sheets (no stopPropagation once handled) — DialogContent.razor:99-105, Sheet:206, Drawer:132, AlertDialog:50.
- [ ] **Window**: drag/resize without pointer capture → fast drag detaches, pointerup lost, window glues to cursor (Window.razor:18-21,84-87; `SetPointerCaptureOnElement` exists).
- [ ] **Tour**: target never scrolled into view while `LockScroll()` blocks manual scrolling → off-screen steps unreachable (Tour.razor:169-188).

### Layout / structure
- [ ] **Resizable**: drag-dead without group `DefaultSizes` (`_sizes` empty → HandleDrag early-returns, ResizablePanelGroup.razor:78); documented `ResizablePanel.DefaultSize` never consumed. ResizableHandle: focusable but keyboard-inert (no keydown/aria-value*; SplitterDivider has it all).
- [ ] **Tabs**: `FindNextEnabledTab` doesn't check disabled → arrows focus AND activate disabled tabs (TabsTrigger.razor:208-215); `Context.TabIds`/content registrations never removed → closable tabs navigate to ghosts; close affordance not keyboard-operable; stale `_preventKey` flag (first arrow scrolls page).
- [ ] **Stepper**: registration after parent render, nothing re-renders → first render shows no indicators/content (StepperTests works around it with an extra `cut.Render()`); `KeepMounted` parameter dead.
- [ ] **Accordion/Collapsible**: collapsed content stays mounted `aria-hidden` but focusable children remain tabbable (needs `invisible`/`inert` when closed). Accordion also lacks any controlled/two-way open state.
- [ ] **Barcode**: `Format` parameter ignored — EAN13/Code39 silently render as Code128 (Barcode.razor:48,74).
- [ ] **QRCode**: logo overlay mixes px and module units → default logo covers 60-130% of the code, unscannable (QRCode.razor:270-273). Also: 500-line dead `QRCodeEncoder.cs` ships in the package.
- [ ] **FileUpload dropzone**: drop is a no-op (`HandleDrop` only clears hover; InputFile is `sr-only`, no `@ondrop:preventDefault`) → browser may navigate away. Stretch the InputFile transparently over the zone or add JS DataTransfer bridge.
- [ ] **PullToRefresh**: pointermove + `touch-action: pan-y` → native pan claims the gesture on real devices, `pointercancel` kills it (validated only in DevTools mouse emulation per its own comments).

## P2 backlog (grouped)

- **Stale render-time preventDefault flags** (first key scrolls page / next key swallowed): RadioGroupItem:15,113, RadioGroupCard:11,65, TabsTrigger:35,199, Rating:15,245, Card clickable:91, CollapsibleTrigger:17, AudioPlayer:24,330-358. Migrate all to `RegisterPreventDefaultKeys`.
- **RadioGroup**: arrows select disabled siblings (no disabled info in `ItemValues`); items never unregister (ghost entries).
- **NumberInput**: unparseable/cleared input silently keeps old `Value` (can't clear to null); invariant-first parse eats `"1,5"` as 15 before culture fallback; FP step accumulation without `Precision`.
- **InputMask**: Backspace always truncates END regardless of caret (+ double ValueChanged per keystroke); no caret management on mid-string edits.
- **Statistic**: invariant-first parse renders de-DE `"1234,5"` as 12345 (10× wrong) — needs explicit `Culture` parameter or parse-order decision.
- **OtpInput**: rejected char stays visible (value attr unchanged → no DOM rewrite); typing into last box pads interior spaces → `OnComplete` fires with embedded spaces; no Arrow navigation between boxes.
- **Switch**: label not associated (no `for`/`id`) — clicking label doesn't toggle (Checkbox does it right).
- **ToggleGroup**: clearing bound `Value`/`SelectedValues` to null never clears `_selectedItems`; no roving tabindex/arrow nav. **Segmented**: advertises radiogroup semantics without the keyboard contract.
- **TagInput**: Enter adds tag AND submits enclosing form (no preventDefault); suggestions listbox has no keyboard selection.
- **InplaceEditor**: Select mode can't cancel with Esc (blur saves the discarded choice).
- **ConfirmButton/PickList/FileManager/AudioPlayer/ThemeSwitcher/Breadcrumb sr-only**: hardcoded English strings beside the `ILumeoLocalizer` infrastructure.
- **ColorPicker**: popover never focused, Esc dead until click inside; `role=dialog` without focus trap (violates repo overlay rule).
- **Toast**: swipe-to-dismiss exists in interop but ToastProvider never registers it (dead feature).
- **Tooltip**: `Offset` parameter silently ignored; no `aria-describedby` wiring; no Esc dismiss. **HoverCard**: `Side.Left/Right` silently become "bottom". **PopConfirm**: `Placement` parameter dead (always bottom).
- **Backdrops**: `bg-black/80` hardcoded across Dialog/AlertDialog/Sheet/Drawer/Sidebar/Image-lightbox while `--color-overlay-backdrop` token exists (only Tour uses it).
- **Scrollspy**: `scrollspyScrollTo` ignores `Offset` (sticky-header overshoot); `ActiveId` is output-only; unthrottled interop per scroll tick (also BackToTop).
- **BottomNav**: active match keeps query/fragment → `/inbox?f=1` loses active state.
- **Splitter**: `SplitterPane.Collapsible` is dead API; panes added after first render get `Size=0` (invisible); rejects-instead-of-clamps at min/max (fast drags stall short — same in Resizable).
- **TabsList AnimatedIndicator**: never re-measures on resize/font-load (no ResizeObserver).
- **Cascader**: external `Value` reset to null leaves stale path highlight. **TreeSelect**: no Escape/keyboard at all; `ExpandAll` only applies in OnInitialized (async Items never expand). **DateTimePicker**: time-before-date held in resettable private state (wiped by any parent re-render). **Calendar**: `ShowYearPicker` dead; `DisplayDate` never follows external Value changes.
- **FileManager**: stale children after `Root` replacement (`_pathStack[^1].Id == CurrentPath` guard); rows click-only.
- **Sortable**: `Group` parameter dead (cross-list implied, not implemented); handle focusable but inert; touch path never registers if initially `Disabled`. **PivotGrid**: path key joins without separator → collisions corrupt aggregates (`"L" + level + "|" + string.Join("", path)`).
- **Avatar**: `Max`/`Total` don't limit rendering (chip merely appended, hardcoded h-10 w-10); no auto image→fallback chain. **List**: `Href` + `OnClick` silently drops OnClick. **Descriptions**: `dt/dd` without `dl`.
- **Gauge**: zero-value arc renders a dot artifact (round linecap + zero dash). **Image/ImageGallery**: `role=dialog aria-modal` without focus trap.
- **Carousel**: no autoplay/pause-on-hover, no indicator dots. **ScrollArea**: vertical-only, webkit-only (Firefox gets default scrollbars).
- **ThemeSwitcher/ThemeToggle**: System mode never live-updates (no `prefers-color-scheme` change listener, no cross-tab storage sync); ThemeToggle missing mandatory `Class` parameter; unguarded `InitializeAsync` in OnAfterRenderAsync (no JSDisconnectedException catch).
- **ConsentBanner**: all-Required categories → `HasDecided` never true (banner permanently stuck); prefs dialog `aria-modal` without trap/lock/Esc.
- **AgentMessage**: `ts.ToLocalTime()` = SERVER timezone on Blazor Server. **SwipeActions**: no pointer capture (stuck mid-swipe); mouse swipe-open instantly closed by synthesized click. **TouchRipple**: OffsetX/Y vs nested-target misposition; component span ignores `prefers-reduced-motion`. **Affix**: width frozen at fix-time (stale on resize/rotate).
- **ReasoningDisplay**: `Detailed` documented "always open" but only removes line-clamp. **FeatureItem**: `RenderFragment? Icon` shadows the `<Icon>` component (repo's own documented trap; rename to `IconContent`).

## Notable GAPS vs shadcn/Radix/Blueprint/Ant (P1-P2 only)

- **StreamingText/AgentMessageList**: no markdown rendering / code-block highlighting (`Prose` param styles nothing on plain text — misleading); no virtualization for long chats; no scroll-to-bottom button with unread indicator.
- **PromptInput**: no Stop button while streaming; no attachments/paste-image; no char counter.
- **AlertDialog**: initial focus should land on the least-destructive action (Radix).
- **Tooltip**: no provider-level delay grouping/skip-delay. **Popover**: no offset parameter (JS gap hardcoded 4px).
- **Select/Combobox**: no typeahead on closed trigger; composition-mode item `Disabled` missing (Combobox).
- **Tabs**: no manual activation mode; Scrollable list lacks overflow arrows.
- **Resizable**: no collapsible panels, no persisted sizes (`autoSaveId` equivalent), no `SizeChanged`.
- **Pagination**: purely compositional — no data-driven pager (sibling/boundary windowing, page-size selector, jump-to) like MudBlazor/Ant.
- **Accordion**: no `Values`/`ValuesChanged` controlled mode.
- **Table**: no `TableFooter`, no striped/loading/empty helpers. **Timeline**: items are string-only (no ChildContent), no alternate layout.
- **Kanban**: presentational DnD only (no card payload in `OnDrop`, no keyboard alternative, no WIP limits). **QueryBuilder**: `in`/`notIn` get single-value editor while the expression splits on commas.
- **Transfer**: no select-all, no per-item Disabled. **Toast**: swipe regions/promise API present — wire the existing swipe interop.
- **Image**: no preview-zoom, no srcset. **Avatar**: no group overflow handling (see P2). **Statistic**: no count-up animation.
- **AudioPlayer**: no rate control/skip buttons/MediaSession. **SwipeActions**: no full-swipe commit, no keyboard alternative.
- **Catalog level**: component coverage already exceeds shadcn + Blueprint (KeyboardShortcutService, DateRangePicker, Virtualize in pickers all exist). Genuinely absent: OverflowList (Blueprint), InfiniteScroll helper, Masonry. Low urgency.

## Test-coverage holes (no test file at all)

ConfirmButton, UploadTrigger, SignaturePad, OverlayForm, SafeArea, DensityScope,
List, Chip, Icon, AudioPlayer, Hero, CTASection, FeatureGrid, FeatureItem,
PullToRefresh, SwipeActions, TouchRipple.

## Verification notes

- bUnit can't see browser-native behavior: the menu double-fire, pointer-capture
  loss, IME composition and touch gesture bugs are logic-verified from code.
  Worth an E2E (Playwright) pass in `tests/Lumeo.Tests.E2E` for the menu family
  once fixed.
- `"1,5"`→15 NumberInput / `"1234,5"`→12345 Statistic parse findings rest on
  documented `NumberStyles.Any` semantics (AllowThousands) — add unit tests
  when fixing.
