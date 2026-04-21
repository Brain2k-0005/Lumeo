# Technical Audit Review - 2026-04-02 v7

## 1. Executive Summary

- Der aktuelle Stand ist deutlich besser als die früheren Stände. `dotnet build` ist grün, und die Tests laufen mit `1303/1303`.
- Die größten technischen Baustellen aus den alten Reviews sind größtenteils beseitigt: Interop ist sauberer zerlegt, DataGrid-Request-Races sind adressiert, und mehrere Cleanup-Pfade wurden gehärtet.
- Übrig bleiben jetzt vor allem vier belastbare Themen: ein Chart-Refresh-Pfad, der `Group`-Sync nach Theme-Wechsel nicht zuverlässig wiederherstellt, ein Fire-and-forget-Autosave im DataGrid, ein API-Inkonsistenzproblem im KeyboardShortcut-Handle und weiterhin lockere JSInterop-Tests.
- Meine Einstufung bleibt damit `produktionsnah`, aber nicht vollständig frei von Laufzeit- und Diagnose-Risiken.
- Größte 3 Stärken: bessere Modulgrenzen, gute Testabdeckung auf Service-Ebene, deutlich verbesserte Cleanup-Hygiene.
- Größte 3 Risiken: Chart-Group-Verlust nach Theme-Wechsel, ungeeignete fire-and-forget-Timerpfade, zu permissive JSInterop-Tests.

## 2. Was technisch gut ist

- Interop ist nicht mehr ein monolithischer Service. `ComponentInteropService` delegiert an spezialisierte Adapter wie [SwipeInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/SwipeInterop.cs#L5), [FloatingPositionInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/FloatingPositionInterop.cs#L5) und [UtilityInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/UtilityInterop.cs#L5). Das senkt die Kopplung und macht Fehlerstellen besser lokalisierbar.
- DataGrid ist sinnvoller aufgeteilt als früher. Persistenz und Serverzugriff liegen jetzt in [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L5) und [DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridServerService.cs#L5), statt alles in [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L979) zu bündeln.
- Die Interop-Verträge sind testseitig deutlich besser abgesichert. [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L1) prüft konkrete JS-Funktionsnamen und Argumente über `VerifyInvoke`.
- Frühere API-/Doku-Drifts sind bereinigt. [ComponentInteropPage.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Pages/Docs/Services/ComponentInteropPage.razor#L710) beschreibt `UnregisterToastSwipe(toastId, elementId)` passend zur Implementierung.

## 3. Konkrete Schwächen

- Titel: Chart-Theme-Wechsel verliert unter Umständen die Gruppen-Synchronisation.
  - Kategorie: echter Bug
  - Betroffene Dateien/Module: [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L89)
  - Erklärung mit Codebezug: Im Theme-Wechsel-Pfad wird das Chart via `disposeChart` + `initChart` neu aufgebaut, und danach nur `RegisterChartEventsAsync()` aufgerufen. Das `Group`-Rejoin geschieht aber nur im `if (Group != _lastGroup)`-Block. Wenn sich `Group` nicht geändert hat, wird `connectCharts` nach dem Reinit nicht erneut aufgerufen.
  - Risikoauswirkung: Gruppierte Charts können nach einem Laufzeit-Themewechsel auseinanderlaufen und nicht mehr synchron scrollen oder zoomen.
  - Priorität: hoch

- Titel: DataGrid-Autosave ist fire-and-forget und kann stale Writes erzeugen.
  - Kategorie: Race Condition
  - Betroffene Dateien/Module: [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L77), [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L979)
  - Erklärung mit Codebezug: `ScheduleAutoSave(Action persistCallback)` startet einen `System.Threading.Timer`, der nur `persistCallback()` aufruft. In [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L982) wird dabei `InvokeAsync(PersistLayoutAsync)` als Action übergeben. Das Ergebnis wird nicht awaited, und es gibt keine Sequenz-/Version-Absicherung gegen überholte Persist-Läufe.
  - Risikoauswirkung: Bei schnellen Layoutänderungen oder langsamer JS-/Storage-Ausführung kann eine ältere Persistierung nach einer neueren fertig werden und den aktuelleren Zustand überschreiben.
  - Priorität: mittel

- Titel: KeyboardShortcut-Handle hat inkonsistente Dispose-Semantik.
  - Kategorie: API-Inkonsistenz
  - Betroffene Dateien/Module: [KeyboardShortcutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/KeyboardShortcutService.cs#L125)
  - Erklärung mit Codebezug: `Dispose()` entfernt nur den lokalen Eintrag und startet JS-Cleanup fire-and-forget, während `DisposeAsync()` die JS-Bereinigung sauber awaited. Dieselbe Ressource hat damit zwei unterschiedliche Cleanup-Modelle. Der synchrone Pfad ist als öffentliche API verfügbar, aber semantisch deutlich schwächer.
  - Risikoauswirkung: Wer den Handle synchron disposet, kann JS-Registrierungen und Listener länger als erwartet im Browser behalten.
  - Priorität: mittel

- Titel: Hover-/Menu-Timer verwenden weiterhin fire-and-forget async Dispatch.
  - Kategorie: Cleanup-Risiko
  - Betroffene Dateien/Module: [HoverCard.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/HoverCard/HoverCard.razor#L38), [NavigationMenuItem.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/NavigationMenu/NavigationMenuItem.razor#L38), [MegaMenuItem.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/MegaMenu/MegaMenuItem.razor#L97)
  - Erklärung mit Codebezug: Die Timer-Callbacks rufen `InvokeAsync(async () => { ... })` auf, ohne das zurückgegebene Task-Objekt zu beobachten. Das ist besser als direkte State-Mutation aus dem Timer-Thread, aber Exceptions und späte Callbacks nach Dispose werden damit weiterhin nur unzureichend kontrolliert.
  - Risikoauswirkung: Sporadische Late-Callbacks oder verschluckte Fehler bei Hover-/Close-Interaktionen, besonders unter Last oder bei schnellem Unmount.
  - Priorität: mittel

- Titel: JSInterop-Tests bleiben bewusst zu permissiv.
  - Kategorie: Testqualität
  - Betroffene Dateien/Module: [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L16), [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L30)
  - Erklärung mit Codebezug: Der Standard-Testkontext läuft in `JSRuntimeMode.Loose`, und die „strict“ Interop-Tests setzen denselben Modus ebenfalls. Zwar wird mit `VerifyInvoke` geprüft, aber fehlende oder zusätzliche JS-Calls sind dadurch nicht maximal hart abgesichert.
  - Risikoauswirkung: Interop-Regressions können trotz grüner Tests durchrutschen.
  - Priorität: mittel

- Titel: Persistenz- und Fehlerpfade melden Probleme nur über `Console.Error`.
  - Kategorie: Robustheit
  - Betroffene Dateien/Module: [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L36), [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L345), [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L58), [CustomizerSidebar.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Shared/CustomizerSidebar.razor#L644)
  - Erklärung mit Codebezug: Mehrere `catch (Exception ex)`-Blöcke schreiben nur ein Log und fahren fort. Für Konsumenten ist dann nicht sichtbar, dass Persistenz oder Chart-Initialisierung tatsächlich fehlgeschlagen ist.
  - Risikoauswirkung: Stille Teilfehler und schlechte Diagnose im Feld.
  - Priorität: niedrig

## 4. Wahrscheinliche Bugs

- Datei: [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L89)
  - Methode/Komponente: `OnAfterRenderAsync`
  - Warum vermutlich Bug: Nach `disposeChart` + `initChart` im Theme-Wechsel-Pfad wird `connectCharts` nicht erneut ausgeführt, wenn `Group` unverändert bleibt.
  - Wie man ihn reproduzieren könnte: Zwei gruppierte Charts rendern, beide mit `Group` setzen, dann zur Laufzeit `Theme` ändern und anschließend Sync-Interaktionen prüfen.
  - Wie man ihn beheben könnte: Nach dem Reinit explizit `connectCharts(Group, ...)` erneut aufrufen oder Group-Rejoin in `RegisterChartEventsAsync()` kapseln.

- Datei: [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L77)
  - Methode/Komponente: `ScheduleAutoSave`
  - Warum vermutlich Bug: Der persistierende Callback wird als `Action` gestartet und nicht awaited. Dadurch können späte/überholte Saves oder unobservierte Fehler entstehen.
  - Wie man ihn reproduzieren könnte: Mehrere Layoutänderungen schnell hintereinander auslösen und eine langsamere Storage-/JS-Umgebung simulieren.
  - Wie man ihn beheben könnte: Callback auf `Func<Task>` umstellen und mit Generation/Version absichern.

- Datei: [KeyboardShortcutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/KeyboardShortcutService.cs#L125)
  - Methode/Komponente: `ShortcutHandle.Dispose`
  - Warum vermutlich Bug: Der synchrone Dispose-Pfad macht nur lokale Bereinigung und startet JS-Cleanup fire-and-forget. Das ist semantisch schwächer als `DisposeAsync` und kann den Browser-State länger als erwartet offenlassen.
  - Wie man ihn reproduzieren könnte: Einen Handle synchron über `Dispose()` statt `DisposeAsync()` freigeben und danach prüfen, ob JS-seitig noch Listener aktiv sind.
  - Wie man ihn beheben könnte: Sync-Dispose entfernen, auf `IAsyncDisposable` beschränken oder den JS-Cleanup-Pfad synchron/robust modellieren.

## 5. Architekturrisiken

- Timer-basierte UI-Interaktionen sind weiterhin über mehrere Komponenten verteilt. Warum: HoverCard, NavigationMenuItem und MegaMenuItem folgen demselben Muster, aber ohne gemeinsame Cancellation-/Dispatch-Strategie. Sinnvoll wäre ein kleines gemeinsames Helper-Modell.
- DataGrid bleibt trotz Aufteilung die komplexeste Stelle des Repos. Warum: Layout, Serverzugriff, Toolbar, Persistenz und Rendering sind zwar getrennt, aber die Komponente orchestriert immer noch sehr viel. Sinnvoll wären klarere Subsystem-Grenzen.
- Die JSInterop-Tests sind besser geworden, aber immer noch nicht maximal streng. Warum: `Loose` als Default lässt unerwartete Calls leichter durch. Sinnvoll wären strengere Test-Cluster für kritische Adapter.
- Fehler werden an einigen Stellen nur geloggt statt modelliert. Warum: Für Konsumenten ist das im Betrieb schwer unterscheidbar von einem normalen Zustand. Sinnvoll wären optionale Fehler-Events oder sichtbare Error-States.

## 6. Test- und Qualitätsbewertung

- Positiv: Die Suite ist groß und aktuell stabil. `dotnet build src\\Lumeo\\Lumeo.csproj -c Release` war erfolgreich, und `dotnet test tests\\Lumeo.Tests\\Lumeo.Tests.csproj -c Release --no-build` lief mit `1303/1303` durch.
- Positiv: Die kritischen Interop-Adapter sind deutlich besser abgesichert als in den früheren Ständen.
- Schwach: Das Chart-Thema `Group`-Rejoin nach Themewechsel ist nicht testseitig abgesichert, obwohl genau dort ein echter Laufzeitfehler sitzen kann.
- Schwach: Die Autosave- und Timerpfade haben Tests, aber keine echten Race-/Overlap-Fälle.
- Schwach: Der JSInterop-Basistestmodus bleibt permissiv, wodurch einige Klassen von Fehlern weiterhin schwerer auffallen.

## 7. Top-10 Maßnahmen

- Chart-Theme-Wechsel so umbauen, dass `connectCharts` nach Reinit immer erneut läuft. Nutzen: behebt den aktuellen Sync-Bug. Aufwand: `S`
- DataGrid-Autosave auf `Func<Task>` + Sequenzschutz umstellen. Nutzen: verhindert stale Writes und verdeckte Fehler. Aufwand: `M`
- `KeyboardShortcutService` auf eine einheitliche Dispose-Semantik reduzieren. Nutzen: klare API und weniger Leak-Risiko. Aufwand: `S`
- Hover-/Menu-Timer auf ein einheitliches Dispatch-/Cancellation-Muster bringen. Nutzen: reduziert Late-Callback- und Exception-Risiko. Aufwand: `M`
- Kritische JSInterop-Tests für fehlende/zusätzliche Calls härten. Nutzen: bessere Regressionserkennung. Aufwand: `M`
- Persistenz-Fehler sichtbarer machen statt nur zu loggen. Nutzen: bessere Diagnose im Feld. Aufwand: `M`
- Timer-/Autosave-Race-Tests ergänzen. Nutzen: deckt genau die harten Randfälle ab. Aufwand: `M`
- DataGrid-Architektur weiter entflechten, besonders Persistenz und Toolbar-Workflows. Nutzen: geringere Komplexität. Aufwand: `L`
- `Loose`-Interop nur dort belassen, wo es zwingend nötig ist. Nutzen: weniger maskierte Fehler. Aufwand: `S`
- Kritische Cleanup-Pfade mit gezielten Disposal-Tests absichern. Nutzen: weniger Leaks und späte Callbacks. Aufwand: `S`

## 8. Scorecard

- Architektur: `7/10`
- Codequalität: `7/10`
- Konsistenz: `8/10`
- Wartbarkeit: `6/10`
- Testqualität: `7/10`
- Produktionsreife: `7/10`
- Entwicklererlebnis: `8/10`
- Zukunftsfähigkeit: `6/10`

## 9. Finale Einschätzung

- Würde ich das Repo heute in Produktion einsetzen? Ja, aber mit klarer Einschränkung auf die Komponenten, deren kritische Laufzeitpfade abgesichert sind.
- Unter welchen Bedingungen ja/nein? Ja, wenn Chart-Group-Rejoin, DataGrid-Autosave und Cleanup-Semantik vor breiter Nutzung gehärtet werden. Nein, wenn Laufzeit-Themewechsel, häufige Layout-Speicherungen oder viele Keyboard-Shortcut-Handles mit sync Dispose zu erwarten sind.
- Was müsste vor einer 1.0 unbedingt noch passieren? Die drei Pflichtpunkte sind: Chart-Group-Rejoin nach Themewechsel fixen, DataGrid-Autosave race-sicher machen und die Dispose-/Cleanup-API des KeyboardShortcutService konsistent machen.
