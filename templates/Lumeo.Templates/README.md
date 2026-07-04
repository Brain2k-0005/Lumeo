# Lumeo.Templates

`dotnet new` templates for [Lumeo](https://lumeo.nativ.sh) Blazor projects — one full app
starter plus item scaffolds for pages, forms, and components.

## Install

```bash
dotnet new install Lumeo.Templates
```

After install, `dotnet new list lumeo` lists all four templates. Remove them with
`dotnet new uninstall Lumeo.Templates`.

| Template | Short name | Kind | What you get |
|---|---|---|---|
| Lumeo Blazor WASM App | `lumeo-app` | project | A styled, dark-mode-ready Blazor WebAssembly app |
| Lumeo Page | `lumeo-page` | item | A `.razor` page with the standard layout |
| Lumeo Form | `lumeo-form` | item | A validated form via the `[LumeoForm]` generator |
| Lumeo Component | `lumeo-component` | item | A reusable component following the Lumeo contract |

## Start a new app

```bash
dotnet new lumeo-app -n MyApp
cd MyApp
dotnet run
```

That's the whole setup. The app boots **styled and dark-mode-ready with zero manual steps** —
`AddLumeo()` is wired, the prebuilt CSS/JS are linked from the NuGet package, and it ships a
collapsible sidebar shell plus Dashboard, Form, and Settings example pages.

## Item templates

The item templates drop files into an **existing Lumeo project** (see
[Prerequisites](#prerequisites-for-item-templates) below, or just start from `lumeo-app`).

### `lumeo-page`

A `.razor` page with an `@page` directive, `Container` + `Stack`, a `Heading` + `Lumeo.Text`
header, and a starter `Card` with buttons.

```bash
dotnet new lumeo-page --name Dashboard --route dashboard
```

| Option | Description | Default |
|---|---|---|
| `--name` / `-n` | Page class + file name (PascalCase) | `NewPage` |
| `--route` | URL route, without the leading slash | `new-page` |

### `lumeo-form`

A POCO annotated with `[LumeoForm]` plus a page that renders it. The
[`[LumeoForm]` source generator](https://lumeo.nativ.sh/docs/lumeo-form) (shipped in the core
package) emits the whole `<Form>` — wired to the built-in `DataAnnotationsFormValidator`, so
invalid submits show per-field errors automatically.

```bash
dotnet new lumeo-form --ModelName Feedback --PageName FeedbackPage --route feedback
```

| Option | Description | Default |
|---|---|---|
| `--ModelName` | Model class + file name (PascalCase) | `ContactForm` |
| `--PageName` | Page class + file name (PascalCase) | `ContactFormPage` |
| `--route` | Page route, without the leading slash | `contact` |

### `lumeo-component`

A reusable `.razor` component pre-wired with the Lumeo component contract: an explicit
`@namespace`, a `Class` parameter, an `AdditionalAttributes` splat, and `Variant` / `Size` /
`Disabled` parameters flowing through a class-merging builder.

```bash
dotnet new lumeo-component --ComponentName Hero --namespace MyApp.Components
```

| Option | Description | Default |
|---|---|---|
| `--ComponentName` | Component class + file name (PascalCase) | `MyComponent` |
| `--namespace` | Target namespace | `MyApp.Components` |

## Prerequisites for item templates

The item templates assume a Blazor project already set up for Lumeo. `dotnet new lumeo-app`
does all of this for you; to add Lumeo to an existing app:

1. **Reference the package(s).** Core, plus any satellite you use:

   ```bash
   dotnet add package Lumeo
   dotnet add package Lumeo.DataGrid   # only if you use DataGrid / DataTable / Filter
   ```

2. **Register the services** in `Program.cs`:

   ```csharp
   using Lumeo;
   builder.Services.AddLumeo();
   ```

3. **Link the prebuilt assets** in `wwwroot/index.html`. All four are shipped inside the
   Lumeo NuGet package as static web assets — no Tailwind build, no copying. Miss the
   `lumeo-utilities.css` link and the app renders **unstyled**; miss `theme.js` and dark mode
   / theming break:

   ```html
   <link rel="stylesheet" href="_content/Lumeo/css/lumeo.css" />
   <link rel="stylesheet" href="_content/Lumeo/css/lumeo-utilities.css" />
   <script src="_content/Lumeo/js/theme.js"></script>
   <script src="_content/Lumeo/js/components.js" type="module"></script>
   ```

4. **Import the namespaces** in `_Imports.razor`:

   ```razor
   @using Lumeo
   @using Lumeo.Services
   ```

## Conventions enforced

- `@namespace` declared at the top of component `.razor` files.
- `[Parameter] public string? Class` for consumer-supplied CSS classes.
- `[Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes`
  splatted onto the root element.
- Theme tokens via Tailwind classes (e.g. `bg-card`, `text-foreground`, `border-border`) —
  never raw hex / hsl / rgb, and no `dark:` prefix (dark mode is CSS-variable driven).
- Icons via the built-in `LumeoIcons` set + `<SvgGlyph>` — icons ship in the Lumeo core since
  4.1, e.g. `<SvgGlyph Svg="@(LumeoIcons.Star)" class="h-4 w-4" />`. No separate icon package
  is required.

## Own your components

Beyond consuming the NuGet package, the [Lumeo CLI](https://lumeo.nativ.sh/docs/cli) lets you
vendor individual components as source (`lumeo add button`) or eject to a fully NuGet-free
project (`lumeo eject`). See the docs for the full guide.
