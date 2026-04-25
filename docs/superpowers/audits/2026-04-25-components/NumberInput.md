# NumberInput

**Path:** `src/Lumeo/UI/NumberInput/`
**Class:** Form input
**Files:** NumberInput.razor

## Contract — OK
- `@namespace Lumeo` present. Has `Class`, `AdditionalAttributes`, `@attributes` on root element.
- No raw color literals. No `dark:` prefix.
- Uses `<Blazicon>` for Minus/Plus icons.

## API — WARN
- Has `Value` + `ValueChanged`, `Disabled`. Good.
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`, `Placeholder`, `ReadOnly`, `MaxLength`.
- For a numeric stepper these are less critical than for text inputs, but class rules require them.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/NumberInputPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (not listed in ComponentsIndex)

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: none (none referenced)
