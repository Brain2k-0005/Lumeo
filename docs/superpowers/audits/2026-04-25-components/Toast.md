# Toast

**Path:** `src/Lumeo/UI/Toast/`
**Class:** Overlay
**Files:** Toast.razor, ToastAction.razor, ToastClose.razor, ToastDescription.razor, ToastProvider.razor, ToastTitle.razor, ToastViewport.razor

## Contract — WARN
- `ToastProvider.razor` implements `IAsyncDisposable` — OK.
- No `JSDisconnectedException` caught in ToastProvider (no JS interop calls directly; uses ToastService events). Not an issue — no JS interop to disconnect from.
- No direct `IJSRuntime` — OK.
- All sub-components have `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes` on root — OK.
- `ToastProvider` subscribes `ToastService.OnShow += HandleShow`, `OnDismiss`, `OnUpdate` — unsubscribed in `DisposeAsync()` — OK.
- Multiple `_ = InvokeAsync(...)` and `_ = DismissAfterDelayAsync(...)` fire-and-forget patterns in lifecycle event handlers. These are acceptable Blazor patterns for event-driven async dispatch (not lifecycle methods). Minor WARN.

## API — WARN
- Toast (display): `Variant`, `ChildContent`, `Class` — OK.
- ToastProvider (the overlay manager): no `Open`/`OpenChanged` or `Disabled` (managed via service, not parameters). Pattern differs from standard overlay API.
- Missing overlay API: `OnOpen`, `OnClose`, `Disabled` — by design (service-driven) but checklist flags it.

## Bugs — WARN
- `_ = InvokeAsync(...)` used in 6 places in ToastProvider for service event handlers. These are in non-lifecycle event callbacks, not `OnInitialized` itself — acceptable but discarded task pattern is present.
- `await Task.Delay(ExitAnimationMs)` in `RemoveWithExitAsync` without cancellation token could delay disposal if component is torn down mid-animation.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ToastPage.razor` (exists)
- 8 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (`toast`)

## CLI — OK
- Registry entry: present (`toast`)
- Files declared: 7 of 7
- Missing from registry: none
- Component deps declared: OK (none listed; ToastService is injected separately)
