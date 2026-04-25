# Delta

**Path:** `src/Lumeo/UI/Delta/`
**Class:** Display
**Files:** Delta.razor

## Contract — WARN
- `@namespace Lumeo`, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root — all present.
- No raw hex/rgb/hsl literals.
- Inline SVG icon blocks (< 3 lines each) — no `<Blazicon>` used; acceptable for tiny inline arrows.
- WARN: `bg-emerald-500/15 text-emerald-600` and `bg-rose-500/15 text-rose-600` are hardcoded semantic-color utilities, not CSS variables; violates "all colors via CSS vars" convention.

## API — OK
- Display class; `Size` and `Variant` are not applicable (component uses `Format` and `Positive` enums instead).
- Present: `Value`, `Format`, `Positive`, `ShowArrow`, `Class`, `AdditionalAttributes`.

## Bugs — OK
- No findings.

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/DeltaPage.razor` (MISSING)
- 0 ComponentDemo blocks
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`delta`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none needed)
