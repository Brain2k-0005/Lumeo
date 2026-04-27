# Lumeo.Templates

`dotnet new` scaffolding templates for Lumeo Blazor projects.

## Install

```bash
dotnet new install Lumeo.Templates
```

## Prerequisites

The templates assume a Blazor project that already references at least the core Lumeo package:

```bash
dotnet add package Lumeo
```

For component-specific templates (e.g. forms with rich text), also add the relevant satellite:

| If your scaffold uses … | Add this satellite |
|---|---|
| `Chart` and chart subtypes | `Lumeo.Charts` |
| `DataGrid`, `DataTable`, `Filter` | `Lumeo.DataGrid` |
| `RichTextEditor` | `Lumeo.Editor` |
| `Scheduler` | `Lumeo.Scheduler` |
| `Gantt` | `Lumeo.Gantt` |

The templates themselves only use core Lumeo components, so a fresh install of `Lumeo` is enough to start.

## Templates

### `lumeo-page`

Scaffolds a new `.razor` page with `@page` directive, `Container`, `Stack`, `PageHeader`, and a starter `Card` with primary + secondary `Button`.

```bash
dotnet new lumeo-page --name Dashboard --route dashboard
```

Parameters:
- `--name` (PageName): page class name in PascalCase. Default: `NewPage`.
- `--route`: URL route without leading slash. Default: `new-page`.

### `lumeo-form`

Scaffolds a POCO model annotated with DataAnnotations + a page that renders it through Lumeo's canonical `<FormField>` wrapper pattern (the validation pattern documented at `/components/form#validation-pattern`). Each input lives inside a `<FormField>` that owns the `Label`, `HelpText`, `Required` marker, and `Error` display.

```bash
dotnet new lumeo-form --ModelName Feedback --PageName FeedbackPage --route feedback
```

Parameters:
- `--ModelName`: model class name (PascalCase). Default: `ContactForm`.
- `--PageName`: page class name (PascalCase). Default: `ContactFormPage`.
- `--route`: page route. Default: `contact`.

### `lumeo-component`

Scaffolds a reusable `.razor` component pre-wired with the Lumeo 2.0 contract: `@namespace`, `Class` parameter, `AdditionalAttributes` splat, theme-token-only CSS, plus `Variant` and `Size` enums and a `Disabled` parameter wired into the class builder.

```bash
dotnet new lumeo-component --ComponentName Hero --namespace MyApp.Components
```

Parameters:
- `--ComponentName`: component name (PascalCase). Default: `MyComponent`.
- `--namespace`: target namespace. Default: `MyApp.Components`.

## Uninstall

```bash
dotnet new uninstall Lumeo.Templates
```

## Conventions enforced

Every template follows the Lumeo 2.0 component contract:

- `@namespace` declared at the top of `.razor` files.
- `[Parameter] public string? Class` for consumer-supplied CSS classes.
- `[Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes` splatted onto the root element.
- Theme tokens via Tailwind classes (e.g. `bg-card`, `text-foreground`, `border-border`) — never raw hex / hsl / rgb.
- No `dark:` Tailwind prefix in components — dark mode is handled by CSS variable swaps in `lumeo.css`.
- Icons via `<Blazicon Svg="Lucide.X" />` from `Blazicons.Lucide`.

If you scaffold a form, the model uses `System.ComponentModel.DataAnnotations` and is rendered through the `<FormField>` wrapper pattern. If you scaffold a component, you get `Variant` / `Size` / `Disabled` parameters wired through a class-merging builder you can extend.
