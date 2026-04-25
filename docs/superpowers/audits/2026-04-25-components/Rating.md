# Rating

**Path:** `src/Lumeo/UI/Rating/`
**Class:** Form input
**Files:** Rating.razor

## Contract — OK
- `@namespace Lumeo` present. Has `Class`, `AdditionalAttributes`, `@attributes` on root element.
- No raw color literals. No `dark:` prefix.
- Uses `<Blazicon>` for Star icons.

## API — WARN
- Has `Value` + `ValueChanged`. Good.
- Has `ReadOnly`, `AllowHalf`, `Max`, `Size`, `IconContent`. Good feature set.
- Missing: `Disabled` (uses `ReadOnly` instead), `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`.
- `ReadOnly` vs `Disabled` semantics: ReadOnly disables interaction but `Disabled` (for form semantics) is absent.

## Bugs — OK
- No async void, no discarded Tasks, no event subscriptions without dispose.
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/RatingPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (not listed)

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: none (Blazicons.Lucide used — packageDependencies gap is global)
