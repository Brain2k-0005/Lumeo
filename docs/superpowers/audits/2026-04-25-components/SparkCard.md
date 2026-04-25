# SparkCard

**Path:** `src/Lumeo/UI/SparkCard/`
**Class:** Display
**Files:** SparkCard.razor

## Contract — OK
- All checks pass.

## API — WARN
- `Size` parameter absent (Display class requires it where applicable); Variant absent too.
- Has `Label`, `Value`, `Data`, `ChildContent`, `Class`, `AdditionalAttributes`.

## Bugs — OK
- No findings.

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/SparkCardPage.razor` (MISSING)
- 0 ComponentDemo blocks
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`spark-card`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none declared; uses inline SVG polyline, no Blazicon)
