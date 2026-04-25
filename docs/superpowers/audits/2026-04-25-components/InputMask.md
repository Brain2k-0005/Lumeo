# InputMask

**Path:** `src/Lumeo/UI/InputMask/`
**Class:** Form input
**Files:** InputMask.razor

## Contract — OK
- `@namespace Lumeo` present
- Has `Class` and `AdditionalAttributes` params
- Root `<input>` carries `@attributes="AdditionalAttributes"`
- No raw color literals, no `dark:` prefix
- No inline SVG blocks

## API — WARN
- Form input class: Value + ValueChanged present; Disabled present; Placeholder present; Mask param present
- Missing: Required, Invalid, ErrorText, HelperText, Label, Name, ReadOnly, MaxLength
- Missing 6+ form-input required params

## Bugs — OK
- No `async void`, no direct IJSRuntime
- Pure C# mask logic, no JS interop
- No discarded Tasks

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/InputMaskPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none)
