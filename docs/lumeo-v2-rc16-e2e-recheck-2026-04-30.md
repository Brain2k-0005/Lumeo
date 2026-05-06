# Lumeo v2-rc.16 E2E Recheck - 2026-04-30

Scope: gezielte Nachpruefung nach Aussage, dass E2E ausgebaut wurde und Tests wieder laufen.

## Ergebnis Kurz

Der normale CI-nahe Testlauf ist wieder gruen, weil E2E im Solution-Test explizit ausgefiltert wird. Das ist als kurzfristige Stabilisierung okay.

Ein echter E2E-Lauf gegen eine gestartete Docs-App ist lokal aber weiterhin nicht gruen. Nach Installation des zum Projekt passenden Playwright-Browsers laufen 2/16 E2E-Tests durch, 14/16 schlagen fehl. Damit ist "CI ist wieder gruen" korrekt, aber "E2E ist operational gruen" konnte ich nicht bestaetigen.

## Verifizierte Commands

```powershell
dotnet build Lumeo.slnx -c Release --no-restore
dotnet test Lumeo.slnx -c Release --no-build --verbosity minimal --filter "FullyQualifiedName!~Lumeo.Tests.E2E"
pwsh tests\Lumeo.Tests.E2E\bin\Release\net10.0\playwright.ps1 install chromium
dotnet run --project docs\Lumeo.Docs\Lumeo.Docs.csproj --urls http://localhost:5287
dotnet test tests\Lumeo.Tests.E2E\Lumeo.Tests.E2E.csproj -c Release --no-build --verbosity minimal
```

## Was jetzt gruen ist

- Build: passed, 0 warnings, 0 errors.
- Registry generation: 173 Items, 131 Komponenten, 16 Patterns, 8 Blocks, 18 Guides, alle Routen live verifiziert.
- CI-naher Testlauf mit E2E-Filter: passed.
- `Lumeo.Tests`: 2111 passed, 0 skipped.
- `Lumeo.Docs.Tests`: 20 passed.
- `Lumeo.RegistryGen.Tests`: 16 passed.
- Registry-vs-Docs/Test-Abgleich: 131/131 Komponenten mit Docs und direkt erkennbarem Test.

## Was nicht gruen ist

### E2E direkt: 14/16 failed

Nach Browser-Installation und gestarteter Docs-App:

```text
Failed: 14, Passed: 2, Skipped: 0, Total: 16
```

Hauptursachen:

- Visual baseline fehlt:

```text
Visual baseline missing at:
tests\Lumeo.Tests.E2E\Snapshots\home-above-fold.png
```

- Search palette Tests finden das erwartete Suchfeld nicht:

```text
Timeout 5000ms exceeded - waiting for Locator("input[placeholder*='Search']") to be visible
```

- Catalog Tests finden erwartete Component-Links nicht:

```text
waiting for Locator("a[href='components/button']") to be visible
waiting for Locator("a[href^='components/']").First to be visible
```

- Dialog/Dropdown/Tooltip Tests finden erwartete Demo-Trigger nicht:

```text
waiting for button text "Open Dialog"
waiting for button text "Open Menu"
waiting for tooltip trigger / Hover button
```

## Bewertung

Der letzte P1 "Solution-Test ist rot wegen fehlendem Playwright-Browser" ist fuer CI praktisch entschaerft, weil CI E2E ausfiltert:

```yaml
dotnet test Lumeo.slnx -c Release --no-build --verbosity normal --filter "FullyQualifiedName!~Lumeo.Tests.E2E"
```

Das ist als kurzfristiger Fix akzeptabel. Fuer v2-final ist E2E damit aber noch kein echtes Release-Gate. Das Projekt baut, aber die Tests beweisen aktuell nicht automatisch, dass die Docs-/Browser-Surface funktioniert.

## Konkrete Rest-Defects

### P1 - E2E ist nicht gruen, nur aus dem CI-Default ausgefiltert

Dateien:

- `.github/workflows/ci.yml`
- `tests/Lumeo.Tests.E2E/PlaywrightTestBase.cs`
- `tests/Lumeo.Tests.E2E/Smokes/*.cs`
- `tests/Lumeo.Tests.E2E/Visual/HomePageVisualTests.cs`

Risiko: CI bleibt gruen, obwohl reale Browser-Smokes stale sein koennen. Das ist besser als ein rotes Default-Gate, aber kein finaler Qualitaetsnachweis.

Fix-Idee: Separaten E2E-Workflow bauen, der:

- Playwright Chromium passend installiert;
- Docs-App startet;
- Healthcheck auf `LUMEO_E2E_BASE_URL` abwartet;
- E2E ausfuehrt;
- bei Visual Tests Baselines committed oder Visual Test bis Baseline existiert deaktiviert.

### P2 - E2E-Selektoren wirken stale gegen aktuelle Docs

Beispiele:

- `Open Dialog`
- `Open Menu`
- `input[placeholder*='Search']`
- `a[href='components/button']`

Risiko: Tests brechen bei harmlosen Copy/Layout-Aenderungen oder pruefen falsche Annahmen ueber Docs-Markup.

Fix-Idee: Docs-Seiten mit stabilen `data-testid`/`data-e2e` Attributen versehen und E2E darauf umstellen.

### P2 - Visual Test hat keine Baseline

Datei:

- `tests/Lumeo.Tests.E2E/Visual/HomePageVisualTests.cs`

Risiko: Der Test kann nie gruen sein, solange `tests/Lumeo.Tests.E2E/Snapshots/home-above-fold.png` fehlt.

Fix-Idee: Baseline bewusst generieren und committen oder den Visual-Test bis zur stabilen Baseline aus dem E2E-Default ausschliessen.

## Finale Einschätzung

Die normale Test-Situation ist deutlich besser: Build und nicht-E2E Tests sind gruen, keine skipped Tests, volle 131/131 Komponentenabdeckung. Aber E2E ist aus Sicht dieses Rechecks noch nicht "done". Es ist jetzt sauber aus dem normalen CI-Pfad entkoppelt, doch als echter Browser-Qualitaetsbeweis ist es noch rot.

Fuer v2-rc ist das akzeptabel, wenn E2E bewusst als separater Workstream behandelt wird. Fuer v2 final mit "Docs page perfekt" sollte E2E entweder wirklich gruen im separaten Workflow laufen oder nicht als erledigter Release-Check zaehlen.
