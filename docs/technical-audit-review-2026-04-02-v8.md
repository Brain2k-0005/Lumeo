# Technical Audit Review - 2026-04-02 v8

## 1. Executive Summary

- Der Code ist weiterhin in gutem Zustand. `dotnet build` ist grün und die Tests laufen mit `1303/1303`.
- Im Vergleich zu den früheren Audits ist die Basis klar robuster: Interop ist modularer, viele Cleanup-Pfade sind sauberer, und der KeyboardShortcut-Service ist jetzt API-konsistent.
- Die verbleibenden Risiken sind enger, aber immer noch real: Chart-Group-Rejoin nach Theme-Wechsel, ein fire-and-forget Autosave-Pfad im DataGrid, permissive JSInterop-Tests und mehrere Stellen mit nur geloggten Fehlern.
- Ich würde den Stand als `produktionsnah` bewerten, aber noch nicht als „ohne bekannte Laufzeit-Fallen“.
- Größte 3 Stärken: bessere Modulgrenzen, gute Service-Testabdeckung, konsistentere Dispose-Hygiene.
- Größte 3 Risiken: Chart-Reinit/Synchronisation, DataGrid-Autosave-Races, lockere Interop-Tests.

## 2. Was technisch gut ist

- Interop ist entkoppelt und verständlich. `ComponentInteropService` delegiert an spezialisierte Adapter wie [SwipeInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/SwipeInterop.cs#L5), [FloatingPositionInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/FloatingPositionInterop.cs#L5) und [UtilityInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/UtilityInterop.cs#L5). Das reduziert die frühere Cross-Talk-Risikooberfläche deutlich.
- DataGrid ist aufgeräumter aufgeteilt. Persistenz und Server-Requests liegen in [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L5) und [DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridServerService.cs#L5), statt komplett in [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L979) zu stecken.
- Die Interop-Verträge sind testseitig sauberer abgesichert. [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L1) prüft konkrete JS-Funktionsnamen und Argumente über `VerifyInvoke`.
- Cleanup-Hygiene ist deutlich besser geworden. Besonders der frühere KeyboardShortcut-Leak-Risikopfad ist inzwischen durch einen einheitlichen async Dispose-Pfad ersetzt.

## 3. Konkrete Schwächen

- Titel: Chart-Theme-Wechsel verliert unter Umständen die Gruppen-Synchronisation.
  - Kategorie: echter Bug
  - Betroffene Dateien/Module: [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L89)
  - Erklärung mit Codebezug: Im Theme-Wechsel-Pfad wird das Chart via `disposeChart` + `initChart` neu aufgebaut und danach nur `RegisterChartEventsAsync()` aufgerufen. Das `Group`-Rejoin passiert separat nur im `if (Group != _lastGroup)`-Block. Wenn `Group` unverändert bleibt, wird `connectCharts` nach dem Reinit nicht erneut ausgeführt.
  - Risikoauswirkung: Gruppierte Charts können nach einem Laufzeit-Themewechsel aus der Synchronisation fallen.
  - Priorität: hoch

- Titel: DataGrid-Autosave bleibt ein fire-and-forget-Pfad.
  - Kategorie: Race Condition
  - Betroffene Dateien/Module: [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L82), [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L979)
  - Erklärung mit Codebezug: `ScheduleAutoSave(Action persistCallback)` startet einen `Timer`, der nur `persistCallback()` ausführt. In [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L983) wird dabei `InvokeAsync(() => PersistLayoutAsync(gen + 1))` übergeben. Der Rückgabewert wird nicht awaited, und es gibt keine harte Sequenz-/Version-Absicherung gegen überholte Saves.
  - Risikoauswirkung: Ein älterer Autosave kann später fertig werden und einen neueren Layoutzustand überschreiben.
  - Priorität: mittel

- Titel: JSInterop-Tests bleiben bewusst permissiv.
  - Kategorie: Testqualität
  - Betroffene Dateien/Module: [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L16), [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L30)
  - Erklärung mit Codebezug: Der globale Test-Context läuft in `JSRuntimeMode.Loose`, und auch die gezielten Interop-Tests setzen den gleichen Modus. Zwar werden bestimmte Calls mit `VerifyInvoke` geprüft, aber fehlende oder zusätzliche JS-Aufrufe bleiben dadurch weniger hart sichtbar als in einem strikt kontrollierten Setup.
  - Risikoauswirkung: Interop-Regressionen können trotz grüner Tests durchrutschen.
  - Priorität: mittel

- Titel: Fehlerpfade melden Probleme oft nur per Console-Log.
  - Kategorie: Robustheit
  - Betroffene Dateien/Module: [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L37), [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L345), [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L58), [CustomizerSidebar.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Shared/CustomizerSidebar.razor#L644)
  - Erklärung mit Codebezug: Mehrere `catch (Exception ex)`-Blöcke schreiben nur nach `Console.Error` und fahren weiter. Für Konsumenten ist dann nicht klar, dass Persistenz, Theme-Laden oder Chart-Init tatsächlich fehlgeschlagen ist.
  - Risikoauswirkung: Stille Teilfehler und schlechte Diagnose im Betrieb.
  - Priorität: niedrig

## 4. Wahrscheinliche Bugs

- Datei: [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L89)
  - Methode/Komponente: `OnAfterRenderAsync`
  - Warum vermutlich Bug: Nach `disposeChart` + `initChart` im Theme-Wechsel-Pfad wird `connectCharts` nicht erneut ausgeführt, wenn `Group` unverändert bleibt.
  - Wie man ihn reproduzieren könnte: Zwei gruppierte Charts rendern, `Group` setzen, dann zur Laufzeit `Theme` ändern und die Synchronsierung testen.
  - Wie man ihn beheben könnte: Nach dem Reinit explizit `connectCharts(Group, ...)` erneut aufrufen oder den Rejoin in `RegisterChartEventsAsync()` kapseln.

- Datei: [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L82)
  - Methode/Komponente: `ScheduleAutoSave`
  - Warum vermutlich Bug: Der persistierende Callback wird nur angestoßen und nicht awaited, wodurch überholte Persist-Läufe oder unobservierte Fehler möglich bleiben.
  - Wie man ihn reproduzieren könnte: Schnell hintereinander Layoutänderungen auslösen, während die Persistenz verzögert ist.
  - Wie man ihn beheben könnte: Callback auf `Func<Task>` ändern und mit Versionierung bzw. Cancellation absichern.

## 5. Architekturrisiken

- Timerbasierte UI-Pfade sind weiterhin über mehrere Komponenten verteilt. Warum: HoverCard, NavigationMenuItem, MegaMenuItem und ähnliche Muster folgen keinem gemeinsamen Dispatch-/Cancellation-Standard. Sinnvoll wäre ein kleiner, zentraler Helper für Timer-Callbacks.
- DataGrid bleibt die schwerste Komponente im Repo. Warum: Auch wenn Services ausgelagert sind, orchestriert die Komponente immer noch sehr viele Zuständigkeiten. Sinnvoll wären noch klarere Grenzen zwischen View, State und I/O.
- Der JSInterop-Testmodus ist konservativ nur teilweise streng. Warum: `Loose` erleichtert das Testen, schwächt aber die Aussagekraft bei fehlenden oder falschen JS-Aufrufen. Sinnvoll wären zusätzliche harte Adapter-Tests.
- Fehler werden an einzelnen Stellen nur geloggt statt modelliert. Warum: Das erschwert Fehlersichtbarkeit für Konsumenten. Sinnvoll wären optionale Fehler-Events oder sichtbare Error-States.

## 6. Test- und Qualitätsbewertung

- Positiv: Die Suite ist groß und stabil. `dotnet build src\\Lumeo\\Lumeo.csproj -c Release` war erfolgreich, und `dotnet test tests\\Lumeo.Tests\\Lumeo.Tests.csproj -c Release --no-build` lief mit `1303/1303` durch.
- Positiv: Interop und zentrale Services sind deutlich besser abgesichert als in den früheren Ständen.
- Schwach: Der Chart-Theme-Wechsel ist weiterhin nicht testseitig abgesichert, obwohl dort ein echter Laufzeitfehler sitzt.
- Schwach: Autosave- und Timer-Pfade haben zwar Tests, aber keine echten Race-/Overlap-Fälle.
- Schwach: Das JSInterop-Setup bleibt permissiv; die Tests sind gut, aber nicht maximal hart.

## 7. Top-10 Maßnahmen

- Chart-Reinit nach Themewechsel so umbauen, dass `connectCharts` immer erneut ausgeführt wird. Nutzen: behebt den aktuellen Sync-Bug. Aufwand: `S`
- DataGrid-Autosave auf einen awaited Callback mit Sequenzschutz umstellen. Nutzen: verhindert stale Writes. Aufwand: `M`
- Kritische JSInterop-Tests härter machen. Nutzen: bessere Regressionserkennung. Aufwand: `M`
- Timer-/Hover-Komponenten auf ein gemeinsames Dispatch-Muster bringen. Nutzen: weniger Laufzeit- und Cleanup-Risiko. Aufwand: `M`
- Fehlerpfade sichtbarer machen statt nur zu loggen. Nutzen: bessere Diagnose im Feld. Aufwand: `M`
- DataGrid-Architektur weiter entflechten. Nutzen: bessere Wartbarkeit. Aufwand: `L`
- Chart-Themewechsel mit einem kleinen Verhaltens-Test absichern. Nutzen: verhindert Regression. Aufwand: `S`
- Autosave-/Timer-Races gezielt testen. Nutzen: deckt die schwersten Randfälle ab. Aufwand: `M`
- `Loose`-Interop nur dort belassen, wo es zwingend nötig ist. Nutzen: weniger maskierte Fehler. Aufwand: `S`
- Cleanup-Pfade in Services weiter vereinheitlichen. Nutzen: weniger versteckte Lifecycle-Probleme. Aufwand: `M`

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

- Würde ich das Repo heute in Produktion einsetzen? Ja, aber nicht ohne die verbleibenden Risiko-Pfade bewusst zu akzeptieren.
- Unter welchen Bedingungen ja/nein? Ja für den aktuellen Stand, wenn Chart-Sync, Autosave und Interop-Tests nicht als „später“ behandelt werden. Nein, wenn Laufzeit-Themewechsel und schnelle Layoutänderungen ein realistischer Teil der Nutzung sind und die Pfade unverändert bleiben.
- Was müsste vor einer 1.0 unbedingt noch passieren? Die drei Pflichtpunkte sind: Chart-Group-Rejoin nach Themewechsel fixen, DataGrid-Autosave race-sicher machen und das Testsetup für kritische JS-Interop-Pfade schärfen.
