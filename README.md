# Lumeo

A beautiful, accessible Blazor component library inspired by [shadcn/ui](https://ui.shadcn.com). 45+ production-ready components with Tailwind CSS theming, dark mode, and multiple color schemes.

## Features

- **45+ Components** — Accordion, Alert, AlertDialog, Avatar, Badge, Breadcrumb, Button, Calendar, Card, Checkbox, Collapsible, Combobox, Command, DataTable, DatePicker, Dialog, DropdownMenu, EmptyState, FileUpload, Form, Input, Label, OtpInput, Pagination, Popover, Progress, RadioGroup, ScrollArea, Select, Separator, Sheet, Sidebar, Skeleton, Spinner, Switch, Table, Tabs, Textarea, ThemeSwitcher, ThemeToggle, Toast, Tooltip
- **5 Color Themes** — Orange (default), Zinc, Blue, Green, Rose
- **Dark Mode** — Class-based with system preference detection
- **Tailwind CSS v4** — CSS variable architecture, zero hardcoded `dark:` overrides
- **Accessible** — ARIA attributes, keyboard navigation, focus trapping
- **Blazor WASM & Server** — Works with both hosting models

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
```

## Usage

```razor
@using Lumeo

<Card>
    <CardHeader>
        <h3 class="text-lg font-semibold">Hello Lumeo</h3>
        <p class="text-sm text-muted-foreground">A beautiful component library.</p>
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
| Orange | `hsl(14 70% 50%)`       | Warm brand        |
| Zinc   | `hsl(240 5% 26%)`       | Clean, neutral    |
| Blue   | `hsl(221 83% 53%)`      | Corporate, trust  |
| Green  | `hsl(142 71% 45%)`      | Growth, eco       |
| Rose   | `hsl(347 77% 50%)`      | Warm, energetic   |

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
- [Blazicons.Lucide](https://github.com/nickvdyck/blazicons) for icons

## License

MIT
