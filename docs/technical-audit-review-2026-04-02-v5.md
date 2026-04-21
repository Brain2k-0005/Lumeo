# Technical Audit Review - 2026-04-02 v5

## 1. Executive Summary

- Der Code ist im aktuellen Stand klar besser als in den vorherigen Revisionen. Der Build ist sauber und die Tests laufen mit `1298/1298` grün.
- Die gröbsten früheren Interop- und Cleanup-Probleme sind weitgehend adressiert, und die Codebasis wirkt insgesamt reifer als zu Beginn des Audits.
- Trotzdem bleiben ein paar belastbare Defekte: ein Race in der DataGrid-Serverlogik, ein fehlender Timer-Cleanup-Pfad im Alert, ein Thread-Safety-Problem im Tooltip und ein Chart-Refresh-Pfad, der nach Theme-Wechsel nicht vollständig neu verdrahtet wird.
- Meine Einstufung bleibt deshalb bei `produktionsnah`, aber noch nicht frei von spürbaren Robustheitsrisiken.
- Größte 3 Stärken: klare Zerlegung der Interop-Schicht, nachvollziehbare Service-Aufteilung im DataGrid, gute Testabdeckung auf Service-Ebene.
- Größte 3 Risiken: konkurrierende DataGrid-Requests, timerbasierte UI-Logik mit Thread-/Lifecycle-Risiken, zu lockere JSInterop-Tests.

## 2. Was technisch gut ist

- Interop-Layer ist sauber entkoppelt. `ComponentInteropService` delegiert an spezialisierte Adapter wie [SwipeInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/SwipeInterop.cs#L5), [FloatingPositionInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/FloatingPositionInterop.cs#L5) und [UtilityInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/UtilityInterop.cs#L5). Das reduziert die frühere God-Class-Tendenz deutlich.
- DataGrid-Logik ist besser aufgeteilt. Die Persistenz und Serverlogik liegen jetzt in [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L5) und [DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridServerService.cs#L5) statt komplett in [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L979).
- Die Interop-Verträge sind testseitig wesentlich besser abgesichert. [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L1) prüft konkrete JS-Funktionsnamen und Argumente über `VerifyInvoke`, statt nur auf Fehlerfreiheit zu hoffen.
- Die Dokumentation ist wieder mit der API synchron. [ComponentInteropPage.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Pages/Docs/Services/ComponentInteropPage.razor#L710) beschreibt `UnregisterToastSwipe(toastId, elementId)` passend zur Implementierung in [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L230).

## 3. Konkrete Schwächen

- Titel: DataGrid-Request-Race kann Loading-State falsch zurücksetzen.
  - Kategorie: Bug-Risiko
  - Betroffene Dateien/Module: [DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridServerService.cs#L13)
  - Erklärung mit Codebezug: `RequestDataAsync` setzt `IsLoading = true`, ersetzt dann `_requestCts` und behandelt Cancellation im `catch (OperationCanceledException)`, aber der `finally`-Block setzt immer `IsLoading = false`. Wenn Request A von Request B abgelöst wird, kann A nach seiner Cancellation den Loading-State von B versehentlich wieder löschen.
  - Risikoauswirkung: Flackernder oder falscher Loading-Indikator bei schneller Suche, Paging oder Filterwechsel.
  - Priorität: hoch

- Titel: Alert entfernt den Auto-Dismiss-Timer beim manuellen Dismiss nicht.
  - Kategorie: echter Bug
  - Betroffene Dateien/Module: [Alert.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Alert/Alert.razor#L74)
  - Erklärung mit Codebezug: `Dismiss()` setzt `_isDismissed = true` und feuert `OnDismiss`, aber `_autoDismissTimer` wird dort nicht disposed. Der Timer kann später trotzdem `AutoDismissCallback` ausführen und `OnDismiss` ein zweites Mal triggern.
  - Risikoauswirkung: Doppelte Dismiss-Events, doppelte Side-Effects und schwer reproduzierbare UI-Zustände.
  - Priorität: hoch

- Titel: Tooltip mutiert State direkt aus dem Timer-Thread.
  - Kategorie: echter Bug
  - Betroffene Dateien/Module: [Tooltip.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Tooltip/Tooltip.razor#L22)
  - Erklärung mit Codebezug: In `HandleMouseEnter` und `HandleMouseLeave` setzt der `System.Threading.Timer` direkt `_isOpen = true/false` und ruft erst danach `InvokeAsync(StateHasChanged)`. Die State-Mutation passiert also außerhalb des Blazor-Dispatchers.
  - Risikoauswirkung: Thread-Safety-Probleme, sporadische Renderer-Fehler und nicht deterministisches Open/Close-Verhalten unter Last.
  - Priorität: hoch

- Titel: Chart-Theme-Refresh verliert Event- und Group-Registrierung.
  - Kategorie: Bug-Risiko
  - Betroffene Dateien/Module: [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L54)
  - Erklärung mit Codebezug: Beim ersten Render werden Events registriert und Gruppen verbunden, z. B. bei [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L67) und [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L79). Im Theme-Wechsel-Pfad ab [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L105) wird nur `disposeChart` plus `initChart` ausgeführt. Die Event-Registrierung und ggf. das erneute `connectCharts` fehlen dort.
  - Risikoauswirkung: Nach Laufzeit-Themewechsel können Chart-Interaktionen oder Sync-Gruppen still verloren gehen.
  - Priorität: mittel

- Titel: JSInterop-Tests sind zwar erweitert, aber weiterhin nicht streng.
  - Kategorie: Testqualität
  - Betroffene Dateien/Module: [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L16), [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L30)
  - Erklärung mit Codebezug: Der globale Test-Context läuft in `JSRuntimeMode.Loose`. Zwar prüft [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L54) konkrete Invocations via `VerifyInvoke`, aber fehlende JS-Aufrufe oder zusätzliche falsche Aufrufe werden in großen Teilen des Test-Stacks weiterhin nur begrenzt sichtbar.
  - Risikoauswirkung: Scheinbar grüne Tests können Interop-Regressionspfade übersehen.
  - Priorität: mittel

- Titel: Fehler werden in mehreren Persistenzpfaden nur geloggt.
  - Kategorie: Robustheit
  - Betroffene Dateien/Module: [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L36), [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L345), [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L58), [CustomizerSidebar.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Shared/CustomizerSidebar.razor#L644)
  - Erklärung mit Codebezug: Mehrere Stellen fangen `Exception` und schreiben nur nach `Console.Error`. Für Konsumenten ist dann nicht erkennbar, dass Speicherzustand, Layout oder Chart-Initialisierung fehlgeschlagen ist.
  - Risikoauswirkung: Stille Teilfehler, die als „UI reagiert komisch“ statt als klarer Fehler sichtbar werden.
  - Priorität: mittel

## 4. Wahrscheinliche Bugs

- Datei: [DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridServerService.cs#L13)
  - Methode/Komponente: `RequestDataAsync`
  - Warum vermutlich Bug: Das `finally` setzt `IsLoading` für jede beendete Anfrage zurück, auch wenn diese Anfrage von einer neueren abgelöst wurde.
  - Wie man ihn reproduzieren könnte: Schnelle `OnServerRequest`-Calls mit Filter- oder Suchänderungen auslösen; die ältere, gecancelte Anfrage läuft in ihren `finally` und kann den Loading-State der neueren Anfrage überschreiben.
  - Wie man ihn beheben könnte: Eine Request-Generation oder lokale Token-Referenz speichern und `IsLoading` nur für den aktuellsten Request zurücksetzen.

- Datei: [Alert.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Alert/Alert.razor#L96)
  - Methode/Komponente: `Dismiss`
  - Warum vermutlich Bug: Der manuelle Dismiss-Pfad räumt den laufenden Auto-Dismiss-Timer nicht auf.
  - Wie man ihn reproduzieren könnte: Alert mit `AutoDismissMs` anzeigen, vor Ablauf manuell schließen und auf den späteren Timer-Callback warten.
  - Wie man ihn beheben könnte: In `Dismiss()` den Timer disposen und auf `null` setzen, plus im Callback `if (_isDismissed) return;` absichern.

- Datei: [Tooltip.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Tooltip/Tooltip.razor#L22)
  - Methode/Komponente: `HandleMouseEnter` / `HandleMouseLeave`
  - Warum vermutlich Bug: `_isOpen` wird aus einem `Timer`-Callback außerhalb des Renderer-Dispatchers gesetzt.
  - Wie man ihn reproduzieren könnte: In Blazor Server schnell zwischen Hover rein und raus wechseln, während der Timer feuert. Sporadische Renderer- oder Zustandsfehler sind möglich.
  - Wie man ihn beheben könnte: Den kompletten State-Update-Block in `InvokeAsync(() => { ... })` kapseln.

- Datei: [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L105)
  - Methode/Komponente: `OnAfterRenderAsync`
  - Warum vermutlich Bug: Nach `disposeChart` + `initChart` im Theme-Wechsel-Pfad werden die Chart-Events und Group-Verbindungen nicht neu registriert.
  - Wie man ihn reproduzieren könnte: Ein Chart mit Event-Callbacks oder `Group` rendern, zur Laufzeit `Theme` ändern und danach auf Klick/Sync-Verhalten prüfen.
  - Wie man ihn beheben könnte: Nach Re-Init dieselben Registrierungsschritte wie im `firstRender`-Block ausführen.

## 5. Architekturrisiken

- Die timerbasierte UI-Logik in Tooltip, HoverCard, NavigationMenu, MegaMenu und Combobox funktioniert aktuell, ist aber langfristig fehleranfällig. Warum: Mehrere Komponenten mischen `Timer`, `InvokeAsync` und asynchrone UI-Updates ohne ein gemeinsames Muster. Sinnvoll wäre ein kleiner helper oder ein einheitlicher Dispatch-Wrapper.
- `DataGrid` bleibt trotz ausgelagerter Services die komplexeste Komponente. Warum: Persistenz, Serverzugriff, Toolbar, Layout und Rendering sind zwar besser getrennt, aber die Komponente orchestriert immer noch sehr viel. Sinnvoll wären klarere Grenzen zwischen View, State und I/O.
- Der Fehlerpfad in Persistenz- und Chart-Code ist für Konsumenten nicht stark genug sichtbar. Warum: Mehrere `catch`-Blöcke loggen nur und machen weiter. Sinnvoll wären explizite Fehlerzustände oder optionale Fehler-Callbacks.
- Die JSInterop-Tests wirken strenger als sie im Standard-Setup sind. Warum: Der Grundmodus bleibt `Loose`, während nur ausgewählte Pfade mit `VerifyInvoke` abgesichert sind. Sinnvoll wären mehr komponentennahe Tests für echte Interaktionspfade.

## 6. Test- und Qualitätsbewertung

- Positiv ist die Breite der Suite. `dotnet build src\\Lumeo\\Lumeo.csproj -c Release` ist grün, und `dotnet test tests\\Lumeo.Tests\\Lumeo.Tests.csproj -c Release --no-build` lief zuletzt mit `1298/1298` durch.
- Gut abgesichert sind vor allem Service- und Adapterverträge, insbesondere rund um Interop, DataGrid-Serverlogik und Layout-Persistenz.
- Untertestet bleiben die kritischen UI-Randfälle: Timer-/Threading-Verhalten in Hover-/Tooltip-Komponenten, concurrent DataGrid-Requests und Chart-Reinitialisierung nach Themewechsel.
- CI ist funktional, aber weiterhin stärker auf „grün bauen“ als auf „kritische Laufzeitpfade realistisch testen“ optimiert.
- Fehlen tun aus meiner Sicht mindestens ein paar kompakte Verhaltens-Tests für die oben genannten Defekte, plus ein etwas strengerer Interop-Modus für gezielte Testgruppen.

## 7. Top-10 Maßnahmen

- Request-Generation im DataGrid-Serverpfad einführen. Nutzen: behebt den Loading-State-Race. Aufwand: `M`
- Auto-Dismiss-Timer im Alert beim manuellen Dismiss disposen. Nutzen: verhindert doppelte Side-Effects. Aufwand: `S`
- Tooltip-State nur noch im Blazor-Dispatcher ändern. Nutzen: beseitigt Thread-Safety-Risiko. Aufwand: `S`
- Chart-Theme-Refresh so umbauen, dass Events und Groups nach `initChart` erneut registriert werden. Nutzen: verhindert stille Interaktionsverluste. Aufwand: `M`
- Timer-basierte Hover-/Menu-Komponenten auf ein gemeinsames Dispatch-Muster bringen. Nutzen: weniger Laufzeit- und Cleanup-Risiko. Aufwand: `M`
- JSInterop-Tests für kritische Pfade ergänzen. Nutzen: bessere Regressionserkennung bei Element-IDs, Event-Namen und Dispose-Fällen. Aufwand: `M`
- Persistenzfehler für DataGrid und Chart sichtbarer machen. Nutzen: bessere Diagnose für Anwender. Aufwand: `M`
- `Loose` nur dort verwenden, wo es zwingend nötig ist. Nutzen: härtere Tests ohne unnötige Maskierung. Aufwand: `S`
- DataGrid-Architektur weiter entkoppeln, insbesondere Toolbar und Persistenz-Workflows. Nutzen: bessere Wartbarkeit. Aufwand: `L`
- Kritische Timer-/AutoSave-Pfade mit kleinen Race-Tests absichern. Nutzen: verhindert Regressionen bei den schwersten Defekten. Aufwand: `M`

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

- Würde ich das Repo heute in Produktion einsetzen? Ja, aber mit klarer Einschränkung auf die stabileren Komponenten und nicht als „alles ist fertig“-Freigabe.
- Unter welchen Bedingungen ja/nein? Ja, wenn die riskanten Pfade rund um DataGrid-Requests, Alert-Autodismiss, Tooltip-Threading und Chart-Reinit mindestens testseitig abgesichert werden. Nein, wenn eine höhere Last oder viele konkurrierende UI-Aktionen zu erwarten sind, ohne diese Punkte vorher zu härten.
- Was müsste vor einer 1.0 unbedingt noch passieren? Die drei Pflichtpunkte sind: Request-Race im DataGrid beseitigen, timerbasierte UI-State-Updates thread-sicher machen und den Chart-Refresh-Pfad nach Themewechsel vollständig reinitialisieren.
