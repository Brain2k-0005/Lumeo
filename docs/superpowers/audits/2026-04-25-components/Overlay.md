# Overlay

**Path:** `src/Lumeo/UI/Overlay/`
**Class:** Other (service-backed overlay orchestrator)
**Files:** OverlayProvider.razor

## Contract — WARN
- `@namespace Lumeo` present.
- No `[Parameter] public string? Class` — root element is a fragment (@foreach), no single root; `Class` not applicable here.
- No `AdditionalAttributes` — same reason; composite host component.
- Implements IAsyncDisposable (yes). No direct IJSRuntime. No ComponentInteropService (delegates to Dialog/Sheet/Drawer sub-components).
- Event subscriptions `OverlayService.OnShow +=` and `OnClose +=` unsubscribed in DisposeAsync — correct.

## API — OK
- This is a programmatic overlay service host, not a directly parameterised overlay. API is via OverlayService injection. Contract doesn't map to the standard Overlay parameter checklist.

## Bugs — WARN
- Lines 96, 108: `_ = InvokeAsync(...)` discards Task from non-async event handler callbacks. Standard Blazor pattern for thread-marshal calls but Tasks are not awaited; exceptions would be silently swallowed.

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/OverlayPage.razor` (MISSING)
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no (not in index as standalone — "Overlay" group in index refers to category, not this component)

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: `alert-dialog`, `button`, `dialog`, `drawer`, `sheet` — all correct
