# Transfer

**Path:** `src/Lumeo/UI/Transfer/`
**Class:** Other (Data/Form)
**Files:** Transfer.razor

## Contract — OK
- All checks pass.
- `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes` on root — present.
- No raw color literals. No `dark:` prefix.
- Uses `<Checkbox>` and `<Blazicon>` (Lucide icons) — correct.

## API — OK
- `SourceItems` + `SourceItemsChanged`, `TargetItems` + `TargetItemsChanged`, `OnChange`, `ShowSearch`, `SourceTitle`, `TargetTitle`, `Class`, `AdditionalAttributes` all present.
- Appropriate for a dual-list transfer component.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/TransferPage.razor` (exists)
- 2 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (missing from ComponentsIndex)

## CLI — WARN
- Registry entry: present (`transfer`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: `checkbox` listed — OK. `list` listed but no `<List>` usage found in source — spurious dep listed.
