# Toggle

**Path:** `src/Lumeo/UI/Toggle/`
**Class:** Trigger
**Files:** Toggle.razor

## Contract — OK
- All checks pass.
- `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes` on root element — all present.
- No raw color literals. No `dark:` prefix.

## API — OK
- `Disabled`, `Size`, `Variant` present. `Pressed` + `PressedChanged` (two-way binding) present.
- No `OnClick` parameter — uses `PressedChanged` pattern instead (appropriate for a stateful toggle).

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/TogglePage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (`toggle`)

## CLI — OK
- Registry entry: present (`toggle`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none)
