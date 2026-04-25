# Combobox

**Path:** `src/Lumeo/UI/Combobox/`
**Class:** Form input (Overlay)
**Files:** Combobox.razor, ComboboxContent.razor, ComboboxCreate.razor, ComboboxEmpty.razor, ComboboxInput.razor, ComboboxItem.razor

## Contract — WARN
- `Combobox.razor` (root wrapper) is missing `[Parameter] public string? Class { get; set; }` — the `class="relative"` is hardcoded and `Class` param is absent (Combobox.razor:18).
- All other files have `Class` and `AdditionalAttributes`.
- Overlay checks: `ComboboxContent` implements `IAsyncDisposable`, catches `JSDisconnectedException`, uses `ComponentInteropService`. Root `Combobox.razor` implements `IDisposable` (not Async) — acceptable since it only disposes a debounce timer.

## API — WARN
- `Value` + `ValueChanged`, `IsOpen` + `IsOpenChanged`, `Multiple`, `Values` + `ValuesChanged` present.
- Missing: `Disabled`, `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ComboboxPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (key `combobox`)
- Files declared: 6 of 6
- Missing from registry: none
- Component deps declared: `spinner` declared; OK
