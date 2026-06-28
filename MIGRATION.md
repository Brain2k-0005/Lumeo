# Migrating to Lumeo 4.0

## Overview

Lumeo 4.0 has **no API-signature breaks** — no renamed types, parameters or enums. It is a large additive + bug-fix release (a Radix/shadcn parity audit plus a library-wide correctness "battle-test" of all 164 components, ~355 fixes). The major bump is for the **scope** and a small set of **behaviour** changes below. Most projects upgrade by bumping the version and rebuilding; only review the items here if your code depended on the old (buggy) behaviour.

> **3.x → 4.0 is a recompile-and-run upgrade for almost everyone.** The behaviour changes below are correctness fixes; they only require action if you relied on the previous behaviour.

### Behaviour changes to review

1. **Badge — removable badges are now controlled (no auto-hide).** Previously, clicking a removable badge's × optimistically hid the badge itself. It no longer does — visibility is data-driven, like the rest of the library. **Action:** in your `OnRemove` handler, remove the item from your own collection so the badge disappears, and `@key` the rendered list:
   ```razor
   @foreach (var tag in _tags)
   {
       <Badge Removable OnRemove="@(() => _tags.Remove(tag))" @key="tag">@tag</Badge>
   }
   ```
2. **Internal state now survives unrelated re-renders.** Selection / checked / expand-collapse / active-index / page / search / scroll / in-progress-edit state across the component library no longer **resets** when the parent re-renders for an unrelated reason, or when bound `Items`/`Value` are re-supplied with the same content (e.g. an async reload). This is the headline fix of the release — but if any code *relied* on that state being wiped by an unrelated re-render, drive the reset explicitly instead (e.g. change the bound `Value`/selection, or `@key` the component to force a remount).
3. **Progress / Gauge / RingProgress clamp out-of-range values.** A `Value` outside `[0, Max]` is now clamped (`150/100` → `100`, negative → `0`) and the indeterminate state reports `aria-busy` + omits `aria-valuenow`. If you passed out-of-range values intentionally, clamp on your side or widen `Max`.
4. **DataTable selection across an `Items` reload.** Row selection is still by reference by default. If your `TItem` is a plain class (not a record) and your reload re-supplies value-equal but reference-distinct rows, supply the new opt-in `ItemKey` so selection survives the reload (otherwise selection clears, as before — no behaviour change unless you adopt `ItemKey`).
5. **Roving keyboard order follows live DOM order.** RadioGroup / ToggleGroup / Segmented / Stepper / Splitter / Steps / Accordion now resolve arrow-key navigation, numbering and neighbour lookup from the **live DOM order** after a keyed reorder, instead of the original mount order. **Action:** none unless you keyed-reorder one of these and relied on arrow-key traversal following mount order — render the items in the order you want them traversed.
6. **KanbanCard `Index` defaults to `-1` (was `0`).** Omitting `Index` previously made every card report position `0`, so programmatic drops always targeted the first slot. The default is now `-1` ("unpositioned"). **Action:** none unless you omitted `Index` and depended on the old always-zero behaviour — pass the real list index (you almost certainly already do).

> **Theme tokens are now OKLCH.** All 878 colour tokens (base + 8 themes) moved from HSL to OKLCH — an exact 1:1 conversion, so the rendered palette and your brand identity are unchanged. No action needed unless you read or parse the raw CSS custom-property values (`var(--color-…)`), which now hold OKLCH strings instead of HSL.

---

# Migrating to Lumeo 3.0

## Overview

Lumeo 3.0 is a **quality release with one breaking change**: per-component size, side, alignment, and orientation enums collapse into four unified public enums under the root `Lumeo` namespace. Everything else — including dozens of new features — is additive.

> **3.0.x and 3.1.x patch / minor releases require no migration.** 3.0.1 through 3.0.5 and 3.1.0 are drop-in upgrades from 3.0.0 — bump the version, rebuild, done. The new 3.1.0 components (AudioPlayer + SignaturePad in core; PdfViewer / Maps / CodeEditor as new satellite packages) are purely additive; the satellite packages each require their own explicit `PackageReference` if you want to use them. The enum unification below applies once when moving from 2.x → 3.0.0, and stays put for every subsequent release.

- **Breaking**: 39 per-component enums (e.g. `Button.ButtonSize`, `Popover.PopoverSide`, `Tabs.TabsOrientation`) replaced by `Lumeo.Size` / `Lumeo.Side` / `Lumeo.Align` / `Lumeo.Orientation`. See "Enum unification" below — most projects can migrate with a single project-wide find/replace.
- **New features (additive)**: `OnBeforeClose` dismiss intercept on overlays; nested overlay z-index stacking; DatePicker keyboard input; DateTimePicker time-zone support; `Form.ResetValues()`; async per-field validators with pending state; menu submenus (`DropdownMenuSub` / `ContextMenuSub` / `MenubarSub`); `TabsList` drag-to-reorder; Toast pause-on-hover; Tooltip collision-flip; ARIA live error announcements across form controls.
- **Internal**: components now inject `IComponentInteropService` (the interface) instead of the concrete class — drop-in for consumers, mockable for tests. Toolbar now goes through the same interop layer.

The Lumeo 2.0 migration guide remains below for projects upgrading directly from 1.x.

## Enum unification (the only breaking change)

Lumeo 3.0 deletes 39 per-component enums and routes every size / side / align / orientation parameter through four shared enums:

| New enum            | Values                                | Replaces                                                                                                                                                                                                                                                                                                                       |
|---------------------|---------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Lumeo.Size`        | `Xs, Sm, Md, Lg, Xl, Xxl`             | `AlertSize`, `AvatarSize`, `ChipSize`, `FileUploadSize`, `IconSize`, `InputSize`, `KbdSize`, `ListSize`, `RatingSize`, `ReasoningSize`, `ResultSize`, `SparkCardSize`, `SpinnerSize`, `StatisticSize`, `SwitchSize`, `ToggleSize`, `ToggleGroupSize`                                                                              |
| `Lumeo.Side`        | `Top, Right, Bottom, Left`            | `DrawerSide`, `DropdownMenuSide`, `HoverCardSide`, `PopoverSide`, `SheetSide`, `SidebarSide`, `TooltipSide`, `TourPlacement`                                                                                                                                                                                                    |
| `Lumeo.Align`       | `Start, Center, End`                  | `DropdownMenuAlign`, `HoverCardAlign`, `PopoverAlign`                                                                                                                                                                                                                                                                          |
| `Lumeo.Orientation` | `Horizontal, Vertical`                | `ButtonGroupOrientation`, `CarouselOrientation`, `FormFieldOrientation`, `ImageCompareOrientation`, `MegaMenuOrientation`, `ResizableDirection`, `SeparatorOrientation`, `SplitterOrientation`, `StackDirection`, `StepperOrientation`, `StepsOrientation`, `TabsOrientation`, `TimelineOrientation`                            |

### Recipe

`@using Lumeo` is already in the root `_Imports.razor` so the new enums resolve unqualified inside `.razor` files. For `.cs` files, add `using Lumeo;` if not already there.

Then a per-component find/replace across your project:

```text
ButtonSize.Sm         →  Size.Sm
AvatarSize.Md         →  Size.Md
PopoverSide.Bottom    →  Side.Bottom
PopoverAlign.Start    →  Align.Start
TabsOrientation.Vertical → Orientation.Vertical
```

…and so on for every enum in the table above. The value names (`Sm`, `Md`, `Lg`, `Bottom`, `Start`, `Vertical`, …) line up across the rename — only the type prefix changes.

### Intentional exceptions (NOT unified)

Some enums stayed component-specific because their value set doesn't fit the union:

- `Button.ButtonSize` — keeps an `Icon` value for icon-only buttons.
- `DialogContent.DialogSize` and `SheetContent.SheetSize` — keep a `Full` value for fullscreen variants.
- `ToastPosition`, `SpeedDialPosition`, `SpeedDialDirection`, `LayoutDirection`, `StepsDirection`, `KpiDeltaDirection`, `DeltaDirection` — domain-specific layout enums.
- Every `*Variant` enum (`ButtonVariant`, `BadgeVariant`, `AlertVariant`, etc.) — variants are inherently per-component.

### Why this is the only break

Blazor `[Parameter]` types are statically typed. The only way to make the consolidation backward-compatible without doubling the API surface is to ship duplicate parameters (`Size` + `UnifiedSize`), which is its own usability problem. We took the one-shot rename instead. A find/replace on the 39 enum names listed above is a < 5-minute migration for most projects.

## New features (no migration needed)

These are additive — your existing code keeps working, and you opt in to the new capabilities only where you want them.

### Overlay dismiss intercept (`OnBeforeClose`)

`Dialog`, `Sheet`, `Drawer`, and `AlertDialog` now expose an `OnBeforeClose` `EventCallback<DismissEventArgs>` that fires for every dismiss path (Escape, click-outside, swipe, close button, AlertDialog Cancel/Action). Set `args.Cancel = true` to veto the dismiss — typical pattern for "unsaved changes" guards.

```razor
<Dialog @bind-Open="_open" OnBeforeClose="ConfirmDiscard">
    <DialogContent>…</DialogContent>
</Dialog>

@code {
    private Task ConfirmDiscard(DismissEventArgs e)
    {
        if (e.Reason is "escape" or "outside" && _hasUnsavedChanges)
        {
            e.Cancel = true;
        }
        return Task.CompletedTask;
    }
}
```

`e.Reason` is one of: `escape`, `outside`, `swipe`, `close`, `action`, `cancel`.

### Nested overlay z-index stacking

A Dialog opened from inside another Dialog (or a Sheet from a Dialog, etc.) now layers correctly above its parent — the `OverlayService` allocates a monotonic z-index per open instance instead of every overlay sharing `z-50`. No code change required.

### DatePicker keyboard input

`DatePicker` accepts typed input by default. Type a date in the configured `Format` and confirm with Enter or blur — invalid input reverts to the last value.

```razor
<DatePicker @bind-Value="_date"
            Format="MM/dd/yyyy"
            AllowKeyboardInput="true"
            OnParseError="msg => _error = msg" />
```

Set `AllowKeyboardInput="false"` to restore the old calendar-only behaviour.

### DateTimePicker time zone

```razor
<DateTimePicker @bind-OffsetValue="_when"
                TimeZone="@TimeZoneInfo.FindSystemTimeZoneById(\"Europe/Berlin\")"
                ShowTimeZoneLabel="true" />
```

The bound `DateTimeOffset` carries the zone's matching offset (DST-aware); the picker UI shows a "(UTC+1)" label next to the value.

### Form: `ResetValues()` and async validators

`Form.ResetValues()` (new) restores the model to its initial snapshot (taken on `OnInitializedAsync`). The existing `Reset()` (clears errors only) is unchanged. `ResetValues()` requires the model to be JSON-round-trippable.

`FormField` gained per-field async validation:

```razor
<FormField For="@(() => Model.Username)"
           AsyncValidator="@CheckUsernameTaken"
           AsyncValidationDebounceMs="300">
    <Input @bind-Value="Model.Username" />
</FormField>

@code {
    async Task<string?> CheckUsernameTaken(object? value)
    {
        var username = value as string;
        if (string.IsNullOrEmpty(username)) return null;
        var taken = await _api.IsUsernameTakenAsync(username);
        return taken ? "Username already taken" : null;
    }
}
```

A spinner appears next to the label while validation is pending. `FormContext.IsAnyFieldValidating` lets you disable the submit button while any field is checking.

### Menu submenus

`DropdownMenu`, `ContextMenu`, and `Menubar` gained `*Sub` / `*SubTrigger` / `*SubContent` triplets:

```razor
<DropdownMenu>
    <DropdownMenuTrigger>…</DropdownMenuTrigger>
    <DropdownMenuContent>
        <DropdownMenuItem>Profile</DropdownMenuItem>
        <DropdownMenuSub>
            <DropdownMenuSubTrigger>Settings</DropdownMenuSubTrigger>
            <DropdownMenuSubContent>
                <DropdownMenuItem>Account</DropdownMenuItem>
                <DropdownMenuItem>Billing</DropdownMenuItem>
            </DropdownMenuSubContent>
        </DropdownMenuSub>
    </DropdownMenuContent>
</DropdownMenu>
```

Hover or ArrowRight opens the submenu; ArrowLeft / cursor-leave closes it; Escape closes the whole tree. Auto-flips to the left edge when the right edge would clip. Nesting works recursively.

### TabsList drag-to-reorder (opt-in)

```razor
<TabsList Reorderable="true" OnReorder="HandleReorder">
    @foreach (var tab in _tabs)
    {
        <TabsTrigger Value="@tab.Id">@tab.Title</TabsTrigger>
    }
</TabsList>

@code {
    void HandleReorder(TabsReorderEventArgs e)
    {
        var item = _tabs[e.FromIndex];
        _tabs.RemoveAt(e.FromIndex);
        _tabs.Insert(e.ToIndex, item);
    }
}
```

The library doesn't mutate the collection — your handler does, and the next render reflects the new order. Works with touch (via the existing sortable JS interop) and keyboard.

### Toast pause-on-hover

Toasts pause their auto-dismiss timer while the cursor is over them or any element inside has focus. Mouse-leave / focus-out resumes the remaining time. Variant-aware ARIA: `Destructive` toasts get `role="alert"` + `aria-live="assertive"`, others get `role="status"` + `aria-live="polite"`.

### Tooltip collision flip

`TooltipContent` now uses fixed-position with viewport collision detection (same engine as Popover). Tooltips near a viewport edge auto-flip to the opposite side instead of clipping.

### ARIA live error regions

Error `<p>` elements on `FormField`, `FormMessage`, `Input`, `Textarea`, `Select`, `NumberInput`, `PasswordInput`, `Checkbox`, `Slider`, and `Switch` now carry `role="alert"` + `aria-live="polite"`, so screen readers announce validation errors when they appear. Helper text is untouched.

## Internal changes (no consumer impact)

- All components now inject `IComponentInteropService` (the interface) instead of the concrete `ComponentInteropService`. Drop-in for normal consumers; lets test projects substitute a mock.
- `Toolbar` no longer holds its own `IJSRuntime` — it routes through the shared interop service like every other component.
- Internal `Lumeo.Internal.LumeoIds`, `Lumeo.Internal.Cx`, and `Lumeo.Internal.DebouncedValue<T>` helpers consolidate the ID-generation, class-composition, and debounce patterns scattered across components. Not part of the public API.

---

# Migrating to Lumeo 2.0

## Overview

Lumeo 2.0 is additive for the vast majority of users — most apps upgrade with a package bump and no code changes. The breaking surface is intentionally small:

- **The `Lumeo` package was split into `Lumeo` (core) + 5 satellite packages.** If you use Chart, DataGrid, DataTable, Filter, RichTextEditor, Scheduler, or Gantt, you now also need to install the corresponding satellite package. See "Package split" below.
- **Overlay components renamed `IsOpen` → `Open`.** Existing `IsOpen` / `IsOpenChanged` continue to work via `[Obsolete]` aliases; consider migrating at your leisure.
- The `[Obsolete]` `Icon` / `Label` RenderFragment slot aliases deprecated in 1.6.0 are now **removed** (5 min rename).
- DataGrid's "Export Excel" is now a **real `.xlsx`** (ClosedXML) instead of a CSV with the wrong extension.
- Date / number components now honour `CultureInfo.CurrentCulture` by default — pass `Culture="@CultureInfo.InvariantCulture"` if you relied on invariant formatting.
- BarChart shows every category label by default and auto-rotates.

## Package split

Lumeo 2.0 follows the DevExpress / Telerik / Microsoft.Extensions model: a small core package plus opt-in satellites for heavy components. The split keeps the core package lean (~568 KB instead of ~918 KB) and means consumers only pay for what they use.

| Component | Now ships in |
|-----------|--------------|
| Chart (and all 30+ chart subtypes) | `Lumeo.Charts` |
| DataGrid, DataTable, Filter | `Lumeo.DataGrid` |
| RichTextEditor | `Lumeo.Editor` |
| Scheduler | `Lumeo.Scheduler` |
| Gantt | `Lumeo.Gantt` |
| All other ~110 components | `Lumeo` (core) |

**To migrate:** add a `<PackageReference>` to each satellite whose components you use. All packages share one version (lockstep), so always upgrade them together.

```xml
<ItemGroup>
  <PackageReference Include="Lumeo" Version="2.0.0" />
  <PackageReference Include="Lumeo.Charts" Version="2.0.0" />     <!-- if using Chart -->
  <PackageReference Include="Lumeo.DataGrid" Version="2.0.0" />   <!-- if using DataGrid/DataTable/Filter -->
  <!-- etc. -->
</ItemGroup>
```

`@using Lumeo` already covers the satellite components — no extra `@using` directives are needed. The `lumeo add <component>` CLI also detects which satellite a component belongs to and prompts you to install the package.

`lumeo-utilities.css` ships unchanged in the `Lumeo` core package as `_content/Lumeo/css/lumeo-utilities.css`. The bundle now also covers every utility class used inside the satellites (DataGrid, Charts, Editor, Scheduler, Gantt, Motion), so installing a satellite alongside the core package is enough — you don't need a separate stylesheet per satellite.

> **rc.15–rc.17 note:** those three pre-releases briefly stopped shipping `lumeo-utilities.css` while a registry CDN was being planned. The CDN never materialized and the change broke drop-in setup for everyone, so rc.18 restores the file. If you pinned to rc.15/16/17 and worked around the missing file by vendoring an older copy or running your own Tailwind build, you can revert that workaround once you're on rc.18+.

## Overlay component rename: `IsOpen` → `Open`

15 overlay components (Dialog, Drawer, DropdownMenu, AlertDialog, Sheet, Popover, ContextMenu, Combobox, Select, ColorPicker, HoverCard, Tour, Collapsible, NavigationMenu*) now expose `Open` / `OpenChanged` as the canonical parameters, matching shadcn/ui and ReUI conventions.

The previous `IsOpen` / `IsOpenChanged` parameters remain as `[Obsolete]` aliases that mirror the new properties — your existing code keeps working but emits a build-time warning. Migrate when convenient:

```razor
<!-- Old (still works, with deprecation warning) -->
<Dialog @bind-IsOpen="_open">…</Dialog>

<!-- New -->
<Dialog @bind-Open="_open">…</Dialog>
```

The aliases will be removed in a future major release.

Services (`ToastService`, `OverlayService`, `ThemeService`, `KeyboardShortcutService`, `ComponentInteropService`, `IDataGridExportService`), theming, CSS variables, and routes are **unchanged**.

## Breaking changes

### 1. Icon / Label RenderFragment slots removed

**Affected components**: `Alert`, `Badge`, `EmptyState`, `Rating`, `Result`, `Segmented`, `SidebarMenuButton`, `StepsItem`, `TabsTrigger`, `TimelineItem`.

**Old (v1.x):**

```razor
<Alert>
    <Icon><Blazicon Svg="Lucide.Info" /></Icon>
    <Title>Heads up</Title>
</Alert>

<SidebarMenuButton>
    <Label>Home</Label>
</SidebarMenuButton>
```

**New (v2.0):**

```razor
<Alert>
    <IconContent><Blazicon Svg="Lucide.Info" /></IconContent>
    <Title>Heads up</Title>
</Alert>

<SidebarMenuButton>
    <LabelContent>Home</LabelContent>
</SidebarMenuButton>
```

**Why**: the old slot names shadowed the `<Icon>` and `<Label>` Lumeo components of the same name, so `<Icon Name="info" />` inside an `Alert` would bind to the slot instead of rendering the component. The `IconContent` / `LabelContent` aliases shipped in 1.6.0 alongside the `[Obsolete]` ones — 2.0 just removes the obsolete aliases.

**Recipe**: project-wide regex search-and-replace scoped to the affected components:

- `<Icon>` ... `</Icon>` → `<IconContent>` ... `</IconContent>`
- `<Label>` ... `</Label>` → `<LabelContent>` ... `</LabelContent>`

Do **not** blindly rename every `<Icon>` / `<Label>` in your codebase — those are real standalone Lumeo components outside of the 10 listed parents.

### 2. DataGrid Excel export is a real .xlsx

**Before (v1.x)**: clicking "Export Excel" produced a CSV with an `.xlsx` extension via the static `DataGridExportService`. Excel would open it with a format warning.

**After (v2.0)**: real `.xlsx` generated via ClosedXML, downloaded as `export.xlsx` with the correct MIME type (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`). PDF export goes through QuestPDF, CSV is unchanged.

**Impact**: if you previously intercepted the "excel" case and routed it to your own handler because of the bug — you can remove that workaround now. For everyone else, this is automatic and requires no code change.

### 3. `Culture` cascades through DataGrid + date/number components

**New**: a `Culture` parameter on `DataGrid`, `DatePicker`, `DateTimePicker`, `NumberInput`, `Slider`, and `Statistic`. Default `null` falls back to `CultureInfo.CurrentCulture`.

**Impact**: if you previously relied on invariant formatting regardless of the user's culture, pass it explicitly:

```razor
<DatePicker @bind-Value="date" Culture="@CultureInfo.InvariantCulture" />
<NumberInput @bind-Value="amount" Culture="@CultureInfo.InvariantCulture" />
```

Or set it once per page via a cascading value. Most apps want the new default — users in `de-DE` now see dates and decimals formatted the way they expect.

### 4. BarChart smart labels by default

`BarChart` now has `LabelStrategy` defaulting to `ChartLabelStrategy.Smart` — it shows every category label and auto-rotates (-60°, -75°) at higher densities.

**Before**: ECharts' default auto-thinning hid every second (or third…) label on busy charts.

**After (v2.0)**: every label is rendered and rotated as needed.

**Impact**: charts with 10+ categories now have visible, rotated X-axis labels. To restore the previous behaviour:

```razor
<BarChart LabelStrategy="ChartLabelStrategy.Auto" ... />
```

Options: `Smart` (default, show all + auto-rotate), `ShowAll` (show all, never rotate), `Auto` (ECharts default thinning).

### 5. Toolbar visibility defaults (not breaking — listed for completeness)

New `ShowSearch`, `ShowColumnChooser`, `ShowExport` booleans on `DataGrid` default to `true`. Behaviour is unchanged from v1 — listed here so you know the knobs exist if you want to hide them.

### 6. `IComponentInteropService.RegisterCarouselSwipe` signature (only affects custom interop implementations)

During the mobile sprint the Carousel scroll-sync callback gained the current snapped item index so `_currentIndex` stays correct after a native touch swipe (previously a touch flick could desync the dot indicator).

**Before**: `RegisterCarouselSwipe(string elementId, string orientation, Func<string, Task> swipeHandler, Func<double, double, Task> scrollHandler)`

**After (2.0)**: the `scrollHandler` is `Func<double, double, int, Task>` — the third argument is the nearest snapped child index.

**Impact**: **none for normal consumers.** This only matters if you implemented `IComponentInteropService` yourself (e.g. a custom JS-interop layer or a test double). The built-in `ComponentInteropService` and `Carousel` are already updated. If you have a custom implementation, add the `int` parameter to your `scrollHandler` delegate.

## New companion packages

Lumeo 2.0 ships with three optional companion packages. You can ignore them if you only consume `Lumeo` as a NuGet package — none of them is required for the core library.

### `Lumeo.Cli` — shadcn-style vendoring

```bash
dotnet tool install -g Lumeo.Cli

lumeo init                   # writes lumeo.config.json
lumeo add button             # copy Button source into your repo
lumeo list                   # list all registry entries
lumeo diff button            # diff vendored copy vs registry
```

### `Lumeo.Templates` — `dotnet new` scaffolders

```bash
dotnet new install Lumeo.Templates

dotnet new lumeo-page       -n SettingsPage
dotnet new lumeo-form       -n RegisterForm
dotnet new lumeo-component  -n FancyCard
```

### `@lumeo-ui/mcp-server` — MCP server for LLM codegen

```bash
npm install -g @lumeo-ui/mcp-server
# then wire into Claude Desktop / Cursor / your MCP client config
```

## Not breaking

The following are **unchanged** from 1.x and require no migration work:

- **Services**: `ToastService`, `OverlayService`, `ThemeService`, `KeyboardShortcutService`, `ComponentInteropService`, `IDataGridExportService` — API stable.
- **Theming**: all CSS variables, all 8 theme files, and dark-mode class toggling behave identically.
- **Routes / URLs**: the docs site URLs and all component routes are unchanged.
- **`AddLumeo()`** registration — same call, same options surface.
- **Tailwind integration** — `lumeo.css` + `lumeo-utilities.css` drop-in usage is identical.

If you hit something that looks breaking but isn't covered here, open an issue — we want 2.0 to be a boring upgrade for existing users.
