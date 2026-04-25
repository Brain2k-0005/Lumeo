# Steps

**Path:** `src/Lumeo/UI/Steps/`
**Class:** Other
**Files:** Steps.razor, StepsItem.razor

## Contract — OK
- Both files have `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes="AdditionalAttributes"`.
- No raw hex/hsl. No `dark:` prefix.
- Uses `<Blazicon Svg="Lucide.Check" />` and `<Blazicon Svg="Lucide.X" />` in StepsItem.

## API — OK
- For Other class: has `CurrentStep`/`CurrentStepChanged` (two-way binding), `Clickable`, `Orientation`, `Animated`, plus StepsItem has `Title`, `Description`, `Status`, `IconContent`, `Class`.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/StepsPage.razor` (exists)
- 8 ComponentDemo blocks (via grep count)
- API Reference: present (via grep count)
- Indexed in ComponentsIndex.razor: no

## CLI — WARN
- Registry entry: present (`steps`)
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: missing `Blazicons.Lucide` package dep (registry-gen does not emit packageDependencies)
