# DateTimePicker

**Path:** `src/Lumeo/UI/DateTimePicker/`
**Class:** Form input
**Files:** DateTimePicker.razor

## Contract — OK
- `@namespace Lumeo`, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root button.
- No raw colors, no `dark:` prefixes.
- Icons via `<Blazicon>` (Lucide.Calendar, Lucide.X).

## API — WARN
- Missing `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name` (standard form-input params)
- Has: `Value`+`ValueChanged`, `Disabled`, `Clearable`, `Placeholder`, `DateFormat`, `Culture`, `Use24Hour`, `ShowSeconds`

## Bugs — OK
- No `async void`, no direct IJSRuntime.
- Overlay JS delegated to Popover — no direct cleanup needed.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/DateTimePickerPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`date-time-picker`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (calendar, popover listed)
