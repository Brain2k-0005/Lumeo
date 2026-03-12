# Lumeo

A beautiful, accessible Blazor component library inspired by [shadcn/ui](https://ui.shadcn.com). 90+ production-ready components with Tailwind CSS theming, dark mode, and multiple color schemes.

**[Live Demo](https://lumeo.nativ.sh)** | [![Live Demo](https://img.shields.io/badge/Live%20Demo-lumeo.nativ.sh-blue)](https://lumeo.nativ.sh)

## Features

- **90+ Components** — Fully accessible, production-ready UI primitives
- **7 Color Themes** — Vega (Orange), Nova (Zinc), Maia (Blue), Lyra (Green), Mira (Rose), Violet, Amber, Teal
- **Dark Mode** — Class-based with system preference detection
- **Tailwind CSS v4** — CSS variable architecture, zero hardcoded `dark:` overrides
- **Programmatic OverlayService** — Open dialogs, sheets, drawers, and toasts from code
- **30 Chart Types** — Powered by ECharts (Bar, Line, Area, Pie, Donut, Radar, Scatter, and more)
- **DataGrid** — Sort, filter, edit, and export with built-in pagination
- **Layout Primitives** — Stack, Flex, Grid, Container, Center, Spacer
- **Accessible** — ARIA attributes, keyboard navigation, focus trapping
- **Blazor WASM & Server** — Works with both hosting models

## Component Categories

### Layout
Stack, Flex, Grid, Container, Center, Spacer

### Typography
Text, Heading, Link, Code

### Forms
Input, Select, Combobox, DatePicker, TimePicker, NumberInput, Checkbox, Switch, RadioGroup, Slider, Toggle, FileUpload, OtpInput, TagInput, ColorPicker, Textarea, Form

### Data Display
Table, DataGrid, Card, Badge, Avatar, Calendar, Descriptions, Statistic, Timeline, Steps, Rating, Image

### Feedback
Toast, Alert, Progress, Spinner, Skeleton, EmptyState, Result

### Overlay
Dialog, Sheet, Drawer, AlertDialog, Popover, Tooltip, HoverCard, ContextMenu, DropdownMenu, Command

### Navigation
Tabs, Breadcrumb, Pagination, Sidebar, Menubar, NavigationMenu, Accordion, Collapsible

### Charts
30 types via ECharts: Bar, Line, Area, Pie, Donut, Radar, Scatter, Heatmap, Treemap, Sankey, Funnel, Gauge, Candlestick, Boxplot, Calendar, Sunburst, Graph, Parallel, ThemeRiver, and more

## Installation

```bash
dotnet add package Lumeo
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
<!-- Theme initialization (prevents flash of unstyled content) -->
<script src="_content/Lumeo/js/theme.js"></script>

<!-- Component interop (loaded on demand, but can be preloaded) -->
<script src="_content/Lumeo/js/components.js" type="module"></script>
```

### 4. Configure Tailwind CSS

Lumeo components use Tailwind CSS utility classes. You need Tailwind CSS v4 configured in your project.

In your Tailwind CSS entry file:

```css
@import "tailwindcss";

/* Scan Lumeo component files for Tailwind classes */
@source "../../path/to/nuget/packages/lumeo/0.1.0/staticwebassets/**/*.razor";

/* Or if using project reference: */
@source "../../Lumeo/src/Lumeo/**/*.razor";

/* Dark mode variant */
@variant dark (&:where(.dark, .dark *));

/* Import Lumeo theme variables into your @theme block */
@theme {
  --color-background: hsl(30 20% 98%);
  --color-foreground: hsl(20 10% 12%);
  /* ... see lumeo.css for full variable list */
}
```

Alternatively, use the pre-built CSS variables file:

```css
@import "./_content/Lumeo/css/lumeo.css" layer(base);
```

For alternate color themes:

```css
@import "./_content/Lumeo/css/themes/_zinc.css" layer(base);
@import "./_content/Lumeo/css/themes/_blue.css" layer(base);
@import "./_content/Lumeo/css/themes/_green.css" layer(base);
@import "./_content/Lumeo/css/themes/_rose.css" layer(base);
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

<Dialog @bind-Open="dialogOpen">
    <DialogTrigger>
        <Button Variant="Button.ButtonVariant.Outline">Open Dialog</Button>
    </DialogTrigger>
    <DialogContent>
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

@code {
    private int count;
    private bool dialogOpen;
    private void Confirm() => dialogOpen = false;
}
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
| Vega   | `hsl(14 70% 50%)`       | Warm brand        |
| Nova   | `hsl(240 5% 26%)`       | Clean, neutral    |
| Maia   | `hsl(221 83% 53%)`      | Corporate, trust  |
| Lyra   | `hsl(142 71% 45%)`      | Growth, eco       |
| Mira   | `hsl(347 77% 50%)`      | Warm, energetic   |
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

## Tech Stack

- .NET 10 / Blazor
- Tailwind CSS v4
- ECharts (via DnetEcharts) for charts
- [Blazicons.Lucide](https://github.com/nickvdyck/blazicons) for icons

## License

MIT
