# Blazor Server latency leg

Drives `tests/Lumeo.Tests.ServerHost` — a minimal **interactive-SERVER**
Blazor host (real SignalR circuit, not WASM/bUnit) — with artificial
round-trip latency injected via CDP network throttling, and exercises the
interaction-heavy components (DataGrid drag/resize, Toast, DatePicker,
Dialog) under it.

## Run

```bash
cd tests/Lumeo.Tests.ServerHost && dotnet build --arch x64          # once
cd scripts/server-leg && npm install                                 # once
node run.mjs               # 200ms RTT (default)
node run.mjs --rtt=500     # heavier RTT
```

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

## Confirmed product bug: Toast under Blazor Server

**Scenario 2 fails, by design of the assertion (not loosened).** Building
this leg surfaced a real, reproducible bug: `ToastProvider` never renders a
toast under a genuine interactive-server circuit.

**Root-caused, not just observed:**
- `ToastService.Show()` executes and returns a valid id — confirmed via a
  second, independent subscriber added directly in `Home.razor`
  (`Toast.OnShow += ...`), which *does* fire for every call.
- `ToastProvider.HandleShow` is subscribed to the same event and calls
  `SafeAsyncDispatcher.FireAndForget(InvokeAsync, () => HandleShowAsync(...))`
  (see `src/Lumeo/UI/Toast/ToastProvider.razor` and
  `src/Lumeo/Services/SafeAsyncDispatcher.cs`), which swallows lifecycle
  exceptions and logs anything else to `Console.Error` — nothing was ever
  logged.
- Captured raw WebSocket frames on the `_blazor` circuit (Playwright's
  `page.on('websocket')` / `framereceived`): **zero new frames arrive**
  after the burst click, for as long as 3 seconds. No render batch is ever
  sent — this is a server-side dispatch failure, not a slow/lost client
  update.
- Isolated to Toast specifically: the **same page**, same render-mode setup,
  same circuit — Dialog's `@bind-Open` (same-component two-way binding) and
  DatePicker's popover both work correctly and were verified interactively.
  Only the cross-component **provider + fire-and-forget-event** pattern
  (Toast's architecture) fails.
- Not a prerendering artifact: reproduces identically with
  `@rendermode="@(new InteractiveServerRenderMode(prerender: false))"`.

**Not fixed here** — per instructions, product issues are reported, not
hacked around from a test harness. `Home.razor`'s `BurstToasts` carries a
comment pointing back to this section. The assertion in `run.mjs` stays
honest (asserts real toast visibility, not "it didn't crash") so a future
fix in `ToastProvider`/`SafeAsyncDispatcher` is provable by this scenario
flipping to PASS.

**Bonus finding — it's not just cosmetic:** `run.mjs` deliberately runs the
Dialog scenario *before* the Toast scenario. With Toast first, the 8
fire-and-forget `SafeAsyncDispatcher` dispatches that never resolve left the
circuit unable to complete Dialog's (unrelated) open round-trip within any
reasonable timeout under RTT throttling — i.e. this isn't purely a "toasts
don't render" bug, it may also degrade the circuit's ability to process
later, unrelated interactive events. Worth confirming under a longer-running
session (this harness only observed it once, immediately downstream).

**Likely next step for whoever picks this up:** compare
`SafeAsyncDispatcher.FireAndForget`'s dispatch path against a
known-working Blazor Server cross-component `InvokeAsync` + `StateHasChanged`
pattern under a *real* circuit (this codebase's test suite is 100% WASM/bUnit
— bUnit's synchronous test renderer and WASM's single-threaded execution
both appear to mask whatever this is; it needed a real SignalR circuit to
surface).
