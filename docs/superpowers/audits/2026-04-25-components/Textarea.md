# Textarea

**Path:** `src/Lumeo/UI/Textarea/`
**Class:** Form input
**Files:** Textarea.razor

## Contract — OK
- All checks pass.

## API — WARN
- `Value` + `ValueChanged` present; `Disabled`, `MaxLength`, `Placeholder` (implicit via AdditionalAttributes) present.
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name` — 6 form-input parameters absent.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/TextareaPage.razor` (exists)
- 6 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`textarea`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none)
