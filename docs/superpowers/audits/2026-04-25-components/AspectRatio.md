# AspectRatio

**Path:** `src/Lumeo/UI/AspectRatio/`
**Class:** Container
**Files:** AspectRatio.razor

## Contract — WARN
- `@attributes="AdditionalAttributes"` is on the outer `<div>` (correct root element).
- Class param present, applied to inner div only — outer div class is hardcoded `"relative w-full"`. Inner div gets `CssClass`. This means AdditionalAttributes land on outer div but Class on inner; minor inconsistency.

## API — OK
- Container class: ChildContent present. Size not applicable (ratio controlled by `Ratio` double param).

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/AspectRatioPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (as "Aspect Ratio")

## CLI — OK
- Registry entry: present (key: aspect-ratio)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (no component deps)
