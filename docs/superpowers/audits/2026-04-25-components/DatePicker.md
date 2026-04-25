# DatePicker

**Path:** `src/Lumeo/UI/DatePicker/`
**Class:** Form input
**Files:** DatePicker.razor, DateRangePicker.razor

## Contract — OK
- All checks pass. Both files: `@namespace Lumeo`, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root.
- No raw colors, no `dark:` prefixes.
- Icons via `<Blazicon>` (Lucide.Calendar, Lucide.X).

## API — WARN
- Missing `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name` (standard form-input params)
- Has: `Value`+`ValueChanged`, `Disabled`, `Placeholder`, `Format`, `Culture`
- DateRangePicker wraps DatePicker; missing same form-level params

## Bugs — OK
- No `async void`, no direct IJSRuntime, no discarded Tasks in lifecycle.
- Overlay JS delegated entirely to Popover — no direct cleanup needed.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/DatePickerPage.razor` (exists)
- 11 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`date-picker`)
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: OK (calendar, popover, time-picker listed)
