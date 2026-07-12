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
dotnet pack src/Lumeo.CodeEditor/Lumeo.CodeEditor.csproj -o ./nupkg
dotnet pack src/Lumeo.DataGrid/Lumeo.DataGrid.csproj -o ./nupkg
dotnet pack src/Lumeo.Editor/Lumeo.Editor.csproj -o ./nupkg
dotnet pack src/Lumeo.FileViewer/Lumeo.FileViewer.csproj -o ./nupkg
dotnet pack src/Lumeo.Scheduler/Lumeo.Scheduler.csproj -o ./nupkg
dotnet pack src/Lumeo.Gantt/Lumeo.Gantt.csproj -o ./nupkg
dotnet pack src/Lumeo.Motion/Lumeo.Motion.csproj -o ./nupkg
dotnet pack src/Lumeo.PdfViewer/Lumeo.PdfViewer.csproj -o ./nupkg
dotnet pack src/Lumeo.Maps/Lumeo.Maps.csproj -o ./nupkg

# Run the docs site
dotnet run --project docs/Lumeo.Docs/Lumeo.Docs.csproj
```

> **Release model:** All NuGet packages (`Lumeo`, `Lumeo.Charts`, `Lumeo.CodeEditor`,
> `Lumeo.DataGrid`, `Lumeo.Editor`, `Lumeo.FileViewer`, `Lumeo.Scheduler`, `Lumeo.Gantt`,
> `Lumeo.Motion`, `Lumeo.PdfViewer`, `Lumeo.Maps`) plus the `Lumeo.Cli` / `Lumeo.Templates`
> tooling and the `@lumeo-ui/mcp-server` npm package are versioned and released in lockstep —
> the canonical version lives in `Directory.Build.props`. The CI publish workflow packs and
> pushes them all when a GitHub release is published.

## Running Tests

```bash
dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj
```

### The NuGet-free "standalone eject" guarantee

`lumeo init --standalone` / `lumeo add` / `lumeo eject` vendor components as
**source** into a consumer's project instead of a NuGet package, so the project
never carries a `Lumeo`/`Lumeo.*` `PackageReference`. This is proven end-to-end by
`tests/Lumeo.Cli.Tests/CliStandaloneE2ETests.cs`, which runs the built CLI as a
real subprocess and does an actual `dotnet build` of the scaffolded project — not
a unit test of the vendoring logic in isolation.

Two tests generalize that proof to the whole registry and run on different
cadences (both tagged with an xunit `Category` trait):

- `Category=EjectGateSmoke` — 5 representative components in one project (a plain
  component, one with component+service deps, the imperative-overlay pattern, the
  icon-rendering component, and one satellite). Fast; runs on **every PR** as part
  of the normal `dotnet test Lumeo.slnx` in `ci.yml`.
- `Category=EjectGateFull` — **every** registered component (`add --all`) in one
  fresh project, one `dotnet build`. Slow (~164 components); excluded from the
  per-PR run and instead runs weekly, on `workflow_dispatch`, and on every
  published release via `.github/workflows/eject-gate.yml`.

If you add a component with a new external (non-Lumeo) NuGet dependency, also add
it to `AllExternalPackagesCsproj()` in `CliStandaloneE2ETests.cs` — that
pre-references every such package so the eject-gate-full build doesn't depend on
the CLI's own `dotnet add package` shell-out succeeding in CI.

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
- **Icons**: Use `<SvgGlyph Svg="@(Lucide.X)" />` from the first-party `Lumeo.Icons.Lucide` pack (`@using Lumeo.Icons`), or `<Icon Name="X" />` for the built-in app-chrome vocabulary.
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

### `@bind-Value` convention (#87.3)

Every two-way-bindable input MUST expose the `Value` + `ValueChanged`
pair so consumers can write `<MyInput @bind-Value="_model.Field" />`
without learning per-input variations. Concretely:

- Parameter name: `Value` (not `Text`, `Selection`, `Number`).
- Event callback: `EventCallback<T> ValueChanged` typed to the same `T`.
- Fire `ValueChanged` on the SAME edit boundary the user expects:
  - **Text-like inputs** (Input, Textarea, NumberInput): on every keystroke
    so live validation works. If debouncing is desirable, expose a
    `DebounceMs` parameter rather than swallowing keystrokes.
  - **Picker inputs** (Select, Combobox, DatePicker, ColorPicker): on
    selection commit (item click, date pick, color confirm) — NOT on
    intermediate hover/keyboard navigation.
  - **Toggle inputs** (Checkbox, Switch, RadioGroup): on the change event.
- If a non-string value needs a converter, expose the convention via an
  analyzer-friendly parameter (e.g. `Format` / `Culture`); never silently
  parse with the invariant culture.
- Internal field that holds the in-flight buffer (before commit) is
  named `_pending` or `_buffer`, never `Value` itself — `Value` is the
  consumer's source of truth.

### Per-component "gotchas" metadata (#87.5)

When a component has non-obvious default behaviour, surface it in the MCP /
skill registry so AI-assisted consumers don't trip over it. This is a live,
supported convention — `Lumeo.RegistryGen` extracts it automatically.

- Author one-line callouts as `<gotcha>…</gotcha>` anywhere in the `.razor`
  file — typically inside the leading `@* … *@` comment block (e.g.
  `<gotcha>SheetContent has no inner scroll container by default — wrap a
  long body in flex-1 overflow-y-auto, or use OverlayForm.</gotcha>`),
  keeping the source of truth in the `.razor` file.
- Match the line `<gotcha>...</gotcha>` exactly; the inner text is extracted
  and trimmed. Content may span multiple lines (the matcher is singleline).
- `Lumeo.RegistryGen` lifts every gotcha into a `gotchas[]` array on the
  component's `registry.json` entry and its `components-api.json` entry
  (empty array when a component declares none). The
  `sync-mcp-registry.yml` workflow regenerates both on push to `master`, so
  don't hand-edit the JSON — just author the `<gotcha>` comment.

## API Stability & Deprecation

Every shipped library package (`Lumeo`, `Lumeo.Charts`, `Lumeo.CodeEditor`,
`Lumeo.DataGrid`, `Lumeo.Editor`, `Lumeo.FileViewer`, `Lumeo.Scheduler`,
`Lumeo.Gantt`, `Lumeo.Motion`, `Lumeo.PdfViewer`, `Lumeo.Maps`) carries a
`PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` pair, enforced by
[`Microsoft.CodeAnalysis.PublicApiAnalyzers`](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md)
(wired in `Directory.Build.targets` for every `/src/` project where
`IsPackable != false`, excluding the 16 `Lumeo.Icons.*` packs — see below).

**What the baselines enforce**

- `PublicAPI.Shipped.txt` is the public surface as of the last release.
  `PublicAPI.Unshipped.txt` holds surface added since then, pending the next
  release (moved into `Shipped.txt` at release time).
- Add a public type or member without recording it in one of these files ->
  **RS0016**, an error under the `-warnaserror` CI build.
- Remove or rename a public type or member that's still listed as shipped
  without going through the deprecation flow below -> **RS0017**.
- Both fire at compile time, in your own build — not just in CI — so a
  breaking change is visible before you open a PR, not after a consumer
  reports it.

**Updating the baselines when you change the API**

1. Add or change a public member as normal.
2. Build the affected project. The analyzer reports RS0016/RS0017 as
   warnings locally (they only become build-breaking errors under
   `-warnaserror`, which CI always uses).
3. Let the built-in code fix add the new member(s) to
   `PublicAPI.Unshipped.txt` (VS/Rider lightbulb -> "Add all items in the
   source to the public API", or from the CLI:
   `dotnet format analyzers src/<Project>/<Project>.csproj --diagnostics RS0016 --severity info --include-generated`
   — run it twice; the first pass can miss members whose diagnostics only
   surface once earlier fixes are compiled in).
4. Commit the updated `PublicAPI.Unshipped.txt` alongside your change. Do
   **not** hand-edit `PublicAPI.Shipped.txt` — a release step moves
   `Unshipped.txt` entries into `Shipped.txt` in lockstep with the version
   bump.

**Removing or changing a public member — the `[Obsolete]`-one-minor-then-major policy**

Lumeo's packages are versioned in lockstep (`Directory.Build.props`). Never
delete or change the signature of a shipped public member directly:

1. Mark it `[Obsolete("Use X instead.")]` (non-erroring) in the **next minor**
   release, and add the *new* replacement member alongside it. Update
   `PublicAPI.Unshipped.txt` for both (the obsolete attribute is part of the
   member's declared signature the analyzer tracks).
2. Keep the obsolete member working for at least one full minor release cycle
   so consumers have a version where the compiler warns but nothing breaks.
3. Remove the obsolete member only in the **next major** release, and record
   the removal in `PublicAPI.Unshipped.txt` (satisfying RS0017) and in
   `CHANGELOG.md` under a "Breaking changes" heading.

**Why the 16 `Lumeo.Icons.*` packs are excluded**

Their public surface is machine-generated — one record per upstream icon
glyph, ~51k members combined across all packs — and append-only by
construction: an icon-set refresh only *adds* members, it never renames or
removes one, so there's no accidental-breakage risk for the analyzer to
catch. Hand-maintaining (or even auto-fixing) a 51k-line baseline per pack on
every icon refresh would dwarf the signal it provides. `Lumeo.DataGrid.Export`
and `Lumeo.SourceGenerators` are excluded for a different reason: both set
`IsPackable=false` — they're internal helper assemblies bundled into another
package's `.nupkg` (`Lumeo.DataGrid.nupkg` and `Lumeo.nupkg` respectively),
not NuGet packages consumers reference directly.

Two analyzer sub-rules are suppressed repo-wide on tracked projects (see the
comment in `Directory.Build.targets` for the full rationale): **RS0041**
(nullable-oblivious member) fires on every Razor component's generated
`BuildRenderTree` override because the Razor source generator always emits
`#nullable disable` in its `.g.cs` output — a framework artifact, not a real
API-nullability risk. **RS0026/RS0027** (optional-parameter overload rules)
flag a handful of already-shipped overload sets (e.g. `Show`,
`RegisterAsync`) that predate this gate; "fixing" them now would itself be a
breaking signature change, which is exactly what this gate exists to
prevent doing by accident.

## Pull Request Guidelines

- Keep PRs focused — one feature or fix per PR.
- Include bUnit tests for any new components.
- Ensure `dotnet build` passes before submitting.
- Update the relevant docs page if you add or change a component's API.
- Do not include generated files or IDE-specific config in your PR.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
