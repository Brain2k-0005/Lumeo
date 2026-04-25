# NumberTicker

**Path:** `src/Lumeo/UI/NumberTicker/`
**Class:** Display
**Files:** NumberTicker.razor

## Contract — OK
- `@namespace Lumeo` present. Has `Class`, `AdditionalAttributes`, `@attributes` on root element.
- No raw color literals. No `dark:` prefix. No SVG icons.
- Implements IAsyncDisposable; uses ComponentInteropService; catches JSDisconnectedException in Tick().

## API — WARN
- Display class requires `Size` and `Variant` where applicable.
- No `Size` or `Variant` — this is a motion/display primitive with a single purpose; Size/Variant not applicable here. Acceptable gap.
- Has `Value`, `StartValue`, `DurationMs`, `Decimals`, `Prefix`, `Suffix`. API complete for its purpose.

## Bugs — OK
- JS interop (MotionTickNumber) called in OnAfterRenderAsync with proper firstRender guard on first call, and on `_initialized && !AreClose` guard on subsequent — correct.
- No findings.

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/NumberTickerPage.razor` (MISSING)
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: none (none referenced)
