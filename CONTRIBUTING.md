# Contributing to Lumeo

Thank you for your interest in contributing to Lumeo!

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node.js (for Tailwind CSS tooling, if needed)
- Git

## Setting Up

```bash
git clone https://github.com/Brain2k-0005/Lumeo.git
cd Lumeo
dotnet build src/Lumeo/Lumeo.csproj
```

## Project Structure

```
Lumeo/
├── src/
│   └── Lumeo/                   # Component library (NuGet package)
│       ├── UI/                  # All components, one folder per component
│       │   └── {ComponentName}/
│       │       ├── {Name}.razor
│       │       └── {Name}.razor.cs  (if needed)
│       ├── Services/            # Shared services (ComponentInteropService, etc.)
│       └── wwwroot/
│           ├── css/             # lumeo.css and theme files
│           └── js/              # JavaScript interop helpers
├── docs/
│   └── Lumeo.Docs/              # Documentation site (Blazor WASM)
│       └── Pages/               # One page per component
└── tests/
    └── Lumeo.Tests/             # bUnit component tests
```

## Building

```bash
# Build the core library
dotnet build src/Lumeo/Lumeo.csproj

# Build satellite packages (each references core)
dotnet build src/Lumeo.Charts/Lumeo.Charts.csproj
dotnet build src/Lumeo.DataGrid/Lumeo.DataGrid.csproj
dotnet build src/Lumeo.Editor/Lumeo.Editor.csproj
dotnet build src/Lumeo.Scheduler/Lumeo.Scheduler.csproj
dotnet build src/Lumeo.Gantt/Lumeo.Gantt.csproj

# Pack all packages (lockstep — all share one version from Directory.Build.props)
dotnet pack src/Lumeo/Lumeo.csproj -o ./nupkg
dotnet pack src/Lumeo.Charts/Lumeo.Charts.csproj -o ./nupkg
dotnet pack src/Lumeo.DataGrid/Lumeo.DataGrid.csproj -o ./nupkg
dotnet pack src/Lumeo.Editor/Lumeo.Editor.csproj -o ./nupkg
dotnet pack src/Lumeo.Scheduler/Lumeo.Scheduler.csproj -o ./nupkg
dotnet pack src/Lumeo.Gantt/Lumeo.Gantt.csproj -o ./nupkg

# Run the docs site
dotnet run --project docs/Lumeo.Docs/Lumeo.Docs.csproj
```

> **Release model:** All 6 packages (`Lumeo`, `Lumeo.Charts`, `Lumeo.DataGrid`, `Lumeo.Editor`,
> `Lumeo.Scheduler`, `Lumeo.Gantt`) are versioned and released in lockstep — the canonical
> version lives in `Directory.Build.props`. The CI publish workflow packs and pushes all 6 in a
> single job when a GitHub release is published.

## Running Tests

```bash
dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj
```

## Adding a New Component

1. Create a folder: `src/Lumeo/UI/{ComponentName}/`
2. Add `{ComponentName}.razor` with the required boilerplate (see below).
3. Add a documentation page at `docs/Lumeo.Docs/Pages/{ComponentName}Page.razor`.
4. Register the route in the docs nav if applicable.
5. Add tests in `tests/Lumeo.Tests/`.

### Required Component Boilerplate

Every `.razor` file must include:

```razor
@namespace Lumeo

<div class="@_classes" @attributes="AdditionalAttributes">
    <!-- component content -->
</div>

@code {
    [Parameter] public string? Class { get; set; }
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string _classes => $"base-classes {Class}".Trim();
}
```

## Coding Conventions

- **Namespace**: `@namespace Lumeo` must be the first line of every `.razor` file.
- **CSS classes**: Always combine via `$"{BaseClasses} {Class}".Trim()` — no hardcoded colors.
- **Theme colors**: Use CSS variables only (`bg-primary`, `text-foreground`, etc.) — never raw hex or hsl values.
- **Dark mode**: Handled by CSS variable swaps in `lumeo.css` — do **not** use `dark:` Tailwind prefixes.
- **Icons**: Use `<Blazicon Svg="Lucide.X" />` from `Blazicons.Lucide`.
- **JS interop**: Go through `ComponentInteropService` — never call `IJSRuntime` directly in components.
- **Enums**: Define inside the component `@code` block.
- **State management**: Use `CascadingValue` with context records (`IsFixed="false"` for mutable state).
- **Two-way binding**: Use `Property` + `PropertyChanged` EventCallback pairs.

### Overlay Components

Overlay components (modals, drawers, popovers, etc.) must:

- Use `ComponentInteropService.RegisterClickOutside` for dismiss-on-click-outside.
- Call `Interop.LockScroll()` / `UnlockScroll()` for modal overlays.
- Call `Interop.SetupFocusTrap()` / `RemoveFocusTrap()` for focus management.
- Implement `IAsyncDisposable` for cleanup.
- Handle `JSDisconnectedException` in cleanup methods.

## Pull Request Guidelines

- Keep PRs focused — one feature or fix per PR.
- Include bUnit tests for any new components.
- Ensure `dotnet build` passes before submitting.
- Update the relevant docs page if you add or change a component's API.
- Do not include generated files or IDE-specific config in your PR.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
