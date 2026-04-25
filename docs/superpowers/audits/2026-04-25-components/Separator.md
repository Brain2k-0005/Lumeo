# Separator

**Path:** `src/Lumeo/UI/Separator/`
**Class:** Display
**Files:** Separator.razor

## Contract — OK
- `@namespace Lumeo` as first line.
- `Class` + `AdditionalAttributes` present.
- `@attributes="AdditionalAttributes"` on root element (conditional branches — all present).
- No raw color literals. No `dark:` prefix.
- No icons used.

## API — OK
- Has `Orientation` enum (Horizontal/Vertical), `ChildContent`, `Class`, `AdditionalAttributes`.
- Display class: no `Size`/`Variant` needed for this primitive.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/SeparatorPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`separator`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: none — correct
