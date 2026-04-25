# PasswordInput

**Path:** `src/Lumeo/UI/PasswordInput/`
**Class:** Form input
**Files:** PasswordInput.razor

## Contract — OK
- `@namespace Lumeo` present. Has `Class`, `AdditionalAttributes`.
- Note: `@attributes="AdditionalAttributes"` is applied to the inner `<input>` element (not the wrapper `<div>`) — functionally correct for pass-through of input attrs like `autocomplete`, `name`, etc.
- No raw color literals. No `dark:` prefix.
- Uses `<Blazicon>` for Eye/EyeOff icons.

## API — WARN
- Has `Value` + `ValueChanged`, `Disabled`, `Placeholder`. Good core.
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`, `ReadOnly`.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/PasswordInputPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (not listed in ComponentsIndex)

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: none (Blazicons used but packageDependencies not emitted — structural gap in registry generator, noted once globally)
