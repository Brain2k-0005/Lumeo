# scripts/sr-audit — NVDA automation for `docs/superpowers/sr-test-protocol.md`

Automates the **NVDA (Windows)** half of the screen-reader test protocol using
[Guidepup](https://www.guidepup.dev) (`@guidepup/guidepup` drives NVDA and
captures its spoken output; `@guidepup/setup` provisions a **portable** NVDA
with no system-wide install). VoiceOver (macOS) is out of scope here and
stays manual — Guidepup's VoiceOver driver needs a macOS host.

## Status: setup works, live run is blocked in this environment

- **Setup: done, confirmed working.** `npx @guidepup/setup` (via `npm run
  setup`) downloaded and registered a portable NVDA build with no admin
  rights and no system-wide install. See "What setup changed" below.
- **Live NVDA automation: blocked here by a foreground-focus problem, not
  a Guidepup/NVDA problem.** `check-env.mjs` reproduces and diagnoses this
  precisely. See "Known blocker" below — this is the honest failure report
  the task asked for.
- `run.mjs` is a complete, ready-to-run implementation of the top-5-component
  sweep. It was **not** run end-to-end against the real docs site in this
  session, because doing so on this machine right now would only produce
  empty/`FAIL` noise from the blocker below, not genuine signal about the
  components. A maintainer should run it after clearing the blocker (see
  "What to run locally instead").

## What setup changed

Running `npm install && npm run setup` in this directory:

1. Downloaded a zip of `guidepup/nvda` (a portable NVDA fork with Guidepup's
   remote-control addon baked in) to a **temp directory**
   (`%TEMP%\guidepup_nvda_<random>\nvda-<version>\nvda`) — not
   `Program Files`, not a system installer, nothing persistent outside
   `%TEMP%`.
2. Created **one registry key**: `HKCU:\Software\Guidepup\Nvda`, a single
   string value pointing at that temp path, so future Guidepup calls can
   find it. No `HKLM`, no elevation, no admin prompt.
3. Restarted `explorer.exe` (`removeForegroundLock` + `restartExplorer` in
   `@guidepup/setup`'s Windows path) — this is the one moderately invasive
   step: it briefly flashes the taskbar/desktop. It happened once during
   this session's `npm run setup` call.

Nothing under `Program Files`, no Windows service, no scheduled task. To
fully undo it: delete the `%TEMP%\guidepup_nvda_*` folder and the
`HKCU:\Software\Guidepup` registry key.

## Known blocker: OS foreground focus

NVDA's navigation commands (`next()`, arrow-key presses, etc.) read whatever
currently holds **real OS foreground focus** — not just whatever window is
merely open. `check-env.mjs` runs three checks and, in this session,
reproduced this exact failure on the second and third:

1. **NVDA starts via Guidepup.** PASS — process launches, Guidepup's remote
   channel connects (`nvda.start()` resolves without error).
2. **A navigation command produces captured speech.** FAIL — `nvda.next()`
   completed, but `nvda.lastSpokenPhrase()` came back empty.
3. **This process can win real foreground focus for a window it spawns
   itself.** FAIL, and this explains #2: even a brand-new maximized Edge
   window, freshly launched by this script, did **not** become the
   foreground window. `GetForegroundWindow()` (via a `user32.dll` P/Invoke
   from PowerShell) reported a pre-existing `Windows Security` dialog
   (`PickerHost.exe`) as the foreground window both before and after
   launching the browser — confirmed twice, including with an explicit
   `SetForegroundWindow()` call, which Windows' foreground-lock policy
   rejected (`returned False`).

This machine is a **live, shared interactive desktop** (session `console`,
state `Active`, user `mike`), not a disposable CI runner — `Get-Process`
during this session showed Discord, Spotify, the Claude Code terminal, and
that stuck `Windows Security`/`PickerHost` dialog all competing for the
desktop. Windows' foreground-lock protection (by design) refuses to let a
background-launched process steal focus from whatever the interactive user
last touched, which on this machine right now is that dialog. Since NVDA
reads "whatever has focus," the docs page never gets read regardless of how
correct the Guidepup calls are.

This is **not** the "audio/session constraints in a headless context" risk
the task anticipated — the session is fully interactive, not headless — it's
a **different, more mundane** foreground-focus contention that happens to
produce the same symptom (silence).

## What a maintainer must run locally instead

To get real PASS/PARTIAL/FAIL results:

1. Use a machine/session dedicated to this run — ideally a fresh interactive
   Windows sign-in (RDP session used only for this, or a Windows CI runner
   with no other GUI apps) so no other window can hold foreground focus.
   Close/resolve any pending Windows Security / credential dialogs first.
2. `cd scripts/sr-audit && npm install`
3. `npm run setup` (skips cleanly if NVDA is already registered).
4. `node check-env.mjs` — **all three checks must PASS** before proceeding.
   If check 3 fails, nothing downstream will produce real data; fix the
   foreground-focus contention (close whatever dialog/app is stuck in the
   foreground, or run in a session where this script is the only GUI actor)
   and re-run `check-env.mjs` until it's clean.
5. `npm run run` (`node run.mjs`) — builds `docs/Lumeo.Docs` (Release),
   boots it on `localhost:5292`, launches Edge (headed — NVDA needs a real
   visible window, not headless), starts NVDA, and walks the top-5
   components: **DataGrid, FileManager, Tabs, Calendar, Cascader** (see
   "Component selection" below). Writes `results/nvda-<date>.json`.
6. Everything the script starts (NVDA, the browser, the `dotnet run` docs
   server) is torn down at the end of `run.mjs`, mirroring what this session
   did manually.

## Component selection

The task brief named "Button, Dialog, Combobox, Tabs, DataGrid" as the
top-5 protocol components, but `docs/superpowers/sr-test-protocol.md` has
**no Button or Dialog rows at all** — its own ranked top-20 list (by
distinct `KeyboardEventArgs.Key` values handled, the protocol's stated
methodology) is DataGrid, FileManager, Tabs, Calendar, Cascader, ... This
script automates that actual top-5 instead, so every expected-announcement
check traces back to a real row in the protocol file rather than an
invented one.

## Coverage and limits of `run.mjs`

- Per component, `run.mjs` exercises a **representative subset** (3 rows,
  not the full protocol table per component) — enough to prove the
  mechanism works end-to-end and catch gross regressions, not a full
  substitute for a human walking every row. Extending `COMPONENTS` in
  `run.mjs` to cover more rows is mechanical (each entry is `{row, action,
  key, expected}`).
- `run.mjs` focuses the target widget directly via `element.focus()` +
  `document.querySelector` on an ARIA-role selector, instead of Tab-ing
  through the docs page's topbar/sidebar chrome first. This exercises the
  real widget's real keyboard handling faithfully; it does not also
  re-verify the page's outer Tab order (already covered elsewhere).
- Rows with no fixed expected text (e.g. "cell value announced" without a
  literal string to match) are logged as `LOGGED — "<actual>"` rather than
  scored PASS/FAIL — a human still has to eyeball those.
- Repeatability: deterministic given a stable environment — same portable
  NVDA build, same Edge, same docs build. The only nondeterminism observed
  in this session was the environment-level foreground-focus issue above,
  not anything in Guidepup/NVDA/Playwright themselves.
- VoiceOver/macOS stays manual, per the task brief — Guidepup's VoiceOver
  driver requires a macOS host, which this environment does not have.

## Files

- `package.json` — pins `@guidepup/guidepup`, `@guidepup/setup`,
  `playwright-core`.
- `check-env.mjs` — 3-step environment diagnostic; run before `run.mjs`.
- `run.mjs` — the full top-5-component NVDA sweep against the docs site.
- `results/` — `nvda-<date>.json` output lands here (none committed here —
  no genuine run completed in this session; see Status above).
