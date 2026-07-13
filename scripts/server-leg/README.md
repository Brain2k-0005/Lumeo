# Blazor Server latency leg

Drives `tests/Lumeo.Tests.ServerHost` — a minimal **interactive-SERVER**
Blazor host (real SignalR circuit, not WASM/bUnit) — with artificial
round-trip latency injected via CDP network throttling, and exercises the
interaction-heavy components (DataGrid drag/resize, Toast, DatePicker,
Dialog) under it.

## Run

```bash
cd scripts/server-leg && npm install   # once — also fetches Playwright's Chromium
node run.mjs               # 200ms RTT (default; builds the host on the fly)
node run.mjs --rtt=500     # heavier RTT
```

`node run.mjs` builds `tests/Lumeo.Tests.ServerHost` (Debug, host's native
architecture) itself before launching it, so the leg always exercises the
checkout it's run against. Set `SERVERLEG_NO_BUILD=1` to reuse an
already-built output instead (e.g. a CI job that pre-builds in an earlier
step) — only do this when you're certain the existing build matches the
commit under test. Set `SERVERLEG_ARCH` (e.g. `=x64`) to pin `dotnet run` to
a specific target architecture instead of the host's native one; leave it
unset on ARM64 hosts (Apple Silicon, ARM Linux CI) since forcing x64 there
requires an x64 runtime/emulation layer to be installed.

## Why CDP throttling, not a server-side delay middleware

Both are wired (see `Program.cs`'s `LUMEO_SERVERLEG_DELAY_MS`), but this
harness uses CDP's `Network.emulateNetworkConditions` because it delays
every individual frame on the WebSocket the circuit uses, in both
directions. A delay middleware only slows the initial HTTP negotiate/upgrade
— once the socket is open, render-batch and event-dispatch frames flow
through with zero added latency, so it can't reproduce "a drag commit's
round-trip takes 200ms", which is exactly the bug class (stuck transforms,
settle-timer/round-trip races) this leg exists to catch.

## Scenarios

1. **DataGrid column drag commit under 200ms RTT** — drags column A onto
   column B's header, waits past several RTTs for the commit to land, and
   asserts no inline `transform` is left stuck on any header/cell.
2. **Toast burst (cap invariant)** — fires 8 toasts, asserts at least one is
   visible and the visible count never exceeds `MaxToasts=5`.
3. **Dialog open/close** — opens, closes, and waits for the dialog content
   to actually detach from the DOM (driven by the real
   `OnExitAnimationEnd` interop round-trip, not a blind timer).

## Fixed product bug: Toast under Blazor Server (issue #363, closed)

**Scenario 2 now passes — a hard `assert()`, not loosened.** This leg
originally surfaced a real, reproducible bug: `ToastProvider` never rendered
a toast under a genuine interactive-server circuit.

**Root-caused, not just observed (original diagnosis):**
- `ToastService.Show()` executed and returned a valid id — confirmed via a
  second, independent subscriber added directly in `Home.razor`
  (`Toast.OnShow += ...`), which *did* fire for every call.
- `ToastProvider.HandleShow` is subscribed to the same event and calls
  `SafeAsyncDispatcher.FireAndForget(InvokeAsync, () => HandleShowAsync(...))`
  (see `src/Lumeo/UI/Toast/ToastProvider.razor` and
  `src/Lumeo/Services/SafeAsyncDispatcher.cs`).
- Captured raw WebSocket frames on the `_blazor` circuit (Playwright's
  `page.on('websocket')` / `framereceived`) showed **zero new frames
  arriving** after the burst click — a server-side dispatch failure, not a
  slow/lost client update.
- Isolated to Toast specifically: Dialog's `@bind-Open` and DatePicker's
  popover both worked correctly on the same page/circuit. Only the
  cross-component **provider + fire-and-forget-event** pattern (Toast's
  architecture) failed.

**Fixed by the toast admission rework (#357):** the canonical
`TryAdmit`/`ReconcileGroup` admission model with synchronous `Leaving`
stamps and a capped pending queue. Re-verified fixed on master (post-#357
merge) via this leg at 200ms RTT: after a burst of 8, 5 toasts are visible,
the `MaxToasts=5` cap invariant holds, and there is no renderer crash — all
8 server-leg assertions pass (7 PASS baseline + this scenario). The scenario
had been flagged `assertKnownBroken()`/XFAIL since this leg's introduction;
it flipped to XPASS after #357 and has now been promoted to a plain
`assert()` in `run.mjs` so a future regression here fails the leg for real.

**Bonus finding from the original investigation — not just cosmetic:**
`run.mjs` deliberately runs the Dialog scenario *before* the Toast scenario.
With Toast first (pre-fix), the 8 fire-and-forget `SafeAsyncDispatcher`
dispatches that never resolved left the circuit unable to complete Dialog's
(unrelated) open round-trip within any reasonable timeout under RTT
throttling. The ordering is left as-is since it no longer matters
functionally (both scenarios pass either way post-fix) but doesn't hurt.
