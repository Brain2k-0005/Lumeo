# Tour

**Path:** `src/Lumeo/UI/Tour/`
**Class:** Overlay
**Files:** Tour.razor

## Contract — WARN
- `rgba(0,0,0,0.5)` raw color literal in SVG overlay mask fill (line 20). This is not inside an `<svg>` icon path — it is a functional overlay backdrop. Violates no-raw-color rule.
- Implements `IAsyncDisposable` — OK.
- `JSDisconnectedException` caught in cleanup paths — OK.
- Uses `ComponentInteropService` (not direct IJSRuntime) — OK.
- No `dark:` prefix.

## API — WARN
- `IsOpen` + `IsOpenChanged` present — OK.
- `OnComplete` and `OnSkip` callbacks present.
- Missing `OnOpen` callback.
- Missing `Disabled` parameter.
- `Open`/`OpenChanged` convention: uses `IsOpen`/`IsOpenChanged` naming — minor inconsistency vs. standard `Open`/`OpenChanged`.

## Bugs — WARN
- `OnAfterRenderAsync` calls `Interop.LockScroll()` without `firstRender` guard — fires on every render when `IsOpen` is true. This can result in repeated lock calls. `UpdateTargetRect` has a selector-equality guard, but `LockScroll` does not.
- `OnParametersSetAsync` is `async` but always returns without awaiting — effectively sync; minor stylistic issue.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/TourPage.razor` (exists)
- 2 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (missing from ComponentsIndex)

## CLI — WARN
- Registry entry: present (`tour`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: missing — Tour uses `ComponentInteropService` and renders inline buttons/overlay. No component deps (no child Lumeo components referenced) — OK.
