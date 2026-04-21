# Technisches Audit

## Review 1

## 1. Executive Summary

- Gesamturteil: Das Repo ist kein Hobbyprojekt mehr, sondern eine ernsthafte, bereits nutzbare Blazor-Component-Library mit breiter Oberfläche, konsistenter Grundästhetik und einer für Open-Source-Verhältnisse starken Testmenge. Positiv sind die saubere Trennung zwischen Library, Docs-App und Tests sowie die vielen kleinen, gut komponierten Razor-Bausteine. Technisch kippt die Qualität aber dort, wo die Komplexität stark ansteigt: `DataGrid`, JS-Interop und servicegetriebene Provider sind deutlich schwächer als die einfachen Komponenten. Mehrere Stellen sind funktional wahrscheinlich falsch, obwohl die Test-Suite grün ist. Das Hauptproblem ist nicht “unsauberer Stil”, sondern dass zentrale Infrastrukturpfade zu viele Verantwortlichkeiten bündeln und deshalb versteckte Cross-Component-Bugs zulassen. Die Testabdeckung ist zahlenmäßig hoch, deckt aber gerade die riskantesten Pfade nur unzureichend ab. Build und Tests laufen lokal, aber die Qualitätsbarriere ist für ein 1.0-Release noch zu dünn. Mein Urteil: produktionsnah, aber nicht 1.0-reif.
- Einschätzung: `produktionsnah`
- Größte 3 Stärken:
- Gute Komposition und Wiederverwendung im Overlay-/Dialog-Bereich, z. B. Fokusfalle und Scroll-Lock in [DialogContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Dialog/DialogContent.razor#L58) und [SheetContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Sheet/SheetContent.razor#L57).
- Solider imperative Service-Layer für Toaster, Themes und Overlays, z. B. [ToastService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ToastService.cs#L11) und [OverlayService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/OverlayService.cs#L9).
- Breite Testbasis: `dotnet test ... --no-build` lief erfolgreich mit `965` bestandenen Tests.
- Größte 3 Risiken:
- Der zentrale Interop-Layer routet mehrere Ereignisse global statt instanzbezogen, z. B. in [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L147), [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L180), [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L216).
- `DataGrid` ist als 1100+-Zeilen-Komponente zu breit geschnitten und vermischt UI, State, Persistenz, Export und Server-Requests in [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L140).
- Die Test- und CI-Abdeckung fokussiert auf Render-/CSS-Verhalten; genau die riskanten Pfade `DataGrid`, `OverlayProvider`, echte JS-Interop-Verhalten und Export sind praktisch nicht abgesichert.

## 2. Was technisch gut ist

- Titel: Gute Overlay-Zugänglichkeit und Lifecycle-Disziplin. Warum gut: Dialog- und Sheet-Inhalte setzen beim Öffnen Scroll-Lock und Focus-Trap und räumen sauber wieder auf. Betroffene Dateien/Module: [DialogContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Dialog/DialogContent.razor#L58), [SheetContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Sheet/SheetContent.razor#L57). Auswirkung auf Qualität/Wartbarkeit: Das reduziert Accessibility-Regressionen und zeigt, dass Lifecycle-Probleme in den Overlay-Primitives bewusst behandelt wurden.
- Titel: Service-/Provider-Modell ist als API für Konsumenten gut gewählt. Warum gut: `ToastService` und `OverlayService` bieten eine pragmatische imperative API statt Konsumenten zu zwingen, transienten UI-State überall selbst zu verdrahten. Betroffene Dateien/Module: [ToastService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ToastService.cs#L11), [OverlayService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/OverlayService.cs#L9), [OverlayProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Overlay/OverlayProvider.razor#L17). Auswirkung auf Qualität/Wartbarkeit: Gute DX für Library-Konsumenten; das API-Modell ist leicht verständlich und gut integrierbar.
- Titel: Form-Validierung ist einfach, aber kohärent. Warum gut: `DataAnnotationsFormValidator` und `FormContext` halten Validierungszustand zentral und ohne unnötige Magie. Betroffene Dateien/Module: [DataAnnotationsFormValidator.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Form/DataAnnotationsFormValidator.cs#L7), [FormContext.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Form/FormContext.cs#L5). Auswirkung auf Qualität/Wartbarkeit: Für einfache bis mittlere Form-Szenarien ist das transparent und debuggbar.
- Titel: Scoped Service-Lifetimes sind passend zum Blazor-Kontext. Warum gut: Interop-, Theme-, Toast-, Overlay- und Shortcut-Services werden scoped registriert. Betroffene Dateien/Module: [LumeoServiceExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Extensions/LumeoServiceExtensions.cs#L8). Auswirkung auf Qualität/Wartbarkeit: Das passt zu Circuit-/Request-Lebenszyklen besser als Singleton und vermeidet offensichtliche globale Zustandslecks.

## 3. Konkrete Schwächen

- Titel: `DataGrid` ist ein God-Component. Kategorie: Architektur. Betroffene Dateien/Module: [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L140). Erklärung mit konkretem Codebezug: Eine Datei kapselt Filtering, Sorting, Selection, Editing, Grouping, Export, Layout-Persistenz, Server-Requests, Error-Handling und Timer. Risikoauswirkung: Hohe Änderungsrisiken, schwer isolierbare Bugs, schlechte Testbarkeit. Priorität: `hoch`
- Titel: Interop-Callbacks sind mehrfach global statt instanzspezifisch. Kategorie: Bug-Risiko. Betroffene Dateien/Module: [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L147), [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L180), [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L216), [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L429). Erklärung mit konkretem Codebezug: Methoden wie `OnSwipeDismiss`, `OnSwipe`, `OnScrollPosition`, `OnResize`, `OnResizeEnd`, `OnScrollVisibilityChanged` iterieren stumpf über `Values`. Risikoauswirkung: Mehrere Carousels, Drawer, Resize-Handles oder `BackToTop`-Instanzen beeinflussen sich gegenseitig. Priorität: `hoch`
- Titel: Performance-Hot-Path nutzt Reflection pro Zelle. Kategorie: Performance. Betroffene Dateien/Module: [DataGridColumn.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridColumn.cs#L33). Erklärung mit konkretem Codebezug: `typeof(TItem).GetProperty(Field)` wird bei jedem Zugriff erneut ausgeführt. Risikoauswirkung: Auf großen Tabellen unnötige CPU-Kosten und GC-Druck. Priorität: `hoch`
- Titel: Fehler werden an kritischen Stellen still geschluckt. Kategorie: Wartbarkeit. Betroffene Dateien/Modules: [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L366), [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L1010), [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L48). Erklärung mit konkretem Codebezug: Mehrere `catch { }` bzw. bloßes `Console.Error.WriteLine` ohne strukturiertes Fehlerbild. Risikoauswirkung: Produktionsprobleme werden unsichtbar und sind für Konsumenten schwer diagnostizierbar. Priorität: `mittel`
- Titel: Provider verwenden `async void`. Kategorie: Bug-Risiko. Betroffene Dateien/Module: [OverlayProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Overlay/OverlayProvider.razor#L94), [ToastProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Toast/ToastProvider.razor#L51). Erklärung mit konkretem Codebezug: Exceptions in Event-Pfaden gehen am normalen Task-Fehlerfluss vorbei. Risikoauswirkung: Sporadische Renderer-Fehler werden schwer reproduzierbar. Priorität: `mittel`
- Titel: Chart-Layer zieht Standard-Script von CDN ohne Integrität/Fallback. Kategorie: Security. Betroffene Dateien/Module: [echarts-interop.js](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/wwwroot/js/echarts-interop.js#L6). Erklärung mit konkretem Codebezug: Default ist `https://cdn.jsdelivr.net/npm/echarts@5/dist/echarts.min.js`. Risikoauswirkung: CSP-/Offline-Probleme, Supply-Chain-Abhängigkeit und schwer kontrollierbare Laufzeitfehler. Priorität: `mittel`
- Titel: Nullability wird teils aktiv umgangen. Kategorie: Stilproblem. Betroffene Dateien/Module: [Select.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Select/Select.razor#L113), [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L218), [OverlayService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/OverlayService.cs#L147). Erklärung mit konkretem Codebezug: `null!`, pragma-Unterdrückungen und `default!` kaschieren Unsicherheit statt sie sauber zu modellieren. Risikoauswirkung: Compilerhilfe verliert an Wert; Randfälle werden später entdeckt. Priorität: `mittel`
- Titel: Test-Setup kaschiert echte Interop-Fehler. Kategorie: Wartbarkeit. Betroffene Dateien/Module: [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L15). Erklärung mit konkretem Codebezug: `JSInterop.Mode = Loose` und lose Module sorgen dafür, dass viele kaputte JS-Pfade trotzdem grün bleiben. Risikoauswirkung: Falsches Sicherheitsgefühl bei interaktiven Komponenten. Priorität: `hoch`
- Titel: CI ist zu dünn für 1.0-Ambitionen. Kategorie: Architektur. Betroffene Dateien/Module: [ci.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/ci.yml#L20). Erklärung mit konkretem Codebezug: Nur Library-Build und Tests; keine Docs-App, keine Package-Audits, keine Analyzer, keine formatting/static-analysis-Gates. Risikoauswirkung: Regressionsfläche bleibt hoch. Priorität: `hoch`

## 4. Wahrscheinliche Bugs

- Datei: [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L267). Methode/Komponente: `RegisterToastSwipe` / `OnToastSwipeDismiss`. Warum vermutlich Bug: Es wird mit `_toastSwipeHandlers[elementId]` gespeichert, aber mit `TryGetValue(toastId, ...)` gelesen. Kurz: `_toastSwipeHandlers[elementId]` vs. `TryGetValue(toastId, ...)`. Wie man ihn reproduzieren könnte: Toast mit Swipe-Dismiss rendern, horizontal wischen; Handler feuert nicht. Wie man ihn beheben könnte: Einheitlich `toastId` oder `elementId` als Key verwenden.
- Datei: [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L147). Methode/Komponente: `OnSwipeDismiss`, `OnSwipe`, `OnScrollPosition`, `OnResize`, `OnResizeEnd`, `OnScrollVisibilityChanged`. Warum vermutlich Bug: Mehrere registrierte Instanzen bekommen dasselbe Event. Wie man ihn reproduzieren könnte: Zwei Carousels oder zwei `BackToTop`-Buttons auf derselben Seite rendern; ein Event bewegt/aktualisiert beide. Wie man ihn beheben könnte: JS soll eine Instanz-ID zurückmelden, C# nur den passenden Handler dispatchen.
- Datei: [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L1085). Methode/Komponente: `RequestServerData`. Warum vermutlich Bug: Bei `OperationCanceledException` wird `return` ausgeführt, bevor `_serverLoading = false` gesetzt wird. Wie man ihn reproduzieren könnte: `OnServerRequest` implementieren, Token respektieren und `OperationCanceledException` werfen; Grid bleibt im Loading-State hängen. Wie man ihn beheben könnte: `_serverLoading = false` in `finally` verschieben.
- Datei: [DataGridExportService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridExportService.cs#L30). Methode/Komponente: `ExportToExcelAsync`. Warum vermutlich Bug: Die Methode erzeugt CSV mit BOM, aber liefert `.xlsx` und Excel-MIME-Type. Wie man ihn reproduzieren könnte: Export öffnen in einem Tool, das echte XLSX-Struktur erwartet; Datei ist formal ungültig. Wie man ihn beheben könnte: Entweder echten XLSX-Writer verwenden oder ehrlich als CSV exportieren.
- Datei: [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L83). Methode/Komponente: `OnAfterRenderAsync`. Warum vermutlich Bug: Nach dem Initialisieren werden nur Option-JSON und Loading-State beobachtet, nicht `Theme`, `Group` oder neue Event-Delegates. Wie man ihn reproduzieren könnte: Theme zur Laufzeit ändern; existierende Charts bleiben visuell im alten Theme. Wie man ihn beheben könnte: Re-Init oder gezieltes Update bei Theme-/Group-Änderungen.

## 5. Architekturrisiken

- `DataGrid` funktioniert heute, skaliert aber schlecht als Änderungsobjekt. Warum: Zu viele Verantwortlichkeiten in einer Komponente. Sinnvolles Refactoring: State-Machine/Store, Export-Service, Layout-Persistence-Service und Server-Adapter trennen.
- Der Interop-Service ist als “God bridge” angelegt. Warum: Eine Klasse sammelt viele unrelated Browser-Verhalten. Sinnvolles Refactoring: Feature-spezifische Interop-Adapter pro Domäne (`OverlayInterop`, `ScrollInterop`, `ChartInterop`, `TableInterop`).
- Die Library setzt stark auf implizite Provider-Platzierung im Host. Warum: Für Toast/Overlay ist zusätzliches Root-Markup nötig, `AddLumeo()` allein reicht nicht. Sinnvolles Refactoring: klarere bootstrap story, optionales host component package oder Analyzer/Docs-Warnungen.
- Die Teststrategie ist auf Breite statt Risikodichte optimiert. Warum: Viele Render-Tests, wenige stateful/integration-nahe Tests. Sinnvolles Refactoring: Weniger Snapshot/CSS-Tests, mehr Verhaltens- und Multi-Instance-Tests.

## 6. Test- und Qualitätsbewertung

- Gut an den Tests: Die Suite ist groß, schnell und stabil; `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build` lief mit `965/965` bestanden. Einfache Komponenten sind damit gegen viele triviale Regressionsfälle gut abgesichert.
- Kritisch unzureichend abgesichert: `DataGrid`, `OverlayProvider`, echtes JS-Interop-Verhalten, Exportfunktionen und Multi-Instance-Szenarien. In `tests/Lumeo.Tests` gibt es `DataTableTests`, aber keine echte `DataGrid`-Testsuite; außerdem nutzt das Test-Setup bewusst `JSRuntimeMode.Loose` in [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L15).
- CI/CD: Formal vorhanden, inhaltlich zu dünn. [ci.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/ci.yml#L20) baut nur die Library und führt Tests aus; die Docs-App, Analyzer, Package-Audits und Security-Checks fehlen.
- Fehlende Checks: `dotnet build` mit Warnings-as-Errors, Analyzer-Regeln, `dotnet list package --vulnerable`, Docs-Build, evtl. Playwright/Browser-Smoke-Tests für JS-Interop. Zusätzlich fiel beim ersten Testlauf lokal `NU1903` für `Microsoft.Extensions.Caching.Memory 9.0.0-preview.6.24327.7` an; die Test-Dependencies in [Lumeo.Tests.csproj](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Lumeo.Tests.csproj#L12) sollten bereinigt werden.

## 7. Top-10 Maßnahmen

- Interop-Callback-Routing pro Instanz statt global. Nutzen: beseitigt echte Cross-Component-Bugs. Aufwand: `M`
- `ToastSwipe`-Key-Mismatch beheben. Nutzen: Swipe-Dismiss funktioniert tatsächlich. Aufwand: `S`
- `DataGrid.RequestServerData` auf `try/finally` umstellen. Nutzen: kein hängender Loading-State mehr. Aufwand: `S`
- `ExportToExcelAsync` ehrlich machen oder echten XLSX-Export bauen. Nutzen: verhindert kaputte Exportdateien. Aufwand: `M`
- `DataGrid` in Subsysteme zerlegen. Nutzen: bessere Testbarkeit und geringere Änderungsrisiken. Aufwand: `L`
- Reflection in `DataGridColumn.GetValue` cachen oder kompilierte Accessors verwenden. Nutzen: bessere Tabellen-Performance. Aufwand: `M`
- `async void` aus `ToastProvider` und `OverlayProvider` entfernen. Nutzen: sauberer Fehlerfluss. Aufwand: `M`
- Risikobasierte Tests für `DataGrid`, Overlay-Flow und Multi-Instance-Interop ergänzen. Nutzen: schließt die wichtigsten blinden Flecken. Aufwand: `M`
- CI um Docs-Build, Vulnerability-Check und Analyzer erweitern. Nutzen: höhere Produktionsreife. Aufwand: `S`
- Externes Chart-CDN optional machen oder lokal bundlen. Nutzen: bessere Robustheit, CSP- und Offline-Kompatibilität. Aufwand: `M`

## 8. Scorecard

- Architektur: `6/10`
- Codequalität: `7/10`
- Konsistenz: `7/10`
- Wartbarkeit: `5/10`
- Testqualität: `6/10`
- Produktionsreife: `6/10`
- Entwicklererlebnis: `8/10`
- Zukunftsfähigkeit: `5/10`

## 9. Finale Einschätzung

- Würde ich dieses Repo heute in Produktion einsetzen? Ja, aber nur selektiv und nicht blind für alle Features.
- Unter welchen Bedingungen ja/nein? Ja für viele der kleineren UI-Primitives und mit kontrolliertem Einsatz der stabileren Komponenten. Nein für eine 1.0-Library-Freigabe, solange `DataGrid`, Interop-Routing, Export und die Provider-/JS-Pfade nicht härter abgesichert sind.
- Was müsste vor einer 1.0 unbedingt noch passieren? Die vier Pflichtpunkte sind: Interop-Bugs beseitigen, `DataGrid` stabilisieren, Export korrekt machen und die Test-/CI-Schicht auf die tatsächlich riskanten Pfade ausrichten. Danach wäre das Repo aus meiner Sicht deutlich näher an “solide Library” als nur “produktionsnah mit Fallstricken”.

## Review 2

**Prüfungen**

`dotnet build src/Lumeo/Lumeo.csproj -c Release` war erfolgreich.  
`dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build` war erfolgreich mit `965/965` Tests.  
Zusätzlich habe ich gezielt per Code-Suche nach Defektmustern und fehlenden Tests für `DataGrid`, `OverlayProvider`, `OnToastSwipeDismiss`, `ExportToExcelAsync`, `OperationCanceledException` und `DataGridServerRequest` gesucht.

**Priorisierte Defect-Liste**

1. **Toast-Swipe-Dismiss ist verdrahtet, kann aber nicht funktionieren**
- Datei: [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L267), [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L281)
- Ursache: Der Handler wird mit `elementId` gespeichert, aber mit `toastId` gelesen.
- Risiko: Swipe-Dismiss feuert nie oder nur zufällig bei identischem Key. Das ist ein echter funktionaler Defekt.
- Fix-Idee: Einheitlich denselben Schlüssel verwenden, am besten `toastId` end-to-end, oder `OnToastSwipeDismiss(string toastId, string elementId)` eindeutig machen.
- Absicherung: Für `OnToastSwipeDismiss` und `RegisterToastSwipe` existiert in `tests/Lumeo.Tests` kein Test.

2. **Interop-Events haben instanzübergreifendes Cross-Talk**
- Datei: [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L147), [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L180), [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L216), [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L316), [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L429)
- Ursache: `OnSwipeDismiss`, `OnSwipe`, `OnScrollPosition`, `OnResize`, `OnResizeEnd`, `OnOtpPaste`, `OnScrollVisibilityChanged` iterieren über alle registrierten Handler statt den auslösenden.
- Risiko: Zwei Carousels, Resizer, OTP-Inputs oder `BackToTop`-Instanzen beeinflussen sich gegenseitig. Das ist ein echter Multi-Instance-Bug.
- Fix-Idee: JS muss die auslösende Instanz-ID zurückmelden; C# darf nur den passenden Handler dispatchen.
- Absicherung: Die vorhandenen Tests zementieren das falsche Verhalten sogar: [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L190), [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L232), [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L248), [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L296)

3. **`DataGrid` kann nach gecanceltem Server-Request im Loading-State hängen bleiben**
- Datei: [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L1085)
- Ursache: Bei `OperationCanceledException` wird vor `_serverLoading = false` mit `return` abgebrochen.
- Risiko: Debounced Search oder schnelle Seitenwechsel können ein Grid dauerhaft als ladend markieren. Das ist eine Race-Condition mit sichtbarer Fehlfunktion.
- Fix-Idee: `_serverLoading = false` und `StateHasChanged()` in ein `finally` verschieben.
- Absicherung: Für `OperationCanceledException`/`DataGridServerRequest` gibt es in `tests/Lumeo.Tests` keinen Test.

4. **Overlay-API kann `Close(null)` nicht von `Cancel()` unterscheiden**
- Datei: [OverlayReference.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/OverlayReference.cs#L19), [OverlayService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/OverlayService.cs#L54), [OverlayProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Overlay/OverlayProvider.razor#L100)
- Ursache: `Close(object? result = null)` erlaubt `null`, aber `OverlayProvider` behandelt jedes `null` als `CancelResult`.
- Risiko: Konsumenten können legitime “kein Payload”-Ergebnisse nicht zurückgeben; API-Vertrag ist semantisch inkonsistent.
- Fix-Idee: `OnClose` um einen expliziten `bool cancelled` erweitern oder getrennte Events/Methoden für Close und Cancel verwenden.
- Absicherung: Für `OverlayProvider` gibt es in `tests/Lumeo.Tests` keinen Test.

5. **`positionFixed` registriert globale Window-Listener ohne explizites Cleanup aus C#**
- Datei: [components.js](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/wwwroot/js/components.js#L97), [SelectContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Select/SelectContent.razor#L54), [ComboboxContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Combobox/ComboboxContent.razor#L46)
- Ursache: `positionFixed` hängt `scroll`/`resize` an `window`; die aufrufenden Komponenten räumen nur `ClickOutside` auf, aber nicht die Positioning-Listener.
- Risiko: Listener bleiben bis zum nächsten Scroll/Resize oder länger aktiv. Bei häufiger Overlay-Nutzung entsteht unnötiger globaler Event-Druck und potenziell Leak-artiges Verhalten.
- Fix-Idee: `PositionFixed` sollte ein Cleanup-Handle oder eine `UnpositionFixed(contentId)`-API haben; Komponenten müssen das in `DisposeAsync` aufrufen.
- Absicherung: Vorhandene Tests prüfen nur “does not throw” für `PositionFixed`, nicht Cleanup: [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L158)

6. **`ToastProvider` nutzt fehleranfällige Async-Konstruktionen mit unobservierten Continuations**
- Datei: [ToastProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Toast/ToastProvider.razor#L51)
- Ursache: `async void`-Event-Handler plus `Task.Delay(...).ContinueWith(async t => ...)` erzeugen verworfene `Task<Task>`-Continuations.
- Risiko: Exceptions in Timerpfaden sind unobserved; Continuations können noch nach Dispose in den Renderer laufen. Das ist eine echte Async-/Cleanup-Schwachstelle.
- Fix-Idee: Keine `async void`; stattdessen `Task`-basierte Methoden und eine saubere `await`-Kette mit internem `DismissAfterDelayAsync`.
- Absicherung: Die Tests decken nur einfache Toast-Anzeige ab, nicht Timer-/Dispose-Rennen.

7. **Shortcut-Unregister ist Fire-and-Forget und race-anfällig**
- Datei: [KeyboardShortcutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/KeyboardShortcutService.cs#L121)
- Ursache: `ShortcutHandle.Dispose()` ruft `_ = service.UnregisterAsync(id);` ohne Await.
- Risiko: Direkt nach `Dispose()` kann der Shortcut im JS noch kurz aktiv sein; bei schnellem Mount/Unmount entstehen Ghost-Shortcuts.
- Fix-Idee: `ShortcutHandle` als `IAsyncDisposable` anbieten oder synchron lokal entfernen und JS-Removal separat robust abwickeln.
- Absicherung: Kein Test deckt dieses Unregister-Race ab.

8. **`Select` verletzt seinen eigenen Nullability-Vertrag**
- Datei: [Select.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Select/Select.razor#L25), [Select.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Select/Select.razor#L113)
- Ursache: `ValueChanged` ist `EventCallback<string>`, aber `OnClear()` sendet `null!`.
- Risiko: Konsumenten mit nicht-nullbaren Bindings oder Callbacks können unerwartet abstürzen oder inkonsistenten State bekommen.
- Fix-Idee: API auf `EventCallback<string?>` umstellen oder Clear bei Single-Select anders modellieren.
- Absicherung: Kein Test prüft den Clear-Pfad gegen nicht-nullbare Consumer.

9. **`ExportToExcelAsync` erzeugt keine echte XLSX-Datei**
- Datei: [DataGridExportService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridExportService.cs#L30)
- Ursache: Es wird CSV mit BOM erzeugt, aber als `.xlsx` und mit XLSX-MIME-Type ausgeliefert.
- Risiko: Tools, Validatoren oder strengere Clients behandeln die Datei als defekt. Das ist funktional falsch.
- Fix-Idee: Entweder echten XLSX-Writer einsetzen oder ehrlich als `.csv` exportieren.
- Absicherung: Für `ExportToExcelAsync` gibt es keinen Test.

10. **`DataGridColumn.GetValue` ist eine echte Performance-Falle im Hot Path**
- Datei: [DataGridColumn.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridColumn.cs#L33)
- Ursache: Reflection via `GetProperty` erfolgt bei jedem Zellzugriff neu.
- Risiko: Sortieren, Filtern, Gruppieren und Rendern großer Tabellen werden unnötig teuer; genau in der komplexesten Komponente fehlt damit ein zentraler Performance-Schutz.
- Fix-Idee: `PropertyInfo` oder kompilierte Getter pro Spalte cachen.
- Absicherung: Keine Performance- oder Lasttests für `DataGrid` vorhanden.

11. **Kritische Defektpfade sind ungetestet**
- Datei: [tests/Lumeo.Tests](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests)
- Ursache: Für `DataGrid`, `OverlayProvider`, `OnToastSwipeDismiss`, `ExportToExcelAsync`, `OperationCanceledException` in `RequestServerData` und echte Multi-Instance-Interop-Szenarien gibt es keine Tests; die Suche in `tests/Lumeo.Tests` lieferte dafür keine Treffer.
- Risiko: Mehrere der obigen Defekte konnten deshalb unbemerkt bleiben; ein Teil ist sogar durch falsche Service-Tests als “erwartetes Verhalten” festgeschrieben.
- Fix-Idee: Gezielt Defekt-Tests ergänzen, insbesondere Multi-Instance-Carousels/Resizers, canceled server requests, toast swipe, overlay result semantics und export validation.
