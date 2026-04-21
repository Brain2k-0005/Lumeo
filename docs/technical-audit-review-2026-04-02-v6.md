# Technical Audit Review - 2026-04-02 v6

## 1. Executive Summary

- Der Code ist erneut spürbar besser geworden. `dotnet build` ist grün, und die Tests laufen aktuell mit `1303/1303`.
- Die frühere Großbaustelle rund um Interop ist deutlich sauberer zerlegt, und die DataGrid-Logik ist inzwischen besser modularisiert.
- Trotzdem bleiben vier belastbare Risikoachsen: ein echter Loading-State-Race im DataGrid-Serverpfad, timerbasierte UI-State-Mutationen außerhalb des Blazor-Dispatchers, ein Debounce-Pfad im Combobox-Search mit async Fire-and-Forget, und ein Chart-Theme-Wechsel, der Event-/Group-Registrierung nicht vollständig neu aufsetzt.
- Meine Einstufung bleibt deshalb `produktionsnah`, aber weiterhin mit klaren Stellen, die vor einer 1.0 gehärtet werden sollten.
- Größte 3 Stärken: klarere Modulgrenzen, gute Interop- und Service-Testabdeckung, saubere Cleanup-Hygiene bei vielen Komponenten.
- Größte 3 Risiken: DataGrid-Request-Race, timerbasierte Async-Pfade, lockerer JSInterop-Testmodus.

## 2. Was technisch gut ist

- Interop ist jetzt besser entkoppelt. `ComponentInteropService` delegiert an spezialisierte Adapter wie [SwipeInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/SwipeInterop.cs#L5), [FloatingPositionInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/FloatingPositionInterop.cs#L5) und [UtilityInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/UtilityInterop.cs#L5). Das reduziert die frühere Cross-Talk- und God-Service-Problematik.
- DataGrid ist intern besser aufgeteilt. Persistenz und Serverzugriff liegen in [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L5) und [DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridServerService.cs#L5), statt alles in [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L979) zu bündeln.
- Die Interop-Verträge sind testseitig präziser geworden. [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L1) prüft konkrete JS-Funktionsnamen und Argumente via `VerifyInvoke`.
- Dokumentation und API sind wieder synchron. [ComponentInteropPage.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Pages/Docs/Services/ComponentInteropPage.razor#L710) beschreibt `UnregisterToastSwipe(toastId, elementId)` passend zur Implementierung in [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L230).

## 3. Konkrete Schwächen

- Titel: DataGrid kann bei konkurrierenden Server-Requests den Loading-State falsch zurücksetzen.
  - Kategorie: echter Bug
  - Betroffene Dateien/Module: [DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridServerService.cs#L13)
  - Erklärung mit Codebezug: `RequestDataAsync` setzt `IsLoading = true`, ersetzt dann `_requestCts`, und der `finally`-Block setzt immer `IsLoading = false`. Wenn Request A von Request B abgelöst wird, kann A nach seiner Cancellation den Loading-State von B wieder löschen.
  - Risikoauswirkung: Flackernder oder falscher Loading-Status bei schneller Suche, Paging oder Filterwechsel.
  - Priorität: hoch

- Titel: Tooltip mutiert UI-State direkt aus dem Timer-Thread.
  - Kategorie: echtes Bug-Risiko
  - Betroffene Dateien/Module: [Tooltip.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Tooltip/Tooltip.razor#L22)
  - Erklärung mit Codebezug: In `HandleMouseEnter` und `HandleMouseLeave` setzt der `System.Threading.Timer` direkt `_isOpen = true/false` und ruft erst danach `InvokeAsync(StateHasChanged)`. Die State-Mutation selbst passiert damit außerhalb des Blazor-Dispatchers.
  - Risikoauswirkung: Sporadische Renderer-Fehler, nicht-deterministisches Open/Close-Verhalten und schwer reproduzierbare Zustände unter Last.
  - Priorität: hoch

- Titel: Combobox-Debounce ist async fire-and-forget und nicht gegen Stale Results abgesichert.
  - Kategorie: Bug-Risiko
  - Betroffene Dateien/Module: [Combobox.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Combobox/Combobox.razor#L111)
  - Erklärung mit Codebezug: `OnSearchChanged` startet einen `Timer` mit `async _ =>` und ruft innerhalb davon `OnSearchAsync.InvokeAsync(text)` über `InvokeAsync(...)` auf. Der vorherige Timer wird zwar disposed, aber ein bereits laufender Callback bleibt unkontrolliert. Es gibt kein Request-Token oder Sequenz-Guard gegen out-of-order Search-Results.
  - Risikoauswirkung: Alte Suchläufe können nach einer neuen Eingabe noch Ergebnisse liefern und den aktuelleren Zustand überschreiben.
  - Priorität: mittel

- Titel: Chart-Theme-Wechsel setzt Chart zwar neu auf, verdrahtet aber Events und Gruppen nicht vollständig neu.
  - Kategorie: Bug-Risiko
  - Betroffene Dateien/Module: [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L105)
  - Erklärung mit Codebezug: Im `firstRender`-Pfad werden `registerChartEvent` und `connectCharts` aufgerufen, z. B. in [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L67) und [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L79). Im Theme-Wechsel-Pfad ab [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L105) passiert nur `disposeChart` plus `initChart`. Die Event-Registrierung und das erneute `connectCharts` fehlen dort.
  - Risikoauswirkung: Nach Laufzeit-Themewechsel können Klick-/Zoom-Events oder Gruppensynchronisation still verloren gehen.
  - Priorität: mittel

- Titel: JSInterop-Testsetup bleibt bewusst locker und maskiert Negativfälle.
  - Kategorie: Testqualität
  - Betroffene Dateien/Module: [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L16), [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L30)
  - Erklärung mit Codebezug: Der Standard-Testkontext läuft in `JSRuntimeMode.Loose`, und auch die gezielten Interop-Tests stellen die Module auf `Loose`. [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L54) prüft dann zwar erwartete Aufrufe, aber fehlende oder zusätzliche JS-Aufrufe werden weiterhin nur begrenzt sichtbar.
  - Risikoauswirkung: Grüne Tests können Interop-Regressionspfade übersehen.
  - Priorität: mittel

- Titel: Persistenz- und Chart-Fehler werden weiterhin nur geloggt.
  - Kategorie: Robustheit
  - Betroffene Dateien/Module: [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L36), [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L345), [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L58), [CustomizerSidebar.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Shared/CustomizerSidebar.razor#L644)
  - Erklärung mit Codebezug: Mehrere `catch (Exception ex)`-Blöcke schreiben nur nach `Console.Error`. Für Konsumenten ist dann nicht klar, ob Layout-Persistenz, Chart-Init oder Theme-Laden fehlgeschlagen ist.
  - Risikoauswirkung: Stille Teilfehler und schwache Diagnose im Feld.
  - Priorität: niedrig

## 4. Wahrscheinliche Bugs

- Datei: [DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridServerService.cs#L13)
  - Methode/Komponente: `RequestDataAsync`
  - Warum vermutlich Bug: Ein gecancelter, älterer Request kann im `finally` den Loading-State zurücksetzen, obwohl ein neuerer Request bereits läuft.
  - Wie man ihn reproduzieren könnte: Einen langsamen Server-Request starten, kurz danach einen zweiten Request auslösen, dann den ersten abbrechen lassen, während der zweite noch läuft.
  - Wie man ihn beheben könnte: Request-Generation oder aktuelle CTS referenzieren und `IsLoading` nur für den jeweils aktuellen Request zurücksetzen.

- Datei: [Tooltip.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Tooltip/Tooltip.razor#L22)
  - Methode/Komponente: `HandleMouseEnter` / `HandleMouseLeave`
  - Warum vermutlich Bug: Der Timer-Callback schreibt direkt in `_isOpen`, statt den gesamten Zustand im UI-Dispatcher zu ändern.
  - Wie man ihn reproduzieren könnte: Tooltip schnell ein- und ausfahren, insbesondere unter Blazor Server mit verzögerten Timer-Ticks.
  - Wie man ihn beheben könnte: Den kompletten State-Block in `InvokeAsync(() => { ... })` kapseln und `_isOpen` nur dort ändern.

- Datei: [Combobox.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Combobox/Combobox.razor#L111)
  - Methode/Komponente: `OnSearchChanged`
  - Warum vermutlich Bug: Ein bereits gestarteter Debounce-Callback kann noch nach einer neuen Eingabe fertig werden und ältere Suchergebnisse nach vorne schieben.
  - Wie man ihn reproduzieren könnte: Sehr schnell mehrere Suchbegriffe tippen und einen langsamen `OnSearchAsync`-Handler verwenden.
  - Wie man ihn beheben könnte: Debounce mit CancellationToken oder Sequenznummer versehen und nur das letzte Ergebnis akzeptieren.

- Datei: [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L105)
  - Methode/Komponente: `OnAfterRenderAsync`
  - Warum vermutlich Bug: Beim Theme-Wechsel wird das Chart neu initialisiert, aber die Event- und Group-Registrierung wird nicht wiederholt.
  - Wie man ihn reproduzieren könnte: Chart mit Event-Callback oder `Group` rendern, dann zur Laufzeit `Theme` ändern und danach interagieren.
  - Wie man ihn beheben könnte: Nach `initChart(...)` die Event- und Group-Registrierung erneut ausführen.

## 5. Architekturrisiken

- Timer-basierte UI-Interaktionen sind weiterhin über mehrere Komponenten verteilt. Warum: Tooltip, Combobox, NavigationMenu, MegaMenu und Hover-Pattern folgen nicht einem einheitlichen Dispatch-/Cancellation-Modell. Sinnvoll wäre ein kleines gemeinsames Muster für Timer + `InvokeAsync`.
- DataGrid bleibt trotz Aufteilung die schwerste Komponente im Repo. Warum: Rendering, Layout, Serverzugriff, Export und Toolbar-Logik sind zwar besser getrennt, aber die Orchestrierung ist immer noch sehr breit. Sinnvoll wären klarere Subsystem-Grenzen.
- Der JSInterop-Testmodus ist zu permissiv für ein Komponenten-Repo mit vielen Browser-Calls. Warum: Der Default `Loose`-Modus lässt falsche oder fehlende Invocations leichter durch. Sinnvoll wären strengere Tests für kritische Adapter.
- Fehler werden an einigen Stellen nur geloggt statt modelliert. Warum: Das erschwert die Diagnose und macht Fehlkonfigurationen im Feld leise. Sinnvoll wären optionale Fehler-Events oder sichtbare Error-States.

## 6. Test- und Qualitätsbewertung

- Positiv: Die Suite ist groß und aktuell stabil. `dotnet build src\\Lumeo\\Lumeo.csproj -c Release` war erfolgreich, und `dotnet test tests\\Lumeo.Tests\\Lumeo.Tests.csproj -c Release --no-build` lief mit `1303/1303` durch.
- Positiv: Interop-Adapter und DataGrid-Serverlogik sind deutlich besser getestet als in den früheren Ständen.
- Schwach: Die kritischsten Runtime-Randfälle sind weiterhin untertestet, vor allem Threading-/Timer-Verhalten und Laufzeit-Themewechsel bei Chart.
- Schwach: Das globale JSInterop-Setup bleibt `Loose`, wodurch Negativfälle und zu viele unerwartete Calls nicht hart genug auffallen.
- Fehlend: Kleine Defekt-Tests für DataGrid-Request-Races, Tooltip-Timer-Threading, Combobox-Debounce und Chart-Reinit nach Themewechsel.

## 7. Top-10 Maßnahmen

- Request-Generation im DataGrid-Serverpfad einführen. Nutzen: behebt den Loading-State-Race. Aufwand: `M`
- Tooltip-State nur noch im Blazor-Dispatcher ändern. Nutzen: beseitigt Thread-Safety-Risiko. Aufwand: `S`
- Combobox-Debounce mit CancellationToken oder Sequenznummer absichern. Nutzen: verhindert Stale Results. Aufwand: `M`
- Chart-Reinit nach Themewechsel vollständig neu verdrahten. Nutzen: vermeidet verlorene Events und Sync-Gruppen. Aufwand: `M`
- JSInterop-Tests für negative Fälle und fehlende Invocations schärfen. Nutzen: bessere Regressionserkennung. Aufwand: `M`
- Lockere `JSRuntimeMode.Loose` nur dort belassen, wo es wirklich nötig ist. Nutzen: härtere Adapter-Tests. Aufwand: `S`
- Persistenzfehler sichtbarer machen statt nur `Console.Error`. Nutzen: bessere Feld-Diagnose. Aufwand: `M`
- Timer-basierte Hover-/Menu-Komponenten auf ein gemeinsames Muster bringen. Nutzen: weniger Lifecycle-Risiko. Aufwand: `M`
- DataGrid-Architektur weiter entflechten, besonders Toolbar und Persistenzpfade. Nutzen: bessere Wartbarkeit. Aufwand: `L`
- Kritische Rennen mit kleinen Verhaltens-Tests absichern. Nutzen: verhindert Regressionen in schwer testbaren Pfaden. Aufwand: `M`

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

- Würde ich das Repo heute in Produktion einsetzen? Ja, aber nicht ohne die oben genannten Risiko-Pfade zu härten.
- Unter welchen Bedingungen ja/nein? Ja für den aktuellen Stand, wenn DataGrid-Request-Races, Tooltip-Threading und Chart-Reinit vor breiter Nutzung abgesichert werden. Nein, wenn viele gleichzeitige interaktive UI-Aktionen zu erwarten sind und diese Pfade unverändert bleiben.
- Was müsste vor einer 1.0 unbedingt noch passieren? Die drei Pflichtpunkte sind: DataGrid-Race beseitigen, timerbasierte State-Updates thread-sicher machen und den Chart-Refresh-Pfad vollständig reinitialisieren.
