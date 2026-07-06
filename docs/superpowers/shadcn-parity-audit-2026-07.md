# shadcn/ui Parity Audit — July 2026

A component-by-component parity audit of the library against shadcn/ui (and the Base UI
variants shadcn now ships upstream). Goal: a single prioritized backlog for closing the
remaining gaps while recording where the library already exceeds shadcn.

Sources: the shadcn docs index (64 entries) and Base UI variant pages, compared against the
registry at `src/Lumeo/registry/registry.json` (164 components) and component sources under
`src/Lumeo/UI/<Name>/` plus the `Lumeo.*` satellite packages. All findings verified against
the actual `.razor` sources.

---

## 1. Executive summary

| Metric | Count |
| --- | --- |
| Total library components | 164 |
| shadcn index entries | 64 |
| Shared shadcn ↔ library mappings deep-compared | 56 (covering all 61 shared mappings) |
| **HIGH-tier findings** | **5** (+1 potential bug to verify) |
| MEDIUM findings | ~46 |
| LOW findings | ~28 |
| Missing components (shadcn-only, candidates) | 6 |
| Library-only components (no shadcn equivalent, no action) | 103 |

**Headline:** the library is at or ahead of shadcn on the large majority of shared
components. The gaps cluster into a small number of *cross-cutting* themes rather than
one-off defects:

1. **Exit-animation asymmetry (B11-class).** Five overlay/menu components animate open but
   unmount instantly on close: DropdownMenu, HoverCard, Menubar, NavigationMenu, Tooltip.
   The hardened overlays (Dialog, Drawer, Sheet, AlertDialog, Toast) already do symmetric
   `animationend`-driven exits — these five were never brought up to that bar.
2. **Missing `data-state` / `data-*` styling hooks.** Many components drive visuals from C#
   and emit only `aria-*`, so consumers can't use shadcn's `data-[state=…]` /
   `group-data-[collapsible=icon]` CSS selectors: Accordion, Collapsible, Checkbox, Switch,
   Toggle, ToggleGroup, Slider, Progress, Sidebar.
3. **Native form participation.** Checkbox and Switch put `Name` on the `<button>`, which
   posts nothing in a native form; neither renders a hidden input, and Switch has no `Value`.
4. **`asChild` / polymorphism gaps** on Badge, BreadcrumbLink, Collapsible Trigger — common
   Blazor "render as `<a>`/`NavLink`" needs.
5. **AI + Chart affordances.** Chart (canvas/ECharts) has no `accessibilityLayer` equivalent;
   the conversation scroller lacks a scroll-to-latest button, empty state, and export.

Five true HIGH-tier items drive the wave plan below.

---

## 2. Missing components — ranked by consumer value

Candidates from shadcn that have no lightweight library equivalent, ranked most→least
impactful, with a one-line scope estimate.

1. **Field** — composable form-field primitive (Label + control + description + error) that
   removes per-input boilerplate. Highest value: touches every form. *Scope:* new
   `Field` / `FieldLabel` / `FieldDescription` / `FieldError` / `FieldGroup` subcomponents
   wrapping any control with id + `aria-describedby`/`aria-invalid` wiring. Medium.
2. **Input Group** — input with inline addons (icon/text/button prefix & suffix in one
   bordered shell). *Scope:* `InputGroup` container + `InputGroupAddon`/`InputGroupText`/
   `InputGroupButton` slots; the existing `Input` prefix/suffix logic can be factored out.
   Small–Medium.
3. **Item** — generic content-row primitive (media + content + trailing actions) reusable
   across lists, menus, and results. *Scope:* `Item` / `ItemMedia` / `ItemContent` /
   `ItemTitle` / `ItemDescription` / `ItemActions`. Small–Medium.
4. **Native Select** — thin styled wrapper over the native `<select>` for simple/mobile
   cases (the custom `Select` is JS-driven). *Scope:* one styled component + FormField
   wiring. Small.
5. **Attachment** — file/image attachment chip for AI prompt composers (pairs with
   `PromptInput`). *Scope:* `Attachment` chip + `AttachmentList` with preview/remove. Small–Medium.
6. **Marker** — map pin/marker primitive to slot alongside `Map`. Niche. *Scope:* single
   primitive bound to the existing `Map`. Small.

---

## 3. HIGH findings — wave plan

Five HIGH-tier findings, grouped with the tightly-related MEDIUMs that must ship together,
into five waves. Each wave is sized for one preview release.

### Wave 1 — Exit-animation symmetry (B11-class close animations)
*The highest-value parity fix in the audit; one coherent motion pass.*
- **DropdownMenu** — `DropdownMenuContent` is `@if(IsOpen)`; open uses `animate-fade-in`,
  close unmounts instantly. Add symmetric exit + `data-state=closed`.
- **HoverCard** — content enters with `animate-fade-in`, no exit animation (unmounts on close).
- **Menubar** — open uses `animate-fade-in`; Content removed via `@if` with no exit.
- **NavigationMenu** — Content/Viewport `animate-fade-in` on open, no exit (`@if` unmount);
  Radix animates directional `data-motion` slide in **and** out.
- **Tooltip** — content is `@if(IsOpen)` (removed from DOM); enter animates, no exit and no
  `data-state=open/closed`.
- *Model on:* the already-hardened Dialog/Drawer/Sheet/Toast `IOverlayExitCallback` +
  `animationend` pattern (panel stays mounted through the exit).

### Wave 2 — `data-state` / `data-*` styling hooks
*Unlocks shadcn `data-[state=…]` and `group-data-[…]` CSS across the library.*
- **Accordion** — emit `data-state=open|closed`, `data-disabled`, `data-orientation`.
- **Collapsible** — `data-state=open/closed` + `data-disabled`.
- **Checkbox** — `data-state=checked/indeterminate`.
- **Switch** — `data-state=checked/unchecked`.
- **Toggle** / **ToggleGroup** — `data-state="on|off"`.
- **Slider** — `data-state` / `data-orientation`.
- **Progress** — `data-state` (indeterminate/loading/complete).
- **Sidebar** — `data-state` / `data-collapsible` / `data-side` (blocks
  `group-data-[collapsible=icon]` selectors today).

### Wave 3 — Native form participation
*Small surface, high correctness impact.*
- **Switch** (MEDIUM-HIGH) — render a hidden bubble `<input>` for `Name`/`Value` so the
  switch submits in a native form POST; add the missing `Value` prop.
- **Checkbox** — wire `Name` to a hidden `<input>` (currently on the `<button>`, posts
  nothing); add `Value`.
- *Pairs naturally with the new **Field** primitive from §2.*

### Wave 4 — Menu-system interaction parity
*Bring menu/nav components up to Radix/Base UI keyboard + subcomponent expectations.*
- **Menubar** (MEDIUM→HIGH) — add `MenubarCheckboxItem`, `MenubarRadioGroup`,
  `MenubarRadioItem`, `MenubarGroup`, and the `inset` prop (toggle/single-select menus are a
  core menubar use-case, impossible today).
- **Sidebar** (HIGH) — ship **cmd/ctrl+b** as the *default* toggle (`ToggleShortcut` is
  opt-in today; parity users expect it on by default).
- **ContextMenu** / **DropdownMenu** — typeahead (type-to-jump), `ArrowLeft` to close a
  submenu and return focus to parent (only `ArrowRight`-to-open exists), `variant="destructive"`
  item, `inset` prop, and a dedicated `Shortcut` subcomponent.
- **NavigationMenu** — hover-close grace-delay / pointer-grace (menus only close on
  click-outside today) + controlled `value`/`defaultValue`.
- **Potential bug to verify (HIGH):** `NavigationMenuViewport` sizes with
  `h-[var(--radix-navigation-menu-viewport-height)]` but no code sets that CSS var — the
  viewport collapses to 0 height if the Viewport path is used (default per-Content path is
  fine). Confirm and either populate the var or drop the Viewport.

### Wave 5 — AI conversation + Chart accessibility
*Close the AI-family and Chart a11y gaps.*
- **Chart** (HIGH, a11y — verify) — ECharts renders to canvas; there is no equivalent to
  shadcn's `accessibilityLayer` (keyboard access + screen-reader table). Add an SR fallback /
  data table and keyboard affordance.
- **AgentMessageList** (Message Scroller, MEDIUM→HIGH) — add a `ConversationScrollButton`
  (appears when scrolled away, jumps to latest), a `ConversationEmptyState` slot, and
  export (`messagesToMarkdown` / download). No scroll-to-bottom affordance exists today.
- **AgentMessage** (Message/Bubble) — add a message `Actions` toolbar (copy/regenerate/retry)
  and branch navigation; optionally split `MessageContent`/`MessageAvatar` slots.

---

## 4. MEDIUM / LOW findings

Deduped across all batches. `→` marks a naming difference between the shadcn source and the
library component.

| Component | Finding | Severity |
| --- | --- | --- |
| Accordion | No `data-state=open\|closed` / `data-disabled` / `data-orientation` attrs — consumer CSS/animation hooks won't fire | MEDIUM |
| Accordion | No `orientation="horizontal"` | LOW |
| Alert | No composable `AlertTitle`/`AlertDescription` subcomponents (string params only; rich markup only via ChildContent) | MEDIUM |
| Alert | No dedicated `AlertAction` slot (positioned action button) | LOW-MEDIUM |
| Alert Dialog | No `size` prop on Content (shadcn default\|sm) | LOW |
| Alert Dialog | No `AlertDialogMedia` icon subcomponent | LOW |
| Avatar | No `delayMs` on Fallback — fallback shows immediately, flashes on fast connections | MEDIUM |
| Avatar | No generic composable `AvatarBadge` slot (only the built-in Status dot) | LOW |
| Badge | Missing `ghost` + `link` variants | MEDIUM |
| Badge | No `asChild`/`render` — renders `<div>`, so badge-as-`<a>`/link needs manual wrapping | MEDIUM |
| Badge | `IconContent` uses `me-1` (inline-start only) vs shadcn `data-icon` start+end gap slots | LOW |
| Breadcrumb | `BreadcrumbLink` has no `asChild`/`render` — hardcoded `<a href>`, so a Blazor `NavLink`/router link loses the styled classes | MEDIUM |
| Button Group | No `ButtonGroupSeparator` and no `ButtonGroupText` (inline text/label slot with `asChild`) | MEDIUM |
| Calendar | No `timeZone` prop; no non-Gregorian calendars (Persian/Hijri); no `captionLayout="dropdown"` native month/year selects; no built-in presets slot | LOW-MEDIUM |
| Card | No `CardAction` subcomponent + no `has-data-[slot=card-action]` header grid; no `size="sm"` | MEDIUM |
| Carousel | No `setApi`/`CarouselApi` instance handle; no Embla `opts`/`plugins`; `Loop` is coarse bool (no align/dragFree) | MEDIUM |
| Chart | No composable `ChartConfig`-driven `ChartTooltipContent`/`ChartLegendContent` primitives | MEDIUM |
| Checkbox | `Name` not wired to a hidden `<input>` (not submitted in native form POST); no `Value` prop | MEDIUM |
| Checkbox | No `data-state=checked/indeterminate` (uses `aria-checked`) | LOW |
| Collapsible | No `disabled` prop on Collapsible/Trigger (+`data-disabled`); Trigger is `div[role=button]` not native `<button>`, no `asChild`; no `data-state` | MEDIUM |
| Combobox | No `ComboboxSeparator`, no standalone `ComboboxLabel`, no `ComboboxChips`/`Value` primitives; non-generic (items typed `object`, no `TItem`) | MEDIUM |
| Combobox | No `autoHighlight` — resets `_focusedIndex=-1` on every search, so Enter after typing does nothing until an Arrow press (its own Command re-seeds) | MEDIUM |
| Command | No `CommandDialog` wrapper (hand-compose Dialog+Command); no `CommandShortcut` slot (a `Shortcut` string param renders `<Kbd>`) | MEDIUM |
| Context Menu | `ContextMenuItem` has no `Inset` prop, no `variant="destructive"`; no `ContextMenuShortcut` subcomponent | MEDIUM/LOW |
| Context Menu | No typeahead (type letters to jump) | MEDIUM |
| Context Menu | SubTrigger opens on `ArrowRight` but no `ArrowLeft` to close the submenu / return focus | MEDIUM |
| Date Picker | No natural-language parsing (shadcn chrono-node extra) | LOW |
| Dialog | No non-modal mode (`modal={false}`); no separate `DialogOverlay`/`DialogPortal` composable parts; no `onOpenAutoFocus`/`onCloseAutoFocus` hooks | MEDIUM |
| Direction → DirectionProvider | Doesn't auto-inherit ambient `<html dir>`; prop is enum, not `"ltr"/"rtl"` string | LOW |
| Drawer | No background-scaling (vaul `shouldScaleBackground`); no non-modal mode; no `snapToSequentialPoints`; snap points vertical-only | MEDIUM |
| Drawer | `SetOpen` writes `Open=value` directly (line 44); lacks Dialog's controlled-vs-uncontrolled guard + `DefaultOpen` — a controlled Drawer can revert/reopen on unrelated parent re-render | MEDIUM |
| Dropdown Menu | No `DropdownMenuShortcut`; no `variant="destructive"` item; no `inset`; no `sideOffset`/`alignOffset` | MEDIUM |
| Dropdown Menu | No exit animation (B11 — see Wave 1); no `ArrowLeft`-to-close submenu (verify in SubContent) | MEDIUM |
| Empty → EmptyState | Monolithic vs composable `EmptyHeader`/`EmptyMedia`/`EmptyTitle`/`EmptyDescription`/`EmptyContent`; no `EmptyMedia variant="icon"` bordered-icon container; no multi-action grouping | MEDIUM |
| Hover Card | No Escape-to-dismiss (relies on focusout only) | LOW/MEDIUM |
| Hover Card | No exit animation (B11 — see Wave 1) | MEDIUM |
| Input OTP → OtpInput | No animated `data-active` blinking caret (uses N real inputs vs single hidden input + fake slots) | LOW |
| Kbd | No `KbdGroup` (grouped-keys wrapper with combinator styling) | LOW |
| Menubar | No exit animation (B11 — see Wave 1) | MEDIUM |
| Message → AgentMessage | Monolithic — no `MessageContent`/`MessageAvatar`/`MessageResponse`/`MessageActions`/`Action`/`MessageBranch`/`MessageToolbar`; no action buttons, branch nav, built-in markdown, or split content/avatar slots | MEDIUM |
| Navigation Menu | No controlled `value`/`defaultValue`, no `orientation`, no `delayDuration`/`skipDelayDuration` | MEDIUM |
| Navigation Menu | No hover-close/grace-delay (closes only on click-outside or hovering another trigger) | MEDIUM |
| Navigation Menu | No exit animation (B11 — see Wave 1) | MEDIUM |
| Pagination | `<button>`-only (OnClick/PageChanged) — no link/`href` mode for SEO/SSR-navigable pagination (shadcn `PaginationLink`/`Previous`/`Next` render anchors + `text` prop) | MEDIUM |
| Progress | No composable `ProgressLabel`/`ProgressValue`/`ProgressTrack`/`ProgressIndicator` slots (monolithic) | LOW |
| Progress | No `data-state` (indeterminate/loading/complete) styling hooks | LOW |
| Radio Group | No Home/End jump-to-first/last (parity with Radix roving groups, which also lack it) | LOW |
| Resizable | No handle `disabled`; no per-panel `onCollapse`/`onExpand`; no imperative panel-ref API (`setLayout`/`collapse`/`expand`); no `autoSaveId` (manual `SavedLayout` only) | MEDIUM |
| Resizable | No Shift = larger keyboard step; no Enter/keyboard collapse on the handle | MEDIUM |
| Scroll Area | Native CSS-styled scrollbars, not a draggable-thumb overlay; no `ScrollBar` subcomponent / `orientation` prop; no JS thumb-drag, no corner element; Firefox degrades to a thin persistent bar | MEDIUM |
| Select | Missing `SelectSeparator`; missing `SelectScrollUpButton`/`DownButton`; no trigger `size` (sm/default); no item-aligned position mode | MEDIUM |
| Sheet | `SheetContent` has no default inner scroll container — a long body overflows the viewport unless manually wrapped in `flex-1 overflow-y-auto` | MEDIUM |
| Sidebar | Missing `SidebarGroupAction`, `SidebarGroupContent`, `SidebarMenuAction`, `SidebarMenuBadge`, `SidebarMenuSub`/`SubItem`/`SubButton`, `SidebarMenuSkeleton`, `SidebarRail`, `SidebarInset`, `SidebarInput`; no public `useSidebar`-style context (`isMobile`/`openMobile`/`setOpenMobile`); no `inset`/`floating` variants | MEDIUM |
| Slider | Max 2 thumbs (`IsRange`) vs arbitrary-N array; no `inverted` prop; no `value`/`defaultValue` array API | MEDIUM |
| Slider | No `data-state`/`data-orientation` (aria only) | LOW |
| Sonner → Toast | No `cancel` button (single `ActionLabel` only); no `loading` helper (Promise uses `Duration=0`); no `richColors`, no global `closeButton`, no `unstyled`, no per-toast icon override | MEDIUM |
| Sonner → Toast | No collapsed-stack + hover-expand (renders flat `flex-col gap-2`); no keyboard focus-hotkey (sonner F6) | MEDIUM |
| Spinner | Fixed Sm/Md/Lg (override via `Class`) vs continuous `size-*`; no `data-icon="inline-start/end"` button convention | LOW |
| Switch | No `data-state=checked/unchecked` (aria-checked only) | LOW |
| Tabs | No uncontrolled `DefaultValue` (`ActiveValue` doubles as initial) | LOW |
| Toggle | No `data-state="on\|off"` — consumers targeting `[data-state=on]` get nothing (visuals driven from C#) | MEDIUM |
| Toggle | No uncontrolled `DefaultPressed` | LOW |
| Toggle Group | No `Orientation` param and no `aria-orientation` on the group | MEDIUM |
| Toggle Group | Missing `data-state=on/off` (same as Toggle) | MEDIUM |
| Toggle Group | No group-level `Disabled`; no `Loop` toggle (always loops); item can't override variant/size (context-only) | LOW |
| Tooltip | No `TooltipProvider` ⇒ no `skipDelayDuration` group behavior (crossing adjacent tooltips re-incurs full delay) | MEDIUM |
| Tooltip | No controlled `Open`/`DefaultOpen`/`OnOpenChange` | MEDIUM |
| Tooltip | No exit animation + no `data-state=open/closed` (B11 — see Wave 1) | MEDIUM |
| Typography (Text/Heading/Link/Code) | No `Blockquote` primitive; no styled List/Table presets; no named `lead`/`muted`/`large`/`small` shorthands (set via props) | LOW |
| Typography | `LineClamp` capped 1–3 (prebuilt CSS) | LOW |
| Bubble → AgentMessage | No branch/sources affordance | LOW |

*No user-visible gaps found for:* Aspect Ratio, Data Table / DataGrid, Input, Label,
Separator, Skeleton, Table, Textarea — all at or beyond shadcn parity.

---

## 5. Where the library is ahead

The honest wins — behaviors and APIs that already exceed shadcn/ui. (Feeds marketing.)

**Overlays & motion robustness**
- Symmetric, Server-interop-safe **exit animations** driven by real `animationend`
  (`IOverlayExitCallback`) on Dialog, Drawer, Sheet, AlertDialog, and Toast — the panel stays
  mounted through the slide/zoom-out. shadcn on Blazor Server routinely drops these.
- Cancelable **`OnBeforeClose(reason)` veto** across overlays (reason: escape/outside/swipe/
  close) — richer than Radix `onInteractOutside`.
- Extras with no shadcn equivalent: `PreventClose`, `Size`, `Scrollable`, per-overlay
  `ZIndex` stacking for nested overlays, `SwipeToClose` gestures, controlled/uncontrolled
  seed hardening.

**Data & visualization**
- **DataGrid**: sort, filter, pagination, column chooser, reorder, pin, group, tree,
  inline + batch edit, export, server-mode, virtualization, layout persistence, fullscreen —
  shadcn ships only a TanStack *guide*, not a component.
- **Chart**: ECharts with 30+ chart types (bar/line/area/pie/radar/radial + sankey/treemap/
  gauge/candlestick/heatmap/geo…) vs shadcn's 6 Recharts types.

**Dates & calendars**
- **Calendar**: full WAI-ARIA grid roving-tabindex keyboard nav (arrows/Home/End/PageUp-Down,
  disabled-skip), range/multiple/single modes, week numbers, swipe, per-date tooltip/badge,
  culture-aware `FirstDayOfWeek`.
- **DatePicker**: single/range/**month/year/multiple** modes, **Wheel** (iOS) variant,
  presets sidebar, inline, typeable input with parse+revert + `OnParseError`, min/max +
  `IsDateDisabled` enforced on the typed path, per-date Class/Tooltip/Badge.

**Rich form controls (supersets of shadcn primitives)**
- **Select** — Searchable/Multiple/Clearable/Creatable, virtualization, data-bound `Items`
  API, groups/descriptions/icons, loading, `MaxDisplayTags`, FormField/error.
- **Combobox** — Creatable, debounced async `OnSearchAsync`, Virtualize, multi-select chips
  with Backspace-remove, `ItemIcon`/`ItemDescription`, controlled-Open cascade guard.
- **Input / Textarea** — prefix/suffix, Clearable with focus-restore, char counter with soft
  limit, Size/Density, AutoResize + MaxRows, FormField id/aria wiring.
- **Tabs** — AnimatedIndicator, `RenderMode` (Active/Lazy/Eager state-preservation), closable
  tabs with WCAG 2.4.3 focus-move, reorderable + persistable layout, scrollable auto-hiding arrows.

**Accessibility & correctness**
- Focus **save/restore to the trigger** (WCAG 2.4.3) across all menu systems.
- `role=status`/`aria-busy` on Skeleton & Spinner; `role=log` + `aria-live=polite` on
  AgentMessageList; per-digit `aria-label` on OTP; Status-dot a11y label on Avatar.
- Timezone- and culture-correct `<time datetime>` timestamps on AgentMessage (bug #303).
- **Live-DOM-order reprojection** for keyed reorders (Command's identity-anchored highlight,
  ToggleGroup/RadioGroup/Tabs roving nav) — survives list reorders where naive clones break.
- Controlled/uncontrolled **veto/echo disambiguation** across Switch/Checkbox/Toggle.
- Invariant-culture numeric formatting (AspectRatio comma-locale fix, Slider, Pagination).
- Pervasive **RTL**: separator chevron rotate, off-canvas translate, vertical writing-mode
  slider, arrow-key axis swap. `ScrollArea` `FocusRingGutter` (net-zero gutter preventing
  focus-ring clipping — a real fix shadcn lacks).

**Breadth**
- **103 library-only components** shadcn has no answer for — Motion (AnimatedBeam, BorderBeam,
  Marquee, Confetti, NumberTicker, Dock…), Dashboard/Data (Bento, KpiCard, Sparkline, Gauge,
  Gantt, Scheduler, PivotGrid, TreeView, Timeline, Stepper…), AI (PromptInput, ReasoningDisplay,
  StreamingText, ToolCallCard), Forms extras (NumberInput, TagInput, Cascader, ColorPicker,
  SignaturePad, RichTextEditor, CodeEditor, QueryBuilder…), Layout (Flex/Grid/Stack/Center/
  Container), Marketing (Hero, CTASection, FeatureGrid), Drag&Drop (Kanban, Sortable, Transfer,
  PickList), plus Barcode, QRCode, Map, PdfViewer, AudioPlayer, ThemeToggle, and more.
