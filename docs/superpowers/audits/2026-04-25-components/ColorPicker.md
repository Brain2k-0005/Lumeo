# ColorPicker

**Path:** `src/Lumeo/UI/ColorPicker/`
**Class:** Form input (Overlay)
**Files:** ColorPicker.razor

## Contract — WARN
- `rgba()` and `hsl()` literals used in inline styles (ColorPicker.razor:28-29, 149, 156) — these are functional color math for the HSV canvas gradient and alpha/luminance calculations, not theme colors. Acceptable for a color picker component.
- All other contract checks pass.
- Implements `IAsyncDisposable`; catches `JSDisconnectedException`; uses `ComponentInteropService`.

## API — WARN
- `Value` + `ValueChanged`, `Disabled`, `IsOpen` + `IsOpenChanged` present.
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name` (form input class requirements).

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ColorPickerPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (key `color-picker`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none required; Blazicons.Lucide not in registry — structural gap)
