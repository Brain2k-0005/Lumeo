# BackToTop

**Path:** `src/Lumeo/UI/BackToTop/`
**Class:** Trigger
**Files:** BackToTop.razor

## Contract — OK
- All checks pass. IAsyncDisposable implemented, JSDisconnectedException caught, ComponentInteropService used.

## API — WARN
- Trigger class: `Disabled`, `Variant` params absent. `Size` absent.
- `OnClick` callback absent (scroll-to-top is implicit; no external click callback exposed).

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/BackToTopPage.razor` (exists)
- 2 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (not listed)

## CLI — OK
- Registry entry: present (key: back-to-top)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (no component deps)
