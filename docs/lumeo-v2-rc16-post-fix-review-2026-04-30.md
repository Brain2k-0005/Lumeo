# Lumeo v2-rc.16 Post-Fix Review - 2026-04-30

Scope: erneutes Review nach Aussage, dass alle im letzten Audit genannten Punkte verbessert wurden. Geprueft wurden die vorherigen Findings gezielt erneut: CI, DataGrid Debounce, Docs `OnThisPage`, Toast/Overlay/MegaMenu Async-Dispatch, Interop Dispose, skipped Tests, Komponenten-Testabdeckung, Registry/Docs/Source-Sync und neue E2E-Abdeckung.

## 1. Executive Summary

Der aktuelle Stand ist deutlich staerker als beim letzten Audit. Die wichtigsten alten Punkte wurden nicht nur kosmetisch, sondern in vielen Faellen strukturell adressiert: CI baut/testet jetzt auf Solution-Ebene, DataGrid Debounce nutzt `Func<CancellationToken, Task>`, `OnThisPage` hat kein `async void` und kein `eval` mehr, Toast/Overlay/MegaMenu nutzen einen zentralen `SafeAsyncDispatcher`, und die direkte Komponenten-Testabdeckung ist von 108/131 auf 131/131 gestiegen. Auch der alte skipped WordImporter-Test ist nicht mehr auffindbar.

Der neue groesste Defekt ist aber hart: `dotnet test Lumeo.slnx -c Release --no-build` ist aktuell rot, weil das neue `Lumeo.Tests.E2E` Projekt in der Solution mitlaeuft, Playwright-Browser aber nicht installiert sind. Genau denselben Befehl fuehrt CI jetzt aus. Damit ist der neue CI-Gate-Ansatz korrekt gemeint, aber im aktuellen Zustand wahrscheinlich nicht gruen lauffaehig. Das ist kein Produktbug in Lumeo-Komponenten, aber ein echter Release-/CI-Blocker.

Produktseitig bleiben nur noch wenige konkrete technische Risiken: `SafeAsyncDispatcher` faengt Exceptions innerhalb der dispatched work, aber nicht sichtbar den Task von `invokeAsync` selbst; `OnThisPage` cancellt zwischen Retries, aber nicht innerhalb des eigentlichen JS-Scans; und ein paar docs-/chart-nahe Timer/eval-Pfade sind noch nicht auf denselben Hardening-Standard gebracht. Insgesamt ist Lumeo jetzt nah an v2-final-tauglich, aber der CI/E2E-Gate muss zuerst sauber gemacht werden.

Einschaetzung: produktionsnah und fuer v2-rc sehr gut, aber noch nicht final release-ready, solange `dotnet test Lumeo.slnx` lokal/CI rot ist.

## 2. Verifizierter Status

Ausgefuehrte Checks:

```powershell
dotnet build Lumeo.slnx -c Release --no-restore
dotnet test Lumeo.slnx -c Release --no-build --verbosity minimal
```

Ergebnisse:

- Build: passed, 0 warnings, 0 errors.
- Registry generation beim Build: 173 Items, 131 Komponenten, 16 Patterns, 8 Blocks, 18 Guides, alle Routen live verifiziert.
- Registry-vs-Test-Abdeckung: 131/131 Komponenten mit direkt erkennbarem Test.
- `Lumeo.Tests`: 2111 passed, 0 skipped.
- `Lumeo.Docs.Tests`: 20 passed.
- `Lumeo.RegistryGen.Tests`: 16 passed.
- `Lumeo.Tests.E2E`: 16 failed, 0 passed, 0 skipped.
- Grund fuer alle 16 E2E-Fails: Playwright Chromium executable fehlt lokal unter `C:\Users\bemi\AppData\Local\ms-playwright\chromium_headless_shell-1148\chrome-win\headless_shell.exe`.

Wichtig: Die E2E-Fails sind nicht durch eine fachliche Assertion entstanden, sondern durch fehlende Browser-Installation. Trotzdem ist das fuer CI ein echter Fehler, weil CI jetzt ebenfalls `dotnet test Lumeo.slnx` ausfuehrt.

## 3. Delta Zum Letzten Audit

### Behoben: CI testet nicht mehr nur `Lumeo.Tests`

Datei:

- `.github/workflows/ci.yml`

Status: verbessert, aber neuer Blocker durch E2E.

Beleg:

```yaml
run: dotnet restore Lumeo.slnx
run: dotnet build Lumeo.slnx -c Release --no-restore -warnaserror
run: dotnet test Lumeo.slnx -c Release --no-build --verbosity normal
```

Bewertung: Die Richtung ist richtig. Docs.Tests und RegistryGen.Tests laufen dadurch nicht mehr ausserhalb des Gates. Aber durch das neue E2E-Projekt ist `dotnet test Lumeo.slnx` ohne Playwright-Setup rot.

### Behoben: DataGrid Debounce ist nicht mehr `System.Threading.Timer` + sync `Action`

Datei:

- `src/Lumeo.DataGrid/UI/DataGrid/DataGridServerService.cs`

Status: weitgehend behoben.

Beleg:

```csharp
internal void DebounceSearch(Func<CancellationToken, Task> work, int delayMs = 300)
```

Verbesserung:

- Kein `System.Threading.Timer` mehr im Server-Search-Debounce.
- Neuer `CancellationTokenSource` pro Suchlauf.
- Tests sind schaerfer: `ExactLastCall`, `AfterDispose_DoesNotFire`, `PropagatesCancellationToken`.

Restbewertung: Der async Fehlerpfad loggt weiterhin nur nach `Console.Error`, aber fuer Debounce ist das akzeptabel, sofern bewusst nicht an den Caller propagiert werden soll. Gegenueber vorher ist das substantiell besser.

### Behoben: `OnThisPage` hat kein `async void` und kein `eval` mehr

Datei:

- `docs/Lumeo.Docs/Shared/OnThisPage.razor`

Status: verbessert, mit kleinem Rest-Race.

Beleg:

```csharp
private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
```

```csharp
await JS.InvokeVoidAsync("lumeo.navScrollActiveIntoView", id);
```

Verbesserung:

- `async void` entfernt.
- `eval` durch globale JS-Funktion ersetzt.
- Route-Scan bekommt `CancellationTokenSource`.
- Dispose cancelt `_scanCts`.

Restproblem: `RescanWithRetry(CancellationToken ct)` cancellt vor `Rescan()`, aber `Rescan()` selbst bekommt keinen Token und hat keinen Generation-Guard. Wenn Navigation waehrend eines laufenden JS-Scans passiert, kann ein alter Scan theoretisch noch `_headings` setzen.

### Behoben: Toast/Overlay/MegaMenu nutzen nicht mehr ad hoc `ContinueWith`

Dateien:

- `src/Lumeo/UI/Toast/ToastProvider.razor`
- `src/Lumeo/UI/Overlay/OverlayProvider.razor`
- `src/Lumeo/UI/MegaMenu/MegaMenuItem.razor`
- `src/Lumeo/Services/SafeAsyncDispatcher.cs`

Status: stark verbessert, aber Dispatcher braucht noch eine kleine Haertung.

Beleg:

```csharp
SafeAsyncDispatcher.FireAndForget(InvokeAsync, () => HandleShowAsync(message), "ToastProvider.HandleShow");
```

Bewertung: Das ist deutlich besser als viele einzelne `ContinueWith`-Bloecke. Lifecycle-Exceptions werden zentral behandelt. Der Restpunkt ist, dass der Rueckgabe-Task von `invokeAsync(...)` selbst nicht awaited oder mit continuation beobachtet wird.

### Behoben: direkte Komponenten-Testabdeckung

Status: behoben.

Maschineller Abgleich:

- Vorher: 108/131 direkt erkennbare Komponenten-Tests.
- Jetzt: 131/131 direkt erkennbare Komponenten-Tests.

Bewertung: Das ist ein grosser Qualitaetssprung. Die fehlenden 23 Komponenten aus dem letzten Audit sind jetzt testseitig nicht mehr blind.

### Behoben: skipped WordImporter-Test

Status: behoben.

Beleg: Suche nach `Skip =` in `tests` ohne `bin/obj/TestResults` liefert keine Treffer.

Bewertung: Gut. Keine still ausgeklammerte Kernfunktion mehr sichtbar.

## 4. Neue / Noch Offene Defects

### P1 - Solution-Test und CI sind aktuell rot wegen E2E ohne Playwright-Setup

Dateien:

- `.github/workflows/ci.yml`
- `tests/Lumeo.Tests.E2E/README.md`
- `tests/Lumeo.Tests.E2E/PlaywrightTestBase.cs`

Ursache: CI fuehrt jetzt `dotnet test Lumeo.slnx` aus. Dadurch wird `tests/Lumeo.Tests.E2E` mit ausgefuehrt. Das E2E-Projekt braucht aber Playwright-Browser und einen laufenden Docs-Server. Die README sagt selbst:

```md
These tests are **not yet wired into CI by default** — they require:
1. A running docs server
2. Playwright browser binaries
```

Aktueller Testfehler:

```text
Microsoft.Playwright.PlaywrightException : Executable doesn't exist at
C:\Users\bemi\AppData\Local\ms-playwright\chromium_headless_shell-1148\chrome-win\headless_shell.exe
```

Risiko: Jeder CI-Lauf, der `dotnet test Lumeo.slnx` ohne Browser-Install ausfuehrt, wird rot. Falls GitHub Actions zufaellig Browser anders cached, fehlt danach trotzdem noch der laufende Docs-Server fuer echte E2E.

Fix-Idee:

Option A, sauber fuer v2 final:

- CI teilt Tests in Unit/Docs/Registry und E2E.
- Unit/Docs/Registry laufen immer.
- E2E Workflow installiert Playwright Chromium, startet `docs/Lumeo.Docs` im Hintergrund und setzt `LUMEO_E2E_BASE_URL`.

Option B, kurzfristig fuer gruenes CI:

- `Lumeo.Tests.E2E` aus dem default `dotnet test Lumeo.slnx` Gate ausschliessen.
- E2E separat dokumentiert/manuell laufen lassen.

Empfehlung: Option A. Fuer v2-rc.16 mit Perfektionsanspruch sollte E2E nicht pseudo-integriert sein, sondern wirklich lauffaehig.

### P2 - `SafeAsyncDispatcher` beobachtet den outer `InvokeAsync` Task nicht

Datei:

- `src/Lumeo/Services/SafeAsyncDispatcher.cs`

Beleg:

```csharp
_ = invokeAsync(async () =>
{
    try { await work(); }
    catch (JSDisconnectedException) { }
    catch (ObjectDisposedException) { }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[{source}] dispatch error: {ex}");
    }
});
```

Ursache: Exceptions innerhalb von `work()` werden gefangen. Wenn aber `invokeAsync(...)` selbst synchron wirft oder der zurueckgegebene Task faulted, bevor/waehrend die Delegate-Ausfuehrung geplant wird, ist der Task fire-and-forget und wird nicht beobachtet.

Risiko: Genau der Lifecycle-Fall, den der Dispatcher adressieren soll, kann weiterhin als unobserved task enden. Das ist seltener als vorher, aber die Abstraktion ist noch nicht ganz wasserdicht.

Fix-Idee:

```csharp
public static void FireAndForget(Func<Func<Task>, Task> invokeAsync, Func<Task> work, string source)
{
    _ = DispatchAsync(invokeAsync, work, source);
}

private static async Task DispatchAsync(Func<Func<Task>, Task> invokeAsync, Func<Task> work, string source)
{
    try
    {
        await invokeAsync(async () =>
        {
            await work();
        });
    }
    catch (JSDisconnectedException) { }
    catch (ObjectDisposedException) { }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[{source}] dispatch error: {ex}");
    }
}
```

Damit wird sowohl Dispatch als auch Work zentral beobachtet.

### P2 - `OnThisPage` cancellt Retry-Delay, aber nicht den laufenden Scan

Datei:

- `docs/Lumeo.Docs/Shared/OnThisPage.razor`

Beleg:

```csharp
await Task.Delay(d, ct);
await Rescan();
```

```csharp
private async Task Rescan()
{
    ...
    var result = await _module.InvokeAsync<Heading[]>("onThisPageScan", Container);
    _headings = result.ToList();
}
```

Ursache: Der Token wird nicht an `Rescan()` weitergereicht. Wenn die Route nach `Task.Delay` wechselt, aber waehrend `onThisPageScan` laeuft, kann das alte Result noch State setzen.

Risiko: Bei schnellem Navigieren kann die Sidebar kurz falsche Headings anzeigen. Das ist kein P1 mehr, weil das grobe `async void`-Problem weg ist, aber fuer "Docs page perfekt" bleibt es ein echter Edge Case.

Fix-Idee: `Rescan(CancellationToken ct, int generation)` einfuehren, nach jedem awaited JS-Call `ct.ThrowIfCancellationRequested()` pruefen und vor `_headings = ...` eine Generation-ID vergleichen.

### P2 - E2E-Projekt ist gut, aber noch nicht als Release-Gate operationalisiert

Dateien:

- `tests/Lumeo.Tests.E2E/*`
- `.github/workflows/ci.yml`

Ursache: E2E-Tests existieren fuer wichtige Risiken, aber Infrastruktur fehlt noch. `PlaywrightTestBase` startet nur Browser/Page, nicht die Docs-App. Die README beschreibt manuelles Starten des Docs-Servers.

Risiko: E2E ist aktuell gleichzeitig "Teil der Solution" und "nicht lauffaehig ohne manuelle Umgebung". Das ist die unguenstigste Zwischenform: lokal/CI kann rot werden, aber ein gruener Lauf beweist noch nicht, dass CI wirklich den Product Surface testet.

Fix-Idee: Entweder WebApplicationFactory/Programmatic host fuer Docs in Tests bauen oder im CI-Workflow den Docs-Server explizit starten und Healthcheck abwarten.

### P3 - Docs `IconPage` nutzt noch `JS.InvokeVoidAsync("eval", ...)`

Datei:

- `docs/Lumeo.Docs/Pages/Components/IconPage.razor`

Beleg:

```csharp
await JS.InvokeVoidAsync("eval",
    $"(function(){{var el=document.getElementById('{_gridId}');if(el)el.scrollTop=0;}})()");
```

Ursache: `OnThisPage` wurde bereinigt, aber in der Docs-App gibt es weiterhin einen `eval`-Pfad.

Risiko: Gering, weil `_gridId` intern generiert ist. Trotzdem ist es inkonsistent mit dem gerade gesetzten Standard und fuer eine perfekte Docs-Seite unschoen.

Fix-Idee: Eine dedizierte JS-Funktion unter `lumeo.icons.resetGridScroll(id)` oder bestehendem Namespace anlegen.

### P3 - Chart Phantom Timer bleibt ein fire-and-forget Timer-Pfad

Datei:

- `src/Lumeo.Charts/UI/Chart/Chart.razor`

Beleg:

```csharp
_phantomTimer = new System.Threading.Timer(_ =>
{
    _phantomTick++;
    _ = InvokeAsync(async () =>
    {
        if (!_initialized || _module is null || !IsPhantomActive) return;
        ...
        await _module.InvokeVoidAsync("updateChart", _chartId, json, false);
    });
}, null, cycle, cycle);
```

Ursache: Der DataGrid-Debounce wurde modernisiert, aber Chart Phantom nutzt weiterhin Timer + fire-and-forget `InvokeAsync`.

Risiko: Wahrscheinlich gering, weil der innere JS-Call lifecycle exceptions faengt und der Timer offenbar fuer Skeleton/Phantom-Animation gedacht ist. Aber als Muster ist es derselbe Problemtyp: der outer `InvokeAsync` Task wird nicht beobachtet.

Fix-Idee: Entweder `SafeAsyncDispatcher` auch hier verwenden oder Timer durch cancellable async loop ersetzen.

## 5. Alte Findings: Status-Tabelle

| Alter Befund | Status | Bewertung |
|---|---:|---|
| CI prueft nicht alle Testprojekte | Behoben, aber neuer E2E-Blocker | Solution-Test ist richtig, braucht E2E-Infrastruktur |
| DataGrid Debounce sync Action/Timer | Behoben | Gute Umstellung auf async + CancellationToken |
| `OnThisPage async void` | Behoben | Rest: Scan selbst nicht cancellable/generation-safe |
| `OnThisPage eval` | Behoben | Rest: `IconPage` hat noch eval |
| Toast/Overlay/MegaMenu `ContinueWith` | Behoben | Rest: outer `InvokeAsync` Task im Dispatcher beobachten |
| ComponentInterop sync Dispose nur local clear | Verbessert | Fire-and-forget JS cleanup besser, aber nicht perfekt beweisbar |
| skipped WordImporter Real-Test | Behoben | Keine `Skip =` Treffer mehr |
| 23 Komponenten ohne direkte Tests | Behoben | 131/131 direkt abgedeckt |

## 6. Bewertung Der Neuen Testlage

Positiv:

- `Lumeo.Tests` ist von 1744 auf 2111 Tests gewachsen.
- Keine skipped Tests im Source-Testbereich gefunden.
- Alle 131 Registry-Komponenten haben direkt erkennbare Tests.
- E2E-Tests decken genau die richtigen Browser-Risiken an: Dialog focus trap, Dropdown keyboard, Tooltip hover, Catalog render, Search palette, Visual homepage.

Kritisch:

- `dotnet test Lumeo.slnx` ist aktuell rot.
- E2E braucht Browser-Install und Docs-Server, aber CI macht beides nicht.
- E2E README sagt "not yet wired into CI", waehrend CI durch Solution-Test faktisch doch versucht, es auszufuehren.

## 7. Top-Massnahmen Vor v2 Final

1. Problem: `dotnet test Lumeo.slnx` schlaegt wegen E2E-Setup fehl. Nutzen: CI wird wieder ein verlaesslicher Release-Gate. Aufwand: M.
2. Problem: E2E braucht manuelles Docs-Server-Setup. Nutzen: echte browserbasierte Release-Sicherheit. Aufwand: M.
3. Problem: `SafeAsyncDispatcher` beobachtet den outer InvokeAsync Task nicht. Nutzen: keine unobserved lifecycle faults im zentralen Dispatcher. Aufwand: S.
4. Problem: `OnThisPage` scannt ohne Token/Generation. Nutzen: keine stale headings bei schneller Navigation. Aufwand: S.
5. Problem: `IconPage` nutzt noch `eval`. Nutzen: einheitlicher Docs-JS-Standard. Aufwand: S.
6. Problem: Chart Phantom Timer bleibt alter fire-and-forget Pfad. Nutzen: konsistenter Async/Lifecycle-Standard. Aufwand: M.

## 8. Scorecard

- Architektur: 8.5/10
- Codequalitaet: 8.5/10
- Konsistenz: 8/10
- Wartbarkeit: 8.5/10
- Testqualitaet: 8/10
- Produktionsreife: 7.5/10
- Entwicklererlebnis: 8.5/10
- Zukunftsfaehigkeit: 8.5/10

## 9. Finale Einschätzung

Ja, der Code wurde substantiell verbessert. Die alte Liste ist nicht einfach wegdiskutiert, sondern groesstenteils wirklich bearbeitet worden. Der groesste Fortschritt ist die vollstaendige direkte Testabdeckung aller 131 Komponenten und die Umstellung der wichtigsten Async-Probleme.

Ich wuerde Lumeo jetzt als sehr starken v2 Release Candidate einstufen. Aber ich wuerde v2 final noch nicht freigeben, solange der zentrale Testbefehl `dotnet test Lumeo.slnx` rot ist. Das ist der eine grosse Blocker. Sobald E2E sauber operationalisiert ist und die kleinen Rest-Hardening-Punkte erledigt sind, wirkt Lumeo technisch release-ready.
