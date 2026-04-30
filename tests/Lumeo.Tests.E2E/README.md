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

## CI integration

These tests are **not yet wired into CI by default** — they require:
1. A running docs server
2. Playwright browser binaries

When ready, create a separate GitHub Actions workflow that:
1. Builds and starts `Lumeo.Docs` in the background
2. Runs `playwright.ps1 install --with-deps chromium`
3. Runs the tests with `LUMEO_E2E_BASE_URL` pointing at the running server

The test project builds cleanly in CI without any of the above (just
`dotnet build`) — only **running** the tests requires the dev server.
