# Result

**Path:** `src/Lumeo/UI/Result/`
**Class:** Display
**Files:** Result.razor

## Contract — OK
- `@namespace Lumeo` as first line.
- `Class` and `AdditionalAttributes` present, `@attributes="AdditionalAttributes"` on root.
- No raw color literals. No `dark:` prefix.
- Icons via `<Blazicon Svg="Lucide.*" />`.

## API — WARN
- Display class: no `Size` or `Variant` parameters (uses `Status` enum instead, reasonable).
- Has `Status`, `Title`, `SubTitle`, `IconContent`, `Extra`, `Class`, `AdditionalAttributes`.
- `Size`/`Variant` absent — minor gap for Display class.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ResultPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (not listed in the ComponentsIndex groups)

## CLI — WARN
- Registry entry: present (`result`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: none; uses `<Blazicon>` but `packageDependencies` absent (structural gap, registry-gen does not emit pkg deps)
