# Watermark

**Path:** `src/Lumeo/UI/Watermark/`
**Class:** Display
**Files:** Watermark.razor

## Contract — OK
- All checks pass.
- `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes` on root — present.
- No raw color literals (SVG uses `fill="currentColor"` — correct). No `dark:` prefix.
- Inline `opacity` value uses `Opacity.ToString(InvariantCulture)` — CSS numeric value, not a color literal.

## API — OK
- Display component: `Text`, `ChildContent`, `Rotation`, `Gap`, `FontSize`, `Opacity`, `Class`, `AdditionalAttributes` — appropriate.
- `Size` and `Variant` not applicable for this component type.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/WatermarkPage.razor` (exists)
- 2 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (missing from ComponentsIndex)

## CLI — OK
- Registry entry: present (`watermark`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none required)
