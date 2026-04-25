# TagInput

**Path:** `src/Lumeo/UI/TagInput/`
**Class:** Form input
**Files:** TagInput.razor

## Contract — OK
- `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes="AdditionalAttributes"` all present.
- No raw hex/hsl. No `dark:` prefix.
- Uses `<Blazicon Svg="Lucide.X" />` for tag remove button.
- Implements `IAsyncDisposable`; catches `JSDisconnectedException`; uses `ComponentInteropService`.

## API — FAIL
- Form input class requires: `Disabled`, `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`, `Value`/`ValueChanged`.
- Present: `Disabled`, `Tags`/`TagsChanged` (value pattern), `Placeholder`, `MaxTags`, `AllowDuplicates`, `Suggestions`.
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name` (6 missing).

## Bugs — WARN
- `OnAfterRenderAsync` calls `Interop.PositionFixed` / `RegisterClickOutside` outside `firstRender` guard — intentional pattern (conditional on `_showSuggestions`), not a bug. However it runs on every render cycle when suggestions are shown, which could cause redundant JS calls. Low severity.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/TagInputPage.razor` (exists)
- 6 ComponentDemo blocks (via grep)
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — WARN
- Registry entry: present (`tag-input`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: `list` dep declared but `Blazicons.Lucide` package dep missing (registry-gen does not emit packageDependencies)
