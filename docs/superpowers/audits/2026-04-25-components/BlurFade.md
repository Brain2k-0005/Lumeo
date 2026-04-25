# BlurFade

**Path:** `src/Lumeo/UI/BlurFade/`
**Class:** Other
**Files:** BlurFade.razor

## Contract — OK
- All checks pass. IAsyncDisposable implemented, JSDisconnectedException caught in OnAfterRenderAsync, ComponentInteropService used.

## API — OK
- All class-required parameters present. (ChildContent, DelayMs, DurationMs, BlurPx, Yoffset, Once, Class, AdditionalAttributes)

## Bugs — OK
- No findings.

## Docs — WARN
- Page: No dedicated `BlurFadePage.razor` — content is in `MotionPage.razor` (shared with BorderBeam etc).
- 2 ComponentDemo blocks (for BlurFade in MotionPage)
- API Reference: present (in MotionPage)
- Indexed in ComponentsIndex.razor: no (not listed)

## CLI — OK
- Registry entry: present (key: blur-fade)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (no component deps)
