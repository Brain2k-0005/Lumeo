# TimePicker

**Path:** `src/Lumeo/UI/TimePicker/`
**Class:** Form input (Overlay)
**Files:** TimePicker.razor

## Contract — WARN
- Overlay checks: uses `<Popover>` child component for overlay behavior — IAsyncDisposable, JSDisconnectedException, and ComponentInteropService are handled by Popover internally, not directly in TimePicker.
- TimePicker itself does not implement `IAsyncDisposable` or catch `JSDisconnectedException` — delegates to Popover. Acceptable by composition but flagged per overlay checklist.
- No raw color literals. No `dark:` prefix. All contract params present.

## API — WARN
- Form input: `Value` + `ValueChanged` present, `Disabled` present, `Placeholder` present.
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name` — 6 form-input parameters absent.
- Missing `Open`/`OpenChanged` (uses internal `_isOpen` only — not two-way bindable by parent).

## Bugs — OK
- No `async void`, no direct IJSRuntime.
- JS interop deferred to Popover component.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/TimePickerPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (missing from ComponentsIndex)

## CLI — OK
- Registry entry: present (`time-picker`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (`popover` listed)
