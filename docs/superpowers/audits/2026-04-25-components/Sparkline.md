# Sparkline

**Path:** `src/Lumeo/UI/Sparkline/`
**Class:** Display
**Files:** Sparkline.razor

## Contract — OK
- All checks pass. SVG shapes built via C# string interpolation with CSS var colors; no raw hex/hsl in source.

## API — OK
- Display class: has `Height` (serves as Size), `Type` (Variant), `Values`, `Color`, `ShowArea`, `ShowLast`, `StrokeWidth`, `AriaLabel`, `Class`, `AdditionalAttributes`.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/SparklinePage.razor` (exists)
- 6 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (not listed in _componentGroups)

## CLI — OK
- Registry entry: present (`sparkline`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none needed)
