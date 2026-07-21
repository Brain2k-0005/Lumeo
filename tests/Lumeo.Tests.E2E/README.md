# Lumeo E2E (Playwright)

Smoke tests against a running docs dev-server that cover failure modes bUnit
can't reach: real focus traps, hover events, keyboard navigation, JS interop.

---

## Running locally

1. Start the docs site:
   ```
   dotnet run --project docs/Lumeo.Docs/Lumeo.Docs.csproj
   ```

2. Install Playwright browsers (first time only):
   ```
   pwsh tests/Lumeo.Tests.E2E/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
   ```
   Or on Linux/Mac: `playwright.sh install --with-deps chromium`

3. In another terminal, run the tests:
   ```
   dotnet test tests/Lumeo.Tests.E2E/Lumeo.Tests.E2E.csproj
   ```

Override the base URL if needed:
```
LUMEO_E2E_BASE_URL=http://localhost:5288 dotnet test tests/Lumeo.Tests.E2E/Lumeo.Tests.E2E.csproj
```

---

## Smoke tests

| File | What it covers |
|------|---------------|
| `Smokes/DialogFocusTrapTests.cs` | Dialog opens, focus stays inside (Tab cycles), Escape closes |
| `Smokes/DropdownKeyboardTests.cs` | DropdownMenu opens, arrow keys move focus, Escape closes |
| `Smokes/TooltipHoverTests.cs` | Tooltip shows on hover (real mouse events) |
| `Smokes/CatalogPageRendersTests.cs` | /components catalog renders ≥ 100 cards (registry sanity) |
| `Smokes/SearchPaletteTests.cs` | Ctrl+K opens palette, typing finds results, click navigates |

---

## Visual snapshots

Visual diff tests live in `Visual/`. To generate baselines:

```
LUMEO_E2E_UPDATE_SNAPSHOTS=1 dotnet test tests/Lumeo.Tests.E2E
```

Baselines are stored in `tests/Lumeo.Tests.E2E/Snapshots/` and should be
committed to the repo. Future iteration will switch from byte-equal to
perceptual diff (ImageSharp). Add per-page tests as the docs surface stabilizes.

---

## Gantt v2/v3 parity harness (feat/gantt-v3, T4)

`Gantt/*.cs` and `Visual/GanttParityVisualTests.cs` are a SEPARATE harness from
everything else in this project: they drive `tests/Lumeo.Tests.ServerHost` (a
Blazor Server host, real SignalR circuit, no WASM boot) instead of the docs
WASM site, via `/e2e/gantt-v2`, `/e2e/gantt-v3`, and `/e2e/gantt-v3-tree` — new
pages added to that project specifically for this harness, rendering the same
deterministic fixture (`tests/Lumeo.Tests.ServerHost/E2E/GanttParityFixtures.cs`)
through v2's `Gantt` (JS/SVG) and the working-name v3 `Gantt3` (plain Razor) so
their DOM output can be asserted for render-equivalence.

Because the base URL differs, these specs do NOT use `PlaywrightTestBase.Goto`/
`BaseUrl` (docs site, `LUMEO_E2E_BASE_URL`) — see `Gantt/GanttParityTestBase.cs`'s
remarks for why that property can't be overridden. They resolve their own base
URL from `LUMEO_GANTT_E2E_BASE_URL` (default `http://localhost:5299`).

Running locally:
```
dotnet run --project tests/Lumeo.Tests.ServerHost/Lumeo.Tests.ServerHost.csproj --urls http://localhost:5299
# in another terminal:
dotnet test tests/Lumeo.Tests.E2E/Lumeo.Tests.E2E.csproj --filter "FullyQualifiedName~Gantt"
```

Not yet wired into `.github/workflows/e2e.yml` — CI currently only starts the
docs server. See docs/superpowers/gantt-v3-t4-report.md for the CI follow-up.

## CI integration

Wired into CI via `.github/workflows/e2e.yml`, which runs on every push and
PR to `master`: it builds the CSS bundles, builds `Lumeo.Tests.E2E` (Release,
which also restores the Playwright assets), installs the Playwright Chromium
browser, and runs the tests against a docs server the workflow starts.

The test project also builds cleanly outside that workflow with a plain
`dotnet build` — only **running** the tests requires a docs dev-server and
Playwright browser binaries (see "Running locally" above).
