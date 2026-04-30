# Lumeo

**130+ accessible Blazor components, AI-ready, motion-integrated, shadcn-inspired.**

130+ components · 1,727 tests · 14 locales · MIT · .NET 10

[![NuGet](https://img.shields.io/nuget/v/Lumeo?logo=nuget&label=Lumeo)](https://www.nuget.org/packages/Lumeo)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Lumeo?logo=nuget&label=downloads)](https://www.nuget.org/packages/Lumeo)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](./LICENSE)
[![Live Demo](https://img.shields.io/badge/demo-lumeo.nativ.sh-black?logo=cloudflare)](https://lumeo.nativ.sh)
[![GitHub stars](https://img.shields.io/github/stars/Brain2k-0005/Lumeo?style=flat&logo=github)](https://github.com/Brain2k-0005/Lumeo/stargazers)
[![Sponsor](https://img.shields.io/github/sponsors/Brain2k-0005?logo=github-sponsors&color=ea4aaa)](https://github.com/sponsors/Brain2k-0005)

> **v2.0 is currently `2.0.0-rc.10`.** The API is stable; we're gathering field feedback for a short window before tagging `2.0.0` final. See [`MIGRATION.md`](./MIGRATION.md) for upgrade notes from 1.x.

## What's new in 2.0

- **AI primitives** — `PromptInput`, `StreamingText`, `AgentMessageList`, `ToolCallCard`, `ReasoningDisplay`. SignalR-native token streaming, sticky auto-scroll, collapsible chain-of-thought.
- **Motion primitives** — `Marquee`, `NumberTicker`, `TextReveal`, `BlurFade`, `BorderBeam`, `ShimmerButton`, `Sparkles`. Opt-in `Animated` props on Steps, Timeline, Progress, Tabs, Switch, Checkbox, Badge, BottomNav.
- **Scheduler + Gantt + RichTextEditor** — FullCalendar v6, Frappe Gantt, and TipTap v2 wrappers. Lazy-loaded JS so you pay only for what you use.
- **Dashboard tiles + Bento** — `Bento`, `BentoTile`, `KpiCard`, `SparkCard`, `Delta`, and a standalone `Sparkline` primitive.
- **Real Excel export** — `IDataGridExportService` emits real `.xlsx` via ClosedXML, PDF via QuestPDF, or CSV — with a browser download helper.
- **14 locales + RTL** — `ILumeoLocalizer` ships EN, DE, ES, FR, IT, PT, NL, PL, JA, ZH-CN, KO, AR, RU, TR. `IThemeService.SetDirectionAsync()` flips the whole library to RTL.
- **`[LumeoForm]` source generator** — annotate a POCO and get a compile-time `RenderForm(model, onSubmit)` method that emits a fully-bound, validated Lumeo Form.
- **DataGrid upgrades** — expandable fullscreen, header drag-to-reorder, layout JSON round-trip, `ShowSearch` / `ShowColumnChooser` / `ShowExport` toggles, `Culture` parameter, `SetHtmlClass` for fullscreen navbar hiding.
- **Companion packages** — see [Companion packages](#companion-packages) below.

## Feature overview

- **130+ components** — accessible UI primitives, Blazor WASM & Server
- **AI primitives** — `PromptInput`, `StreamingText`, `AgentMessageList`, `ToolCallCard`, `ReasoningDisplay`
- **Motion primitives** — `Marquee`, `NumberTicker`, `TextReveal`, `BlurFade`, `BorderBeam`, `ShimmerButton`, `Sparkles`, `Sparkline`
- **Dashboard tiles** — `KpiCard`, `SparkCard`, `Delta`, `Bento`, `BentoTile`, `PickList<T>`
- **Scheduler + Gantt + RichTextEditor** — FullCalendar + Frappe Gantt + TipTap wrappers, lazy-loaded
- **14 locales + RTL** — EN/DE/ES/FR/IT/PT/NL/PL/JA/ZH-CN/KO/AR/RU/TR via `ILumeoLocalizer`; `IThemeService.SetDirectionAsync()` for RTL
- **Excel / PDF / CSV export** — `IDataGridExportService` (ClosedXML + QuestPDF)
- **`[LumeoForm]` source generator** — annotate a POCO, get a fully-bound Form for free
- **Culture-aware** — `Culture` on DataGrid, DatePicker, DateTimePicker, NumberInput, Slider, Statistic
- **BottomNav + Splitter** — mobile tab bar (safe-area, optional FAB) and resizable multi-pane layouts
- **Block templates** — SignIn, SignUp, ResetPassword, OtpVerify, Pricing, Hero, Dashboard, Settings
- **8 color themes + dark mode** — Zinc, Blue, Green, Rose, Orange, Violet, Amber, Teal; class-based dark mode
- **Tailwind CSS v4** — CSS variable architecture, zero hardcoded colors
- **Programmatic OverlayService** — open dialogs, sheets, drawers, toasts from code
- **30+ chart types** — ECharts-backed; BarChart `LabelStrategy="Smart"` auto-rotates dense labels
- **DataGrid** — sort, filter, inline edit, column pin, row group, drag-to-reorder, fullscreen, layout JSON, Excel/PDF/CSV export
- **Form validation** — DataAnnotations + custom validators with styled error states
- **Accessible** — ARIA roles, keyboard navigation, focus trapping, screen-reader support
- **1,727 bUnit tests** — CI-enforced on every PR

## Component Categories

| Category | Components |
|----------|------------|
| **Layout** | Stack, Flex, Grid, Container, Center, Spacer, AspectRatio, Resizable, ScrollArea, Separator, Splitter |
| **Typography** | Text, Heading, Link, Code |
| **Forms** | Input, Select, Combobox, DatePicker, DateRangePicker, DateTimePicker, TimePicker, NumberInput, PasswordInput, InputMask, Checkbox, Switch, RadioGroup, Slider, Toggle, ToggleGroup, FileUpload, OtpInput, TagInput, ColorPicker, Textarea, Form, Mention, Cascader, PickList, RichTextEditor |
| **Data Display** | Table, DataTable, DataGrid, Card, Badge, Chip, Avatar, Calendar, Scheduler, Gantt, Descriptions, Statistic, Timeline, Steps, Rating, Image, ImageCompare, TreeView, QRCode, Watermark, Sparkline |
| **Feedback** | Toast, Alert, Progress, Spinner, Skeleton, EmptyState, Result |
| **Overlay** | Dialog, Sheet, Drawer, AlertDialog, Popover, Tooltip, HoverCard, ContextMenu, DropdownMenu, Command, PopConfirm, Tour |
| **Navigation** | Tabs, Breadcrumb, Pagination, Sidebar, BottomNav, Menubar, NavigationMenu, MegaMenu, Accordion, Collapsible, Scrollspy, BackToTop, Affix, SpeedDial |
| **AI** | PromptInput, StreamingText, AgentMessageList, AgentMessage, ToolCallCard, ReasoningDisplay |
| **Motion** | *via Lumeo.Motion satellite* — BlurFade, BorderBeam, Marquee, NumberTicker, ShimmerButton, Sparkles, TextReveal, AnimatedBeam, Meteors, Globe, Dock, Spotlight, TypingAnimation, Confetti, MagneticButton, AnimatedGradientText, Ripple, OrbitingCircles, and 12 more |
| **Dashboard** | Bento, BentoTile, KpiCard, SparkCard, Delta |
| **Drag & Drop** | Kanban, SortableList, Transfer |
| **Charts** | 30+ ECharts types — Bar (smart labels), Line, Area, Pie, Donut, Radar, Scatter, Heatmap, Treemap, Sankey, Funnel, Gauge, Candlestick, Boxplot, Calendar, Sunburst, Graph, Parallel, ThemeRiver, WordCloud, GeoMap |

## Installation

Lumeo 2.0 follows the DevExpress / Telerik / Microsoft.Extensions model: a small core package plus opt-in satellites for heavy components. The split keeps the core lean (~530 KB) — you only pay for what you use.

```bash
# Core — always required (~121 components)
dotnet add package Lumeo

# Add satellites only for the components you use:
dotnet add package Lumeo.Charts      # Chart and 30+ subtypes
dotnet add package Lumeo.DataGrid    # DataGrid, DataTable, Filter
dotnet add package Lumeo.Editor      # RichTextEditor
dotnet add package Lumeo.Scheduler   # Scheduler
dotnet add package Lumeo.Gantt       # Gantt
dotnet add package Lumeo.Motion      # 30 motion primitives
```

Or reference them in your `.csproj`. All packages share one version (lockstep) — always upgrade them together:

```xml
<ItemGroup>
  <PackageReference Include="Lumeo"            Version="2.0.0-rc.18" />
  <!-- add only the satellites you need: -->
  <PackageReference Include="Lumeo.Charts"    Version="2.0.0-rc.18" />
  <PackageReference Include="Lumeo.DataGrid"  Version="2.0.0-rc.18" />
  <PackageReference Include="Lumeo.Editor"    Version="2.0.0-rc.18" />
  <PackageReference Include="Lumeo.Scheduler" Version="2.0.0-rc.18" />
  <PackageReference Include="Lumeo.Gantt"     Version="2.0.0-rc.18" />
  <PackageReference Include="Lumeo.Motion"    Version="2.0.0-rc.18" />
</ItemGroup>
```

`@using Lumeo` covers all satellite components — no extra `@using` directives needed.

## Companion packages

Lumeo 2.0 ships with three optional companion packages that extend the core library.

### `Lumeo.Cli` — shadcn-style vendoring

Copy component source into your own repo so you can fork and customize it — like shadcn/ui on the JS side.

```bash
dotnet tool install -g Lumeo.Cli
lumeo init                # one-time — writes lumeo.config.json
lumeo add button dialog   # copy components into your repo
lumeo list                # list all registry entries
lumeo diff button         # diff vendored copy vs registry
```

### `Lumeo.Templates` — `dotnet new` scaffolders

```bash
dotnet new install Lumeo.Templates
dotnet new lumeo-page       -n SettingsPage
dotnet new lumeo-form       -n RegisterForm
dotnet new lumeo-component  -n FancyCard
```

### `@lumeo-ui/mcp-server` — MCP server for LLM codegen

Give Claude, ChatGPT, Copilot, or Cursor the schemas + examples they need to write correct Lumeo markup.

```bash
npm install -g @lumeo-ui/mcp-server
# then wire into Claude Desktop / Cursor / your MCP client config
```

## Setup

### 1. Register services

```csharp
// Program.cs
using Lumeo;

builder.Services.AddLumeo();
```

### 2. Add imports

```razor
@* _Imports.razor *@
@using Lumeo
@using Lumeo.Services
```

### 3. Include scripts and styles

Add to your `index.html` (WASM) or `_Host.cshtml` (Server):

```html
<!-- Lumeo design tokens (CSS variables + keyframes) -->
<link rel="stylesheet" href="_content/Lumeo/css/lumeo.css" />

<!-- Pre-compiled Tailwind utilities Lumeo's components need (no local Tailwind build required) -->
<link rel="stylesheet" href="_content/Lumeo/css/lumeo-utilities.css" />

<!-- Theme initialization (prevents flash of unstyled content) -->
<script src="_content/Lumeo/js/theme.js"></script>

<!-- Component interop (loaded on demand, but can be preloaded) -->
<script src="_content/Lumeo/js/components.js" type="module"></script>
```

That's enough to make every Lumeo component render correctly. **You don't need Tailwind installed in your app.**

### 4. (Optional) Use Tailwind in your own markup

If you want to write Tailwind classes yourself (in your pages / your own components), install Tailwind CSS v4 in your app and import Lumeo's tokens so utilities like `bg-primary` resolve against Lumeo's theme:

```css
@import "tailwindcss";

/* Import Lumeo theme variables */
@import "./_content/Lumeo/css/lumeo.css" layer(base);

/* Dark mode variant */
@variant dark (&:where(.dark, .dark *));

/* Map Lumeo CSS variables to Tailwind theme */
@theme {
  --color-background: var(--color-background);
  --color-foreground: var(--color-foreground);
  --color-primary: var(--color-primary);
  --color-primary-foreground: var(--color-primary-foreground);
  /* ... see lumeo.css for full variable list */
}
```

In this setup you can drop `lumeo-utilities.css` from step 3 — your own Tailwind build will emit every utility Lumeo uses, plus anything you use in your app.

For alternate color themes, import additional theme files:

```css
@import "./_content/Lumeo/css/themes/_blue.css" layer(base);
@import "./_content/Lumeo/css/themes/_green.css" layer(base);
@import "./_content/Lumeo/css/themes/_rose.css" layer(base);
@import "./_content/Lumeo/css/themes/_orange.css" layer(base);
@import "./_content/Lumeo/css/themes/_violet.css" layer(base);
@import "./_content/Lumeo/css/themes/_amber.css" layer(base);
@import "./_content/Lumeo/css/themes/_teal.css" layer(base);
```

## Usage

```razor
@using Lumeo

<Card>
    <CardHeader>
        <Heading Level="3">Hello Lumeo</Heading>
        <Text Size="sm" Color="muted">A beautiful component library.</Text>
    </CardHeader>
    <CardContent>
        <Button OnClick="@(() => count++)">
            Clicked @count times
        </Button>
    </CardContent>
</Card>

@code {
    private int count;
}
```

### Dialogs

```razor
<Dialog @bind-IsOpen="dialogOpen">
    <DialogTrigger>
        <Button Variant="Button.ButtonVariant.Outline">Open Dialog</Button>
    </DialogTrigger>
    <DialogContent Size="DialogContent.DialogSize.Lg">
        <DialogHeader>
            <DialogTitle>Are you sure?</DialogTitle>
            <DialogDescription>This action cannot be undone.</DialogDescription>
        </DialogHeader>
        <DialogFooter>
            <Button Variant="Button.ButtonVariant.Secondary" OnClick="@(() => dialogOpen = false)">Cancel</Button>
            <Button OnClick="Confirm">Confirm</Button>
        </DialogFooter>
    </DialogContent>
</Dialog>
```

### Toasts

```razor
@inject ToastService Toast

<Button OnClick="@(() => Toast.Success("Saved!", "Your changes have been saved."))">
    Save
</Button>
```

### Form Validation

```razor
<Form Model="model" OnValidSubmit="HandleSubmit">
    <FormField Label="Email" HelpText="We'll never share your email." Required>
        <Input @bind-Value="model.Email" type="email" />
    </FormField>
    <FormField Label="Password" Required>
        <PasswordInput @bind-Value="model.Password" ShowStrength />
    </FormField>
    <Button type="submit">Sign Up</Button>
</Form>
```

## Theming

### Switch color schemes at runtime

```razor
<ThemeSwitcher />
```

Or programmatically via `ThemeService`:

```razor
@inject ThemeService Theme

<Button OnClick="@(() => Theme.SetSchemeAsync("blue"))">
    Switch to Blue
</Button>
```

### Available themes

| Theme  | Primary Color            | Character         |
|--------|--------------------------|-------------------|
| Zinc   | `hsl(240 5% 26%)`       | Clean, neutral    |
| Blue   | `hsl(221 83% 53%)`      | Corporate, trust  |
| Green  | `hsl(142 71% 45%)`      | Growth, eco       |
| Rose   | `hsl(347 77% 50%)`      | Warm, energetic   |
| Orange | `hsl(14 70% 50%)`       | Warm brand        |
| Violet | `hsl(262 83% 58%)`      | Bold, creative    |
| Amber  | `hsl(38 92% 50%)`       | Energy, attention |
| Teal   | `hsl(173 80% 40%)`      | Calm, modern      |

### Dark mode

```razor
<!-- Toggle button -->
<ThemeToggle />

<!-- Or programmatically -->
@inject ThemeService Theme

await Theme.SetModeAsync(ThemeMode.Dark);   // Force dark
await Theme.SetModeAsync(ThemeMode.Light);  // Force light
await Theme.SetModeAsync(ThemeMode.System); // Follow OS preference
await Theme.ToggleModeAsync();              // Toggle current
```

## Documentation

- **[Live Docs](https://lumeo.nativ.sh)** — Full component demos and API reference
- **[Form Validation Guide](https://lumeo.nativ.sh/docs/form-validation)** — DataAnnotations, custom validators, examples
- **[Accessibility Guide](https://lumeo.nativ.sh/docs/accessibility)** — ARIA roles, keyboard patterns, focus management
- **[Contributing Guide](https://lumeo.nativ.sh/docs/contributing)** — Setup, component creation, testing, code style
- **[Changelog](https://lumeo.nativ.sh/docs/changelog)** — Full release history
- **[Migration Guide](./MIGRATION.md)** — 1.x → 2.0 upgrade notes

## Tech Stack

- .NET 10 / Blazor
- Tailwind CSS v4
- ECharts for charts, FullCalendar for Scheduler, Frappe Gantt for Gantt, TipTap for RichTextEditor
- ClosedXML + QuestPDF for DataGrid export
- [Blazicons.Lucide](https://github.com/nickvdyck/blazicons) for icons

## License

MIT
