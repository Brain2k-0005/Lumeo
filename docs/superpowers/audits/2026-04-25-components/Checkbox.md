# Checkbox

**Path:** `src/Lumeo/UI/Checkbox/`
**Class:** Form input
**Files:** Checkbox.razor

## Contract — OK
- All checks pass.
- Inline SVG icons are < 5 lines each and represent the check/indeterminate marks — not decorative Lucide icons; acceptable per checklist note.

## API — WARN
- `Checked` + `CheckedChanged`, `Disabled`, `Label` present.
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Name`, `Value` (string value for form submission). `IsIndeterminate` + `IsIndeterminateChanged` present as bonus.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/CheckboxPage.razor` (exists)
- 6 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (key `checkbox`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none required)
