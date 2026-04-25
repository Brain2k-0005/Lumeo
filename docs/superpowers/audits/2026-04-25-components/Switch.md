# Switch

**Path:** `src/Lumeo/UI/Switch/`
**Class:** Form input
**Files:** Switch.razor

## Contract — OK
- `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes="AdditionalAttributes"` all present.
- No raw hex/hsl. No `dark:` prefix.
- Uses `<Spinner>` component (no direct Blazicon usage).

## API — FAIL
- Form input class requires: `Disabled`, `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`, `Value`/`ValueChanged`.
- Present: `Disabled`, `Label` (accessible aria only), `Checked`/`CheckedChanged` (value pattern with different name).
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Name` (5 missing).

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/SwitchPage.razor` (exists)
- 12 ComponentDemo blocks (via grep)
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (Form group)

## CLI — OK
- Registry entry: present (`switch`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: `spinner` dep declared — OK
