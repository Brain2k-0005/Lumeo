# Lumeo

A beautiful, accessible Blazor component library inspired by [shadcn/ui](https://ui.shadcn.com). 103 production-ready components with Tailwind CSS theming, dark mode, and 8 color schemes.

**[Live Demo](https://lumeo.nativ.sh)** | [![NuGet](https://img.shields.io/nuget/v/Lumeo)](https://www.nuget.org/packages/Lumeo) | [![Live Demo](https://img.shields.io/badge/Live%20Demo-lumeo.nativ.sh-blue)](https://lumeo.nativ.sh)

## Features

- **103 Components** — Fully accessible, production-ready UI primitives
- **8 Color Themes** — Zinc, Blue, Green, Rose, Orange, Violet, Amber, Teal
- **Dark Mode** — Class-based with system preference detection
- **Tailwind CSS v4** — CSS variable architecture, zero hardcoded colors
- **Programmatic OverlayService** — Open dialogs, sheets, drawers, and toasts from code
- **30+ Chart Types** — Powered by ECharts (Bar, Line, Area, Pie, Donut, Radar, Scatter, and more)
- **DataGrid** — Sort, filter, inline edit, column pin, row group, virtual scroll, and CSV/JSON export
- **Form Validation** — DataAnnotations and custom validation with styled error states
- **Accessible** — ARIA attributes, keyboard navigation, focus trapping, screen reader support
- **1,316 Tests** — Comprehensive bUnit test coverage
- **Blazor WASM & Server** — Works with both hosting models

## Component Categories

### Layout
Stack, Flex, Grid, Container, Center, Spacer, AspectRatio, Resizable, ScrollArea, Separator

### Typography
Text, Heading, Link, Code

### Forms
Input, Select, Combobox, DatePicker, DateRangePicker, DateTimePicker, TimePicker, NumberInput, PasswordInput, InputMask, Checkbox, Switch, RadioGroup, Slider, Toggle, ToggleGroup, FileUpload, OtpInput, TagInput, ColorPicker, Textarea, Form, Mention, Cascader

### Data Display
Table, DataTable, DataGrid, Card, Badge, Chip, Avatar, Calendar, Descriptions, Statistic, Timeline, Steps, Rating, Image, ImageCompare, TreeView, QRCode, Watermark

### Feedback
Toast, Alert, Progress, Spinner, Skeleton, EmptyState, Result

### Overlay
Dialog, Sheet, Drawer, AlertDialog, Popover, Tooltip, HoverCard, ContextMenu, DropdownMenu, Command, PopConfirm, Tour

### Navigation
Tabs, Breadcrumb, Pagination, Sidebar, Menubar, NavigationMenu, MegaMenu, Accordion, Collapsible, Scrollspy, BackToTop, Affix, SpeedDial

### Drag & Drop
Kanban, SortableList, Transfer

### Charts
30+ types via ECharts: Bar, Line, Area, Pie, Donut, Radar, Scatter, Heatmap, Treemap, Sankey, Funnel, Gauge, Candlestick, Boxplot, Calendar, Sunburst, Graph, Parallel, ThemeRiver, WordCloud, GeoMap, and more

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
<!-- Lumeo theme CSS -->
<link rel="stylesheet" href="_content/Lumeo/css/lumeo.css" />

<!-- Theme initialization (prevents flash of unstyled content) -->
<script src="_content/Lumeo/js/theme.js"></script>

<!-- Component interop (loaded on demand, but can be preloaded) -->
<script src="_content/Lumeo/js/components.js" type="module"></script>
```

### 4. Configure Tailwind CSS

Lumeo components use Tailwind CSS v4 utility classes. In your Tailwind CSS entry file:

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

## Tech Stack

- .NET 10 / Blazor
- Tailwind CSS v4
- ECharts for charts
- [Blazicons.Lucide](https://github.com/nickvdyck/blazicons) for icons

## License

MIT
