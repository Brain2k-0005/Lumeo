# InplaceEditor

**Path:** `src/Lumeo/UI/InplaceEditor/`
**Class:** Form input
**Files:** InplaceEditor.razor

## Contract — WARN
- `@namespace Lumeo` present
- Has `Class` and `AdditionalAttributes` params
- Root `<div>` carries `@attributes="AdditionalAttributes"`
- No raw color literals, no `dark:` prefix
- Inline SVG icons used (checkmark and X in ShowButtons section, edit pencil icon in display mode) — not using `<Blazicon>` for these small inline SVGs (3 SVG blocks > 3 lines each, should use Blazicons)

## API — WARN
- Form input class: Value + ValueChanged present; Disabled present; Placeholder present
- Missing: Required, Invalid, ErrorText, HelperText, Label, Name, ReadOnly, MaxLength
- Missing 5+ form-input required params — FAIL threshold

## Bugs — OK
- No `async void`, no direct IJSRuntime
- `Task.Delay` not used
- No discarded Tasks

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/InplaceEditorPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none; no other Lumeo components referenced inside)
