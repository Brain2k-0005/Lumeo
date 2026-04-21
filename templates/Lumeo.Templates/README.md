# Lumeo.Templates

Scaffolding templates for Lumeo Blazor projects.

## Install

```bash
dotnet new install Lumeo.Templates
```

## Templates

### `lumeo-page`

Scaffolds a new `.razor` page with `@page` directive, `PageHeader`, `Stack`, and a starter `Card`/`Button` block.

```bash
dotnet new lumeo-page --name Dashboard --route dashboard
```

Parameters:
- `--name` (PageName): The page class name (PascalCase). Default: `NewPage`.
- `--route`: URL route without leading slash. Default: `new-page`.

### `lumeo-form`

Scaffolds a POCO model annotated with `[LumeoForm]` plus a page that renders the generated form.

```bash
dotnet new lumeo-form --ModelName Feedback --PageName FeedbackPage --route feedback
```

Parameters:
- `--ModelName`: Model class name (PascalCase). Default: `ContactForm`.
- `--PageName`: Page class name (PascalCase). Default: `ContactFormPage`.
- `--route`: Page route. Default: `contact`.

### `lumeo-component`

Scaffolds a reusable `.razor` component following Lumeo conventions (`@namespace`, `Class`, `AdditionalAttributes`, CSS variables).

```bash
dotnet new lumeo-component --ComponentName Hero --namespace MyApp.Components
```

Parameters:
- `--ComponentName`: Component name (PascalCase). Default: `MyComponent`.
- `--namespace`: Target namespace. Default: `MyApp.Components`.

## Uninstall

```bash
dotnet new uninstall Lumeo.Templates
```
