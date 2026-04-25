# ShimmerButton

**Path:** `src/Lumeo/UI/ShimmerButton/`
**Class:** Trigger
**Files:** ShimmerButton.razor

## Contract — WARN
- `@namespace Lumeo` as first line. `Class` + `AdditionalAttributes` present.
- `@attributes="AdditionalAttributes"` on root button.
- No raw color literals (ShimmerColor is a CSS variable/user-supplied string, not hardcoded).
- No `dark:` prefix.
- No explicit icon use, though Blazicon not needed here.
- Does NOT implement `IAsyncDisposable` — uses JS interop (Ripple) in `OnAfterRenderAsync` but no cleanup of the ripple listener registered.

## API — OK
- Trigger class: `Disabled`, `Size`, `Variant`, `OnClick` all present.
- Extra: `Shimmer`, `ShimmerColor`, `PressEffect` — good additions.

## Bugs — WARN
- `OnAfterRenderAsync` guarded by `if (firstRender && PressEffect == Ripple)` — OK.
- `Interop.RippleAttachAsync(_el)` called but no corresponding detach/dispose — potential listener leak if component unmounts with ripple active.
- `IAsyncDisposable` not implemented; ripple JS handler may persist.

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/ShimmerButtonPage.razor` (MISSING)
- 0 ComponentDemo blocks
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — WARN
- Registry entry: present (`shimmer-button`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: none — missing `button` dep (uses `Button.ButtonVariant`, `Button.ButtonSize` enum types)
