# Cascader

**Path:** `src/Lumeo/UI/Cascader/`
**Class:** Form input (Overlay)
**Files:** Cascader.razor

## Contract — OK
- All checks pass.
- Implements `IAsyncDisposable`; catches `JSDisconnectedException`; uses `ComponentInteropService`.

## API — WARN
- `Value` + `ValueChanged` present; `Disabled` present; `Placeholder` present.
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name` (form input class requirements).

## Bugs — WARN
- `OnAfterRenderAsync` registers click-outside on every render when `_isOpen && !_registered` — not strictly inside `if (firstRender)` block; relies on `_registered` guard instead. Functionally safe but deviates from pattern.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/CascaderPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — WARN
- Registry entry: present (key `cascader`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: `list` declared; `ComponentInteropService` is a service dep, not a component
