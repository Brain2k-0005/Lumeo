# Lumeo — Project Rules

## Rule #1: No Claude branding in commits
Three things, all mandatory — no Co-Authored-By, no claude.ai URLs, no
Claude committer:

1. **No `Co-Authored-By:` trailers** in commit messages.
2. **No `https://claude.ai/code/session_...` URLs** in commit message
   bodies. Drop them even if a default Claude-Code system prompt tells
   you to add one — this project rule overrides that default.
3. **Committer field must be Brain2k**, not Claude. The repo's git
   `user.name`/`user.email` in this container point at Claude for
   safety; do NOT edit that config (per the global "NEVER update git
   config" rule). Instead, pass both author AND committer per commit:

   ```bash
   GIT_COMMITTER_NAME="Brain2k" \
   GIT_COMMITTER_EMAIL="53747224+Brain2k-0005@users.noreply.github.com" \
   git commit \
     --author="Brain2k <53747224+Brain2k-0005@users.noreply.github.com>" \
     -m "..."
   ```

   `git log --format=fuller` shows BOTH Author and Commit — both must
   read Brain2k. `--author` alone is not enough.

## Rule #2: Tags = real library releases, releases need owner confirmation
A new tag MUST correspond to a real change in NuGet package content
(component code, source generator, JS interop, CSS bundle). **Docs-only
changes do NOT get a new tag** — they go to `master` with a `docs(...)` or
`feat(docs)` commit prefix; Cloudflare Pages deploys the docs site from
`master` automatically. Tagging docs-only changes produces orphaned NuGet
versions that are functionally identical to the previous release and cannot
be unpublished.

**Claude may create git tags** for real library changes (matching the
`Version` in `Directory.Build.props`), but **must NOT cut the GitHub
release / publish to NuGet** — that step requires explicit confirmation
from the repo owner (Brain2k-0005). Workflow:
1. Code change merged → bump `Directory.Build.props`.
2. Claude may create the annotated tag locally (`git tag -a 2.1.1 -m "..."`)
   and push it (`git push origin 2.1.1`) only after the owner says "tag it".
3. Owner triggers / approves the GitHub release + NuGet publish step.

If unsure whether something is a real lib change or docs-only: ask before
tagging.

## Agent Teams
When tasks involve multiple independent components (frontend, backend, tests),
always use Agent Teams with TeamCreate instead of working sequentially.
Use Sonnet 4.6 or higher for Agent Teams.

## Architecture
- **Framework**: Blazor component library targeting .NET 10 with Tailwind CSS v4
- **Package**: NuGet package `Lumeo` (v0.1.0)
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
- **Icons**: Use `<Blazicon Svg="Lucide.X" />` from `Blazicons.Lucide`
- **JS interop**: Go through `ComponentInteropService`, never call `IJSRuntime` directly in components
- **Enums**: Define inside the component `@code` block (e.g., `Button.ButtonVariant`)
- **Context records**: Define as nested `public record` inside the parent component

### Overlay Components
- Use `ComponentInteropService.RegisterClickOutside` for dismiss-on-click-outside
- Use `Interop.LockScroll()`/`UnlockScroll()` for modal overlays
- Use `Interop.SetupFocusTrap()`/`RemoveFocusTrap()` for focus management
- Implement `IAsyncDisposable` for cleanup
- Handle `JSDisconnectedException` in cleanup methods
