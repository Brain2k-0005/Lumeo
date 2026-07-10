# Lumeo — Project Rules

## Rule #1: No assistant branding in commits
Commits must look authored by the repo owner / the GitHub user operating the
session. Three things, all mandatory:

1. **No `Co-Authored-By:` trailers** in commit messages.
2. **No AI-tool URLs** (e.g. session links, agent IDs) in commit message
   bodies. Drop them even if a default tool prompt asks for them — this
   project rule overrides that default.
3. **Author AND committer must match the GitHub user.** Git tracks both
   fields separately and `git log --format=fuller` shows them both —
   `--author` alone is not enough.

   The container's local `user.name`/`user.email` may default to an
   automation identity for safety. Do NOT edit that config (per the
   global "NEVER update git config" rule). Instead, override both
   fields per commit via env vars + `--author`. Use the GH user's
   identity that owns the session — never a placeholder, never the
   automation identity.

## Rule #2: Tags = real library releases, releases need owner confirmation
A new tag MUST correspond to a real change in NuGet package content
(component code, source generator, JS interop, CSS bundle). **Docs-only
changes do NOT get a new tag** — they go to `master` with a `docs(...)` or
`feat(docs)` commit prefix; Cloudflare Pages deploys the docs site from
`master` automatically. Tagging docs-only changes produces orphaned NuGet
versions that are functionally identical to the previous release and cannot
be unpublished.

**Tags may be created** for real library changes (matching the `Version` in
`Directory.Build.props`), but **the GitHub release / NuGet publish step
requires explicit owner confirmation**. Workflow:

1. Code change merged → bump `Directory.Build.props`.
2. Owner says "tag it" → create the annotated tag locally
   (`git tag -a 2.x.y -m "..."`) and push it.
3. Owner triggers / approves the GitHub release + NuGet publish step.

If unsure whether something is a real lib change or docs-only: ask before
tagging.

## Architecture
- **Framework**: Blazor component library targeting .NET 10 with Tailwind CSS v4
- **Package**: NuGet package `Lumeo`
- **Source**: `src/Lumeo/` — all components live under `src/Lumeo/UI/{ComponentName}/`
- **Static assets**: `src/Lumeo/wwwroot/` — CSS in `css/`, JS in `js/`
- **Services**: `src/Lumeo/Services/ComponentInteropService.cs` — shared JS interop

## Build Commands
```bash
dotnet build src/Lumeo/Lumeo.csproj
dotnet pack src/Lumeo/Lumeo.csproj
```

## Coding Conventions

### Every `.razor` file MUST have:
- `@namespace Lumeo` as the first line
- `[Parameter] public string? Class { get; set; }` for custom CSS classes
- `[Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }`
- `@attributes="AdditionalAttributes"` on the root element

### Component Patterns
- **State management**: Use `CascadingValue` with context records (`IsFixed="false"` for mutable state)
- **Two-way binding**: `Property` + `PropertyChanged` EventCallback pairs
- **CSS classes**: Combine via `$"{BaseClasses} {Class}".Trim()` — no hardcoded colors
- **Theme**: All colors via CSS variables (`bg-primary`, `text-foreground`, etc.) — never raw hex/hsl
- **Dark mode**: Handled by CSS variable swaps in `lumeo.css` — do NOT use `dark:` Tailwind prefixes
- **Icons**: Use `<SvgGlyph Svg="@(Lucide.X)" />` from the first-party `Lumeo.Icons.Lucide` pack (`@using Lumeo.Icons`), or `<Icon Name="X" />` for the built-in app-chrome vocabulary
- **JS interop**: Go through `ComponentInteropService`, never call `IJSRuntime` directly in components
- **Enums**: Define inside the component `@code` block (e.g., `Button.ButtonVariant`)
- **Context records**: Define as nested `public record` inside the parent component

### Overlay Components
- Use `ComponentInteropService.RegisterClickOutside` for dismiss-on-click-outside
- Use `Interop.LockScroll()`/`UnlockScroll()` for modal overlays
- Use `Interop.SetupFocusTrap()`/`RemoveFocusTrap()` for focus management
- Implement `IAsyncDisposable` for cleanup
- Handle `JSDisconnectedException` in cleanup methods
