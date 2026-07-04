# MyApp

A Blazor WebAssembly app scaffolded with **[Lumeo](https://lumeo.nativ.sh)**. It boots
styled and dark-mode-ready with zero manual setup:

- `AddLumeo()` is already wired in `Program.cs`.
- The prebuilt `lumeo.css` (OKLCH default theme) + `lumeo-utilities.css` + `theme.js` are
  linked in `wwwroot/index.html` straight from the Lumeo NuGet package — no Tailwind build
  and no asset copying required.
- A collapsible sidebar shell (`Layout/MainLayout.razor`) with a working dark-mode toggle.
- Three example pages: **Dashboard** (KPI cards + a `DataGrid`), **Form** (validated
  `<Form>`), and **Settings** (`Tabs` + inputs).

## Run

```bash
dotnet run
```

Then open the printed URL. Toggle light/dark with the button in the top-right (or `Ctrl+B`
to collapse the sidebar).

## Project layout

| Path | What it is |
|---|---|
| `Program.cs` | Host builder — `AddLumeo()` registers all Lumeo services. |
| `wwwroot/index.html` | Links the prebuilt Lumeo CSS/JS shipped in the NuGet package. |
| `Layout/MainLayout.razor` | Sidebar + header shell with the dark-mode toggle. |
| `Pages/Dashboard.razor` | KPI cards + a sortable, paginated `DataGrid`. |
| `Pages/FormPage.razor` | `<Form>` + `<FormField>` with DataAnnotations validation. |
| `Pages/Settings.razor` | `Tabs` with form inputs and a `Select`. |

## Add more Lumeo pieces

Scaffold additional pages, forms, and components with the item templates:

```bash
dotnet new lumeo-page      --name Reports --route reports
dotnet new lumeo-form      --ModelName Feedback --PageName FeedbackPage --route feedback
dotnet new lumeo-component --ComponentName StatCard --namespace MyApp.Components
```

## Three ways to own Lumeo

This starter uses **path 1** (NuGet). You can switch at any time:

1. **NuGet package (default).** `Lumeo` + `Lumeo.DataGrid` are referenced in `MyApp.csproj`.
   Upgrade by bumping the version. You consume the components as a library.

2. **Vendor the source with the CLI.** Install the tool and copy individual components into
   your project as editable source (shadcn-style), while the shared runtime stays in the
   NuGet package:

   ```bash
   dotnet tool install -g Lumeo.Cli
   lumeo init
   lumeo add button card
   ```

3. **Eject to fully NuGet-free.** Vendor the whole Lumeo runtime as source and drop the
   package references entirely — you own 100% of the code:

   ```bash
   lumeo eject
   ```

See the [Lumeo docs](https://lumeo.nativ.sh/docs/introduction) for the full guide.
