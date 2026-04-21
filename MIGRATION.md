# Migrating to Lumeo 2.0

## Overview

Lumeo 2.0 is additive for the vast majority of users — most apps upgrade with a package bump and no code changes. The breaking surface is intentionally small:

- The `[Obsolete]` `Icon` / `Label` RenderFragment slot aliases deprecated in 1.6.0 are now **removed** (5 min rename).
- DataGrid's "Export Excel" is now a **real `.xlsx`** (ClosedXML) instead of a CSV with the wrong extension.
- Date / number components now honour `CultureInfo.CurrentCulture` by default — pass `Culture="@CultureInfo.InvariantCulture"` if you relied on invariant formatting.
- BarChart shows every category label by default and auto-rotates.

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

### `@lumeo/mcp-server` — MCP server for LLM codegen

```bash
npm install -g @lumeo/mcp-server
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
