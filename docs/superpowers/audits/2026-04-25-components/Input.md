# Input

**Path:** `src/Lumeo/UI/Input/`
**Class:** Form input
**Files:** Input.razor

## Contract — WARN
- `@namespace Lumeo` present
- Has `Class` and `AdditionalAttributes` params
- `@attributes="AdditionalAttributes"` on root input, but NOT on wrapper div when Prefix/Suffix active — wrapper branch lacks `@attributes`
- No raw color literals, no `dark:` prefix
- Icons via `<Blazicon Svg="Lucide.X">` — correct

## API — WARN
- Form input class: Value + ValueChanged present; Disabled present; Size present; Prefix/Suffix slots present
- Missing: Required, Invalid, ErrorText, HelperText, Label, Name, Placeholder (exposed via `@attributes` passthrough only), ReadOnly, MaxLength
- Many form-input params absent; reliance on `AdditionalAttributes` for type/placeholder is a valid design choice but Required/Invalid/ErrorText are genuinely absent

## Bugs — OK
- No `async void`, no direct IJSRuntime
- No discarded Tasks in lifecycle

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/InputPage.razor` (exists)
- 8 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none; Blazicons structural gap)
