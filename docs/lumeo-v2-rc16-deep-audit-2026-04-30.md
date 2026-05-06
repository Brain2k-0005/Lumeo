# Lumeo v2-rc.16 Deep Audit - 2026-04-30

Scope: erneutes technisches Audit nach den letzten Code-Aenderungen. Fokus ist v2-rc.16, nicht v1. Bewertet wurden Source/Registry/Docs/Tests/CI sowie gezielt Async, Race Conditions, Disposal, Event-Handler, Cross-Talk, ungueltige Zustaende und ungetestete kritische Pfade.

## 1. Executive Summary

Lumeo ist nach dem aktuellen Stand deutlich naeher an einem soliden v2 Release als in den frueheren Reviews. Der wichtigste Fortschritt: der Registry-zu-Source-zu-Docs-Abgleich ist aktuell sauber, alle 131 Registry-Komponenten haben Source-Dateien und eine Docs-Seite. Lokal sind Build und Tests gruen: `dotnet build Lumeo.slnx -c Release --no-restore` lief mit 0 Fehlern und 0 Warnungen, `Lumeo.Tests` hatte 1744 passed / 1 skipped, `Lumeo.Docs.Tests` 20 passed und `Lumeo.RegistryGen.Tests` 16 passed. Die Registry generiert 173 Items: 131 Komponenten, 16 Patterns, 8 Blocks, 18 Guides; alle Routen wurden beim Build als live verifiziert.

Trotzdem wuerde ich v2-rc.16 noch nicht als "perfekt" oder final-hardening-fertig einstufen. Es gibt keine neu gefundenen P0-Blocker im Sinne "Source fehlt" oder "Docs-Seite fehlt", aber es gibt mehrere P1/P2-Defekte in Async/Event/CI-Testabdeckung. Besonders kritisch sind fire-and-forget Pfade ueber `ContinueWith`, ein Timer-Debounce im DataGrid, der async Arbeit nicht awaitet, sowie eine Docs-Komponente mit `async void` Navigation-Handler und JS `eval`. Ausserdem deckt CI nicht denselben Qualitaetsumfang ab, den wir lokal gruen sehen. Fuer v2 final sollte das in 2 grossen Schritten behoben werden: erst ein Async/Lifecycle-Hardening ueber alle interaktiven Provider, danach ein Release-CI-Gate mit vollstaendiger Komponenten-/Docs-/Registry-Abdeckung.

Einschaetzung: produktionsnah fuer kontrollierte Nutzer und Demos, aber noch nicht "v2 final perfekt". Als v2-rc ist der Stand stark; als finale UI-Library mit Perfektionsanspruch fehlen noch harte Guarantees fuer Lifecycle, Async-Fehler und ungetestete High-Interactivity-Komponenten.

Groesste 3 Staerken:

- Registry, Source und Docs sind aktuell konsistent: 131/131 Komponenten haben Source und Docs.
- Testbasis ist breit: lokal laufen 1744 Komponenten-/Service-Tests plus Docs- und RegistryGen-Tests.
- Disposal und JS-Interop sind vielerorts bewusst behandelt, z.B. `ComponentInteropService`, `ToastProvider`, `KeyboardShortcutService`, `RichTextInterop`.

Groesste 3 Risiken:

- Async/Event-Pfade nutzen mehrfach fire-and-forget plus `ContinueWith`; Fehler werden geloggt statt strukturiert kontrolliert.
- CI laeuft nicht denselben Umfang wie die lokale Release-Pruefung und wuerde Docs-/Registry-Regressions nicht sicher blocken.
- 23 Registry-Komponenten haben keine direkt erkennbare Komponententest-Datei; das ist fuer "alle Komponenten perfekt" zu viel Blindflug.

## 2. Verifizierter Status

Commands/Checks:

```powershell
dotnet build Lumeo.slnx -c Release --no-restore
dotnet test tests\Lumeo.Tests\Lumeo.Tests.csproj -c Release --no-build --verbosity minimal
dotnet test tests\Lumeo.Docs.Tests\Lumeo.Docs.Tests.csproj -c Release --no-build --verbosity minimal
dotnet test tests\Lumeo.RegistryGen.Tests\Lumeo.RegistryGen.Tests.csproj -c Release --no-build --verbosity minimal
dotnet test tests\Lumeo.Tests\Lumeo.Tests.csproj -c Release --no-build --filter FullyQualifiedName~DataGridServerServiceTests --verbosity minimal
dotnet test tests\Lumeo.Tests\Lumeo.Tests.csproj -c Release --no-build --filter FullyQualifiedName~ToastTests --verbosity minimal
```

Resultate:

- Build: passed, 0 warnings, 0 errors.
- `Lumeo.Tests`: 1744 passed, 1 skipped.
- `Lumeo.Docs.Tests`: 20 passed.
- `Lumeo.RegistryGen.Tests`: 16 passed.
- DataGridServerService gezielt: 25 passed.
- Toast gezielt: 28 passed.
- Registry-Audit: 131/131 Komponenten mit Source OK.
- Docs-Audit: 131/131 Komponenten mit Docs-Seite OK.
- Direkte Testdatei-Abdeckung: 108/131 OK, 23/131 ohne direkt erkennbaren Test.

## 3. Gut geloest

### Registry/Docs/Source sind wieder synchron

Warum gut: Das war vorher ein typisches Release-Risiko fuer versteckte fehlende Features. Der aktuelle maschinelle Abgleich findet keine Registry-Komponente ohne Source und keine Registry-Komponente ohne Docs-Seite.

Betroffene Dateien/Module:

- `src/Lumeo/registry/registry.json`
- `docs/Lumeo.Docs/Pages/Components/**/*.razor`
- `src/Lumeo*/**/*`

Auswirkung: Das reduziert Broken-Link-, Packaging- und "hidden missing component"-Risiken deutlich. Fuer eine UI-Library ist das eine zentrale v2-Qualitaetsbasis.

### Build- und Testbasis ist breit

Warum gut: Der lokale Stand beweist, dass der aktuelle Code nicht nur kompiliert, sondern auch ein grosser Anteil der Komponenten und Services ueber bUnit/xUnit abgesichert ist.

Betroffene Dateien/Module:

- `tests/Lumeo.Tests`
- `tests/Lumeo.Docs.Tests`
- `tests/Lumeo.RegistryGen.Tests`
- `Lumeo.slnx`

Auswirkung: Regressionen in vielen Basiskomponenten werden schnell gefunden. Besonders positiv ist, dass es eigene Tests fuer Services wie `ComponentInteropService`, `KeyboardShortcutService`, `DataGridServerService` und `DataGridExportService` gibt.

### JS-Interop ist nicht mehr komplett ad hoc

Warum gut: `ComponentInteropService` kapselt viele browsernahe Features und trennt Teilbereiche ueber helper classes wie `ClickOutsideInterop`, `ResizeInterop`, `ScrollInterop`, `SwipeInterop`, `RichTextInterop`.

Betroffene Dateien/Module:

- `src/Lumeo/Services/ComponentInteropService.cs`
- `src/Lumeo/Services/Interop/*.cs`

Auswirkung: Das ist besser wartbar als Interop direkt in jeder Komponente. Es macht Disposal, Strict-Interop-Tests und spaetere Browser-Hardening-Arbeit ueberhaupt erst realistisch.

## 4. Priorisierte Defect-Liste

### P1 - CI prueft nicht den vollstaendigen v2 Release-Umfang

Datei:

- `.github/workflows/ci.yml`

Beleg:

```yaml
run: dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-restore --verbosity normal
```

Ursache: CI testet nur `tests/Lumeo.Tests`. Die lokal erfolgreich ausgefuehrten Projekte `tests/Lumeo.Docs.Tests` und `tests/Lumeo.RegistryGen.Tests` fehlen in CI. Der Docs-Build laeuft ausserdem ohne `-warnaserror`, waehrend Library/Satellites mit `-warnaserror` gebaut werden.

Risiko: Registry- oder Docs-Routen-Regressions koennen lokal auffallen, aber im PR unentdeckt bleiben. Fuer den Anspruch "alle Docs page muss perfekt sein" ist das ein echter Release-Gate-Defekt.

Fix-Idee: CI auf Solution-Ebene vereinheitlichen:

```powershell
dotnet restore Lumeo.slnx
dotnet build Lumeo.slnx -c Release --no-restore -warnaserror
dotnet test Lumeo.slnx -c Release --no-build --verbosity normal
```

Zusatz: Falls Solution-weites Testen wegen Projektstruktur nicht sauber geht, explizit `Lumeo.Tests`, `Lumeo.Docs.Tests` und `Lumeo.RegistryGen.Tests` testen und Registry-Route-Generation als eigenen Schritt blockierend machen.

### P1 - DataGrid Debounce startet async UI-Arbeit ueber sync Timer-Action

Dateien:

- `src/Lumeo.DataGrid/UI/DataGrid/DataGridServerService.cs`
- `src/Lumeo.DataGrid/UI/DataGrid/DataGrid.razor`
- `tests/Lumeo.Tests/Services/DataGridServerServiceTests.cs`

Beleg:

```csharp
internal void DebounceSearch(Action requestAction, int delayMs = 300)
{
    _searchDebounceTimer?.Dispose();
    _searchDebounceTimer = new System.Threading.Timer(_ =>
    {
        requestAction();
    }, null, delayMs, System.Threading.Timeout.Infinite);
}
```

```csharp
_serverService!.DebounceSearch(() => InvokeAsync(async () =>
{
    await RequestServerData();
    StateHasChanged();
}));
```

Ursache: `DebounceSearch` akzeptiert `Action`, aber der Callsite startet `InvokeAsync(async () => ...)`, also async Arbeit als fire-and-forget. Der Timer kann den Callback nach Dispose oder waehrend einer neuen Generation ausloesen, Exceptions werden nicht awaitet, und der Test erlaubt sogar Race-Verhalten:

```csharp
Assert.True(callCount <= 2);
```

Risiko: Bei schneller Suche, Dispose/Navigate oder Server-Request-Fehlern koennen UI-Updates spaet kommen, Fehler unobserved bleiben oder alte Suchanfragen nachlaufen. Das ist kein garantierter Repro-Bug in jedem Lauf, aber ein belastbares Race-/Async-Designproblem.

Fix-Idee: `DebounceSearch` auf `Func<CancellationToken, Task>` umstellen, per `CancellationTokenSource` statt `System.Threading.Timer` implementieren, Exceptions kontrolliert behandeln und Tests hart machen: exakt letzter Callback, kein Callback nach Dispose, Exception wird beobachtet/weitergereicht.

### P1 - `OnThisPage` nutzt `async void` fuer Navigation und nicht-cancellable Retry-Loops

Datei:

- `docs/Lumeo.Docs/Shared/OnThisPage.razor`

Beleg:

```csharp
private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
{
    if (e.Location == _lastPath) return;
    _lastPath = e.Location;
    await RescanWithRetry();
}
```

Zusatzbeleg:

```csharp
int[] delays = { 50, 150, 300, 600 };
foreach (var d in delays)
{
    await Task.Delay(d);
    await Rescan();
    if (_headings.Count > 0) return;
}
```

Ursache: `NavigationManager.LocationChanged` ist ein sync Event, aber der Handler ist `async void`. Gleichzeitig laufen Retry-Scans ohne Cancellation/Generation-Guard. Bei schnellem Navigieren koennen alte Scans nach einer neueren Route noch `_headings` und `_activeId` setzen.

Risiko: Docs-On-this-page kann bei schnellen Routenwechseln falsche Headings anzeigen, spaet JS-Interop ausloesen oder Fehler verschlucken. Weil Docs fuer v2 "perfekt" sein sollen, ist das relevant, obwohl es "nur" Docs-App-Code ist.

Fix-Idee: Handler sync lassen und kontrolliert starten:

```csharp
private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
{
    _ = InvokeAsync(() => RescanWithRetryAsync(_scanCts.Token));
}
```

Mit `_scanCts.Cancel()` pro Route, `Task.Delay(d, token)`, Generation-ID und Dispose-Cancellation. Zusaetzlich `eval` in `ScrollTo` durch eine dedizierte JS-Funktion ersetzen.

### P1 - Fire-and-forget `ContinueWith` in Toast/Overlay/MegaMenu verschluckt Fehler und erschwert Lifecycle-Sicherheit

Dateien:

- `src/Lumeo/UI/Toast/ToastProvider.razor`
- `src/Lumeo/UI/Overlay/OverlayProvider.razor`
- `src/Lumeo/UI/MegaMenu/MegaMenuItem.razor`

Beleg:

```csharp
_ = InvokeAsync(() => HandleShowAsync(message))
    .ContinueWith(t =>
    {
        if (t.IsFaulted)
            Console.Error.WriteLine($"[ToastProvider] HandleShow error: {t.Exception}");
    }, TaskScheduler.Current);
```

Ursache: Provider-Events werden fire-and-forget in die Renderer-Queue gelegt. Fehler werden nur in `Console.Error` geschrieben. `TaskScheduler.Current` ist hier fragil, weil Event-Callbacks aus verschiedenen Kontexten kommen koennen. Das Muster taucht mehrfach auf: Toast lines 55/83/94/118/141/177, Overlay lines 97/114, MegaMenu lines 87/108.

Risiko: Bei Dispose/Navigate/Concurrent Service-Events koennen Fehler verschwinden, Tests sehen sie nicht, und Konsumenten bekommen keinen deterministischen Zustand. Fuer UI-Provider ist das ein klassischer "works until load/lifecycle edge case" Defekt.

Fix-Idee: Zentralen helper einfuehren, z.B. `SafeInvokeAsync(Func<Task> work, string source)`, der `ObjectDisposedException`, `InvalidOperationException` und `JSDisconnectedException` sauber behandelt, ohne `ContinueWith` und ohne `TaskScheduler.Current`. Fuer Toast/Overlay Tests ergaenzen: Show nach Dispose, Dismiss waehrend Exit-Animation, concurrent Show/Dismiss, Provider dispose setzt pending overlay references deterministisch auf cancel.

### P2 - ComponentInteropService hat sync Dispose, das JS-Registrierungen nur lokal cleared

Datei:

- `src/Lumeo/Services/ComponentInteropService.cs`

Beleg:

```csharp
public void Dispose()
{
    _clickOutside.Clear();
    _swipe.Clear();
    _resize.Clear();
    _scroll.Clear();
    _utility.Clear();
    _selfRef?.Dispose();
}
```

Ursache: `DisposeAsync` ruft fuer registrierte ClickOutside-IDs JS-Unregister auf, `Dispose()` nicht. Der Service ist scoped registriert und implementiert beides. In Blazor sollte async disposal bevorzugt werden, aber das sync `Dispose()` existiert und kann in Tests/DI/host-spezifischen Pfaden genutzt werden.

Risiko: Wenn sync Dispose verwendet wird, koennen browserseitige Listener/Observer bis zur Page-Lifetime weiterleben, waehrend die .NET reference disposed ist. Das ist genau die Art JS-Interop-Leak, die spaeter schwer zu debuggen ist.

Fix-Idee: Entweder sync `Dispose()` entfernen, falls API/DI das erlaubt, oder klar dokumentieren und intern auf einen best-effort async cleanup umstellen. Besser: Registration-Handles pro Feature einfuehren, die Komponenten explizit `DisposeAsync`en, und Service-Dispose nur als Circuit-Fallback verwenden.

### P2 - WordImporter hat einen skipped Real-DOCX-Test mit lokalem absoluten Pfad

Datei:

- `tests/Lumeo.Tests/Editor/WordImporterTests.cs`

Beleg:

```csharp
[Fact(Skip = "Requires test fixture at C:/Users/bemi/Downloads/TEST0033/...docx — not in repo.")]
```

Ursache: Der einzige realistischere DOCX-Test ist nicht reproduzierbar, weil die Fixture nicht im Repository liegt.

Risiko: `WordImporter` kann fuer echte Word-Dokumente regressieren, obwohl CI gruen bleibt. Gerade Editor/Importer-Funktionalitaet ist stark format- und edge-case-lastig; Minimal-ZIP-Tests reichen dafuer nicht.

Fix-Idee: Kleine anonymisierte DOCX-Fixture unter `tests/Lumeo.Tests/Fixtures/Editor/` einchecken oder programmgesteuert eine realistischere DOCX mit Styles/Headings/Lists/Links/Tables erzeugen. Skip entfernen und Test in CI erzwingen.

### P2 - 23 Komponenten ohne direkte Komponententest-Datei

Betroffene Registry-Komponenten:

- `cascader`
- `color-picker`
- `consent-banner`
- `date-time-picker`
- `gantt`
- `image-compare`
- `inplace-editor`
- `input-mask`
- `kanban`
- `mega-menu`
- `mention`
- `password-input`
- `pop-confirm`
- `qr-code`
- `rich-text-editor`
- `scheduler`
- `sortable`
- `speed-dial`
- `time-picker`
- `tour`
- `transfer`
- `tree-select`
- `tree-view`

Ursache: Automatischer Registry-vs-Test-Abgleich findet fuer diese Komponenten keine direkt passende Testdatei unter `tests/Lumeo.Tests/Components`. Einige koennen indirekt ueber Service- oder generische Tests abgedeckt sein, aber fuer "jede Komponente perfekt" reicht indirekt nicht.

Risiko: Das sind ueberwiegend interaktive/komplexe Komponenten: Pickers, Scheduler, Gantt, Drag/Sort, Overlay/Popover-artige Komponenten, Editor-nahe Komponenten. Genau dort entstehen typischerweise Event-, Focus-, Keyboard-, Lifecycle- und Edge-State-Bugs.

Fix-Idee: Pro Komponente mindestens einen Contract-Test-Satz:

- rendert ohne Exception mit Default-Parametern;
- `Class`, `Style`, `Id`, `AdditionalAttributes` werden korrekt gemerged;
- disabled/readonly/open/selected states;
- keyboard/focus/outside-click fuer overlayartige Komponenten;
- dispose/double-dispose ohne JSInterop-Leak;
- docs sample compiliert/rendert.

## 5. Wahrscheinliche Bugs

### DataGrid: Debounced Server Search kann veraltete oder nach Dispose laufende Arbeit ausloesen

Datei: `src/Lumeo.DataGrid/UI/DataGrid/DataGridServerService.cs`

Methode: `DebounceSearch`

Warum vermutlich Bug: Der Timer-Callback ist sync, die eigentliche Arbeit async. Es gibt keine Cancellation fuer den async Teil und keine Await-/Exception-Kette. Der bestehende Test akzeptiert Race-Verhalten mit `callCount <= 2`, statt exakt den letzten Call zu verlangen.

Reproduktion:

1. ServerMode DataGrid mit langsamem `OnServerRequest`.
2. Schnell mehrere Suchwerte tippen.
3. Direkt navigieren/disposen oder Search erneut triggern.
4. Beobachten, ob alte Requests/StateHasChanged spaet eintreffen oder Exceptions nur geloggt/unobserved bleiben.

Fix: `Func<CancellationToken, Task>` Debouncer, Generation-ID, Dispose-Cancellation, Tests fuer exakt eine Ausfuehrung und keine Ausfuehrung nach Dispose.

### Docs OnThisPage: falsche Headings bei schnellen Routenwechseln moeglich

Datei: `docs/Lumeo.Docs/Shared/OnThisPage.razor`

Methode: `OnLocationChanged`, `RescanWithRetry`, `Rescan`

Warum vermutlich Bug: Alte `RescanWithRetry` Tasks koennen nach neuer Navigation fertig werden und `_headings` fuer die falsche Route setzen.

Reproduktion:

1. Docs-App starten.
2. Schnell zwischen zwei Komponenten-Seiten mit unterschiedlichen Headings wechseln.
3. On-this-page Sidebar beobachten.
4. Bei Timing-Treffern koennen Headings der vorherigen Seite kurz oder dauerhaft stehen bleiben.

Fix: CancellationTokenSource pro Route, monotone Scan-Generation, kein `async void`.

### Toast/Overlay/MegaMenu: Fehler in Event-Callbacks werden nicht testbar propagiert

Dateien: `ToastProvider.razor`, `OverlayProvider.razor`, `MegaMenuItem.razor`

Methoden: `HandleShow`, `HandleDismiss`, `HandleUpdate`, `HandleShowAsync`, `HandleClose`, `HandleMouseEnter`, `HandleMouseLeave`

Warum vermutlich Bug: UI-Aufgaben laufen fire-and-forget, `ContinueWith` loggt nur. Wenn eine Exception im Callback entsteht, bleibt der Provider fuer Konsumenten scheinbar erfolgreich, aber intern kann Zustand fehlen.

Reproduktion:

1. Testservice loest Show/Dismiss aus, waehrend Provider disposed oder RenderContext invalid ist.
2. Exception im Callback erzwingen.
3. Assert scheitert nicht, weil Fehler nur in Console landet.

Fix: zentraler awaited Safe-Dispatcher plus Tests, die Fehlerpfade deterministisch pruefen.

## 6. Architekturrisiken

### Grosse interaktive Surface Area ohne einheitlichen Lifecycle-Contract

Heute funktioniert viel, weil Komponenten und Services individuell sauber genug sind. Spaeter wird es problematisch, weil Komponenten wie Toast, Overlay, MegaMenu, Pickers, Scheduler, Gantt, Sortable, TreeSelect und RichTextEditor unterschiedliche Patterns fuer Events, async Dispatch und Cleanup verwenden.

Sinnvolles Refactoring:

- Eine gemeinsame `ComponentAsyncDispatcher`/`SafeInvoke`-Hilfsschicht fuer fire-and-forget UI Events.
- Ein gemeinsamer Test-Contract fuer alle Komponenten mit Event-/JS-/Dispose-Verhalten.
- Einheitliche Registration-Handles fuer JS-Interop statt Service-globaler Registry-Maps.

### Docs-App ist Teil des Produkts, aber CI behandelt sie noch nicht gleich hart

Die Docs sind fuer v2 faktisch Product Surface. Der lokale Build validiert Routen, aber CI testet Docs.Tests nicht. Das macht Docs-Perfektion vom lokalen Entwicklerverhalten abhaengig.

Sinnvolles Refactoring:

- Docs build/test als blocking Release Gate.
- Link-/route-/sample-render Tests fuer jede Registry-Komponente.
- Optional Browser smoke test gegen prerendered docs.

### Testabdeckung ist breit, aber nicht gleichmaessig

108/131 direkte Testabdeckung ist gut, aber die fehlenden 23 sind nicht triviale Display-Komponenten, sondern zu grossen Teilen komplexe Interaktionskomponenten.

Sinnvolles Refactoring:

- `ComponentContractTests` als shared theory ueber Registry-Komponenten.
- Pro komplexer Komponente eigener Interaction-Test.
- JSInterop strict mode fuer alle Komponenten, die Interop nutzen.

## 7. Test- und Qualitaetsbewertung

Gut:

- Sehr viele Komponenten- und Service-Tests vorhanden.
- DataGridServerService hat bereits Race-orientierte Tests.
- StrictInteropTests existieren und pruefen Interop-Aufrufe.
- Docs- und RegistryGen-Tests existieren und laufen lokal gruen.

Unzureichend abgesichert:

- 23 Komponenten ohne direkte Tests.
- Fire-and-forget Fehlerpfade in Toast/Overlay/MegaMenu.
- Docs-OnThisPage Routenwechsel-/Cancellation-Verhalten.
- Real-DOCX Import.
- Browsernahe Tests fuer Focus, Keyboard, outside-click, ResizeObserver, IntersectionObserver, Drag/Drop, Scheduler/Gantt.

CI/CD:

- CI ist gut als Basis, aber nicht ausreichend als v2-final Gate.
- Fehlend: `Lumeo.Docs.Tests`, `Lumeo.RegistryGen.Tests`, full solution test, route/search-index validation, optional Playwright/browser smoke, stricter artifact hygiene.

## 8. Top-10 Massnahmen

1. Problem: CI prueft nicht alle lokalen Release-Checks. Nutzen: PRs koennen Docs/Registry nicht mehr kaputt mergen. Aufwand: S.
2. Problem: DataGrid Debounce ist sync Timer plus async fire-and-forget. Nutzen: weniger Race Conditions bei ServerMode Search. Aufwand: M.
3. Problem: `OnThisPage` nutzt `async void` und nicht-cancellable retries. Nutzen: stabile Docs-Navigation ohne stale headings. Aufwand: S.
4. Problem: `ContinueWith`-Pattern in Toast/Overlay/MegaMenu. Nutzen: deterministische Fehlerbehandlung und weniger Lifecycle-Bugs. Aufwand: M.
5. Problem: 23 Komponenten ohne direkte Tests. Nutzen: echte "alle Komponenten" Absicherung. Aufwand: L.
6. Problem: skipped WordImporter Real-DOCX-Test. Nutzen: Importer-Regressions werden in CI sichtbar. Aufwand: S.
7. Problem: JS-Interop sync Dispose kann browserseitige Registrierungen nicht unregisteren. Nutzen: weniger Event-/Observer-Leaks. Aufwand: M.
8. Problem: Keine einheitlichen Component Contract Tests. Nutzen: API-Konsistenz fuer `Class`, `Style`, `Id`, `AdditionalAttributes`, disabled/readonly. Aufwand: M.
9. Problem: Browsernahe Interaktionen nur begrenzt abgedeckt. Nutzen: echte Sicherheit fuer Popover, Pickers, Drag/Drop, Scheduler/Gantt. Aufwand: L.
10. Problem: Docs haben zwar Seiten, aber kein vollstaendiges Sample-/Visual-Smoke-Gate. Nutzen: Docs werden Product-grade. Aufwand: M.

## 9. Scorecard

- Architektur: 8/10
- Codequalitaet: 8/10
- Konsistenz: 8/10
- Wartbarkeit: 8/10
- Testqualitaet: 7/10
- Produktionsreife: 7/10
- Entwicklererlebnis: 8/10
- Zukunftsfaehigkeit: 8/10

## 10. Finale Einschätzung

Wuerde ich Lumeo v2-rc.16 heute in Produktion einsetzen? Ja, fuer kontrollierte Anwendungen, interne Tools, Demos und produktionsnahe Pilotprojekte. Nein, noch nicht als "perfekte" finale v2 UI-Library mit dem Anspruch, dass alle Komponenten und die Docs-Seite ohne Hidden/Missing Futures durch sind.

Unter welchen Bedingungen ja: Wenn die konsumierende App bereit ist, bei komplexen Komponenten wie Scheduler, Gantt, RichTextEditor, Pickers, Sortable/Kanban und Overlay-Patterns selbst nochmal Integrations-/Browser-Smoke-Tests zu fahren.

Was vor v2 final passieren muss:

1. Ein grosser Async/Lifecycle-Hardening-Block: DataGrid Debounce, OnThisPage, Toast/Overlay/MegaMenu `ContinueWith`, JS-Interop Dispose.
2. Ein grosser Release-Gate-Block: CI erweitert auf alle Testprojekte, 23 Komponenten mit direkten Tests, skipped DOCX-Test weg, Docs/Registry/Sample-Checks blocking.

Mein hartes Urteil: Die grossen strukturellen Luecken aus den alten Reviews sind groesstenteils geschlossen. Der aktuelle Rest ist nicht mehr "Repo ist unreif", sondern "v2 final braucht jetzt diszipliniertes Hardening". Genau diese letzten 10-15 Prozent entscheiden, ob Lumeo nur sehr gut wirkt oder wirklich robust bleibt, wenn Nutzer wild klicken, schnell navigieren und Komponenten in echten Apps kombinieren.
