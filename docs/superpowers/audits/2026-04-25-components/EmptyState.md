# EmptyState

**Path:** `src/Lumeo/UI/EmptyState/`
**Class:** Display
**Files:** EmptyState.razor

## Contract — OK
- `@namespace Lumeo`, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root.
- No raw colors, no `dark:` prefixes.
- No icons (icon slot is `RenderFragment`).

## API — OK
- Display class; `Size`/`Variant` not applicable for this component.
- Present: `IconContent`, `Title`, `Description`, `Action`, `Class`, `AdditionalAttributes`.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/EmptyStatePage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`empty-state`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none needed)
