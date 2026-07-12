# Lumeo

**164 accessible Blazor components, AI-ready, motion-integrated, shadcn-inspired.**

**164 components · 5,800+ tests** · 14 locales · mobile-first · MIT · .NET 8+

[![NuGet](https://img.shields.io/nuget/v/Lumeo?logo=nuget&label=Lumeo)](https://www.nuget.org/packages/Lumeo)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Lumeo?logo=nuget&label=downloads)](https://www.nuget.org/packages/Lumeo)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](./LICENSE)
[![Live Demo](https://img.shields.io/badge/demo-lumeo.nativ.sh-black?logo=cloudflare)](https://lumeo.nativ.sh)
[![Agent Skill on skills.sh](https://img.shields.io/badge/skills.sh-lumeo-000?logo=vercel&logoColor=white)](https://skills.sh/Brain2k-0005/Lumeo/lumeo)
[![GitHub stars](https://img.shields.io/github/stars/Brain2k-0005/Lumeo?style=flat&logo=github)](https://github.com/Brain2k-0005/Lumeo/stargazers)
[![Sponsor](https://img.shields.io/github/sponsors/Brain2k-0005?logo=github-sponsors&color=ea4aaa)](https://github.com/sponsors/Brain2k-0005)

> **Lumeo 4.2.0 is on NuGet** — a first-party **icon family** (16 trimmable packs), **`dotnet new`** app + full-stack templates, and a **shadcn-parity campaign** (overlay exit animations, `data-*` styling hooks, native form participation, menu-system + NavigationMenu parity, chart/AI a11y) on top of the 4.0 major (Radix / Base UI / shadcn parity audit + a 164-component battle-test, ~355 bugs fixed, 5,800+ tests, CLI **100% NuGet-free** eject). `dotnet add package Lumeo`. See [`CHANGELOG.md`](./CHANGELOG.md) — 4.1 is an additive, opt-in upgrade from 4.0 (a few documented behaviour changes); from 3.x see [`MIGRATION.md`](./MIGRATION.md).

## What's new in 4.0

4.0 pairs a Radix / Base UI / shadcn **parity audit** with a library-wide **correctness hardening** pass. There are **no API-signature breaks** — see [`MIGRATION.md`](./MIGRATION.md) for the handful of behaviour changes.

- **NuGet-free standalone eject** — `lumeo eject` (or `lumeo init --standalone`) vendors components **plus the whole runtime** as source, so a project compiles and runs with zero `Lumeo` / satellite `PackageReference`. Proven across all 164 components.
- **Battle-test campaign** — an adversarial sweep of all 164 components fixed ~355 confirmed bugs (UI state surviving data refreshes, keyboard / ARIA, edge data, lifecycle teardown, keyed reorder), each with a bUnit regression test (suite 5,800+ green).
- **OKLCH theme palette** — base + all 8 themes (878 tokens) migrated HSL → OKLCH, exact 1:1 (brand identity unchanged), matching Tailwind v4 / current shadcn.
- **RTL** — new `DirectionProvider` + a logical-utility migration (`ml-→ms-`, `left-→start-`, …); identical in LTR, mirrored in RTL.
- **tweakcn / shadcn native compatibility** — a bare shadcn `--primary` (or a pasted tweakcn export) drives Lumeo's tokens 1:1 with zero setup.
- **Accessibility & composition** — `aria-describedby` across every form control, `aria-current`, reduced-motion entry gating; Card `CardTitle` / `CardDescription`, Avatar `StatusLabel`, Chart `AriaLabel`, `AsChild` on AlertDialog / Drawer triggers, public `Lumeo.Cx`, DataTable `ItemKey`.
- **LumeoFormGenerator** — TimeOnly / TimeSpan / `List<string>` / Phone / Url mappings, `[Range]` / `[StringLength]` validation, `[Display(Order)]` ordering.
- **MCP** — type-bound enum validation + a new `lumeo_get_a11y` tool (roles, keyboard keys, focus).

## Feature overview

- **164 components** — accessible UI primitives, Blazor WASM & Server
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
- **Mobile-first** — touch gestures (swipe, pinch, long-press, pull-to-refresh, swipe-actions), 44×44 px hit targets per WCAG 2.5.5, iOS-style wheel pickers, haptic feedback service, safe-area helpers — try it at `/docs/mobile`
- **5,800+ tests** — CI-enforced on every PR

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

Lumeo follows the DevExpress / Telerik / Microsoft.Extensions model: a small core package plus opt-in satellites for heavy components — you only pay for what you use. Live, per-package brotli sizes are tracked at [lumeo.nativ.sh/docs/bundle-facts](https://lumeo.nativ.sh/docs/bundle-facts).

```bash
# Core — always required (the bulk of the component set)
dotnet add package Lumeo

# Add satellites only for the components you use:
dotnet add package Lumeo.Charts      # Chart and 30+ subtypes
dotnet add package Lumeo.DataGrid    # DataGrid, DataTable, Filter
dotnet add package Lumeo.Editor      # RichTextEditor
dotnet add package Lumeo.Scheduler   # Scheduler
dotnet add package Lumeo.Gantt       # Gantt
dotnet add package Lumeo.Motion      # 30 motion primitives
dotnet add package Lumeo.Maps        # Map
dotnet add package Lumeo.PdfViewer   # PdfViewer
dotnet add package Lumeo.FileViewer  # FileViewer
dotnet add package Lumeo.CodeEditor  # CodeEditor
```

Or reference them in your `.csproj`. All packages share one version (lockstep) — always upgrade them together:

```xml
<ItemGroup>
  <PackageReference Include="Lumeo"            Version="4.2.0" />
  <!-- add only the satellites you need: -->
  <PackageReference Include="Lumeo.Charts"    Version="4.2.0" />
  <PackageReference Include="Lumeo.DataGrid"  Version="4.2.0" />
  <PackageReference Include="Lumeo.Editor"    Version="4.2.0" />
  <PackageReference Include="Lumeo.Scheduler" Version="4.2.0" />
  <PackageReference Include="Lumeo.Gantt"     Version="4.2.0" />
  <PackageReference Include="Lumeo.Motion"    Version="4.2.0" />
  <PackageReference Include="Lumeo.PdfViewer" Version="4.2.0" />
  <PackageReference Include="Lumeo.Maps"      Version="4.2.0" />
  <PackageReference Include="Lumeo.CodeEditor" Version="4.2.0" />
  <PackageReference Include="Lumeo.FileViewer" Version="4.2.0" />
</ItemGroup>
```

`@using Lumeo` covers all satellite components — no extra `@using` directives needed.

## Companion packages

Lumeo ships with three optional companion packages that extend the core library.

### `Lumeo.Cli` — shadcn-style vendoring

Copy component source into your own repo so you can fork and customize it — like shadcn/ui on the JS side.

```bash
dotnet tool install -g Lumeo.Cli
lumeo init                # one-time — writes lumeo.json
lumeo add button dialog   # copy components into your repo
lumeo list                # list all registry entries
lumeo diff button         # diff vendored copy vs registry
lumeo eject               # go 100% NuGet-free (vendor the runtime too)
```

`lumeo eject` (or `lumeo init --standalone`) vendors the components **and** the runtime they need, so the project builds with no `Lumeo` package reference at all — proven across all 164 components.

### `Lumeo.Templates` — `dotnet new` scaffolders

```bash
dotnet new install Lumeo.Templates
dotnet new lumeo-page       -n SettingsPage
dotnet new lumeo-form       -n RegisterForm
dotnet new lumeo-component  -n FancyCard
```

### `@lumeo-ui/mcp-server` — MCP server for LLM codegen

Give Claude, ChatGPT, Copilot, or Cursor the schemas + examples they need to write correct Lumeo markup. 13 tools: `lumeo_list_components`, `lumeo_search`, `lumeo_get_component` (full per-parameter schema), `lumeo_get_example`, `lumeo_get_install`, `lumeo_validate_markup` (pre-flight check Razor for hallucinated APIs / bad enums / bad nesting), `lumeo_get_a11y` (roles / keyboard / focus), `lumeo_get_theme_tokens`, `lumeo_list_services` / `lumeo_get_service`, `lumeo_list_patterns` / `lumeo_get_pattern`, `lumeo_changelog`.

```bash
npm install -g @lumeo-ui/mcp-server
# then wire into Claude Desktop / Cursor / your MCP client config
```

### Lumeo Agent Skill — `skills/lumeo/`

A portable [agent skill](https://docs.claude.com/en/docs/agents-and-tools/agent-skills) that teaches Claude Code, Cursor, Codex, Gemini CLI, OpenCode and 50+ other AI agents the Lumeo conventions and how to drive the `lumeo-mcp` server.

```bash
npx skills add github.com/Brain2k-0005/Lumeo/skills/lumeo
```

Installs to `.agents/skills/lumeo/` in your current project with symlinks for every supported agent. Use the Vercel-Labs [`skills` CLI](https://github.com/vercel-labs/skills) — discoverable at [skills.sh](https://skills.sh). The skill auto-activates whenever you mention a Lumeo component.

Other install methods (manual copy, per-project, global) are documented in [`skills/lumeo/README.md`](skills/lumeo/README.md).

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
        <Text Size="sm" Color="muted">A Blazor component library.</Text>
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
- **[Migration Guide](./MIGRATION.md)** — 3.x → 4.0 upgrade notes

## Tech Stack

- .NET 8+ / Blazor — every shipped package multi-targets `net8.0;net10.0`. One
  known net8.0-only limitation: IME-composition guards (`KeyboardEventArgs.IsComposing`,
  added in .NET 9's `Microsoft.AspNetCore.Components.Web`) are unavailable on net8.0,
  so Command's arrow-key navigation and PromptInput's Enter-to-send don't
  distinguish IME candidate-window input from a "real" keystroke on that target —
  everything else is functionally identical across both TFMs.
- Tailwind CSS v4
- ECharts for charts, FullCalendar for Scheduler, Frappe Gantt for Gantt, TipTap for RichTextEditor
- ClosedXML + QuestPDF for DataGrid export
- First-party `Lumeo.Icons.*` packs (Lucide, Tabler, Phosphor, …) for icons

## License

MIT
