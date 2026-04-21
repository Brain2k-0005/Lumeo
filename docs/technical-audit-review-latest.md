# Technisches Audit – Aktueller Stand

Stand: 2026-04-01

Prüfungen:
- `dotnet build src/Lumeo/Lumeo.csproj -c Release` erfolgreich
- `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build --verbosity minimal` erfolgreich mit `1126/1126` Tests
- Repo-weite Code-Suche über `src`, `tests`, `.github` und `docs` nach Async-, Cleanup-, Interop-, Nullability- und CI-Mustern durchgeführt

## 1. Executive Summary

- Das Repository ist heute deutlich reifer als beim ersten Audit. Die früheren schweren Interop-Defekte sind weitgehend beseitigt, Cleanup-Pfade wurden sichtbar nachgezogen und die CI ist inzwischen näher an einer echten Qualitätsbarriere.
- Die Repo-Struktur ist sauber: `src/Lumeo` enthält die Library, `docs/Lumeo.Docs` die Showcase-/Doku-App und `tests/Lumeo.Tests` die Test-Suite. Diese Trennung ist klar und zweckmäßig.
- Die zentrale Library ist breit aufgestellt, mit vielen UI-Modulen und einem kleinen Service-Kern aus Interop, Theme, Toast, Overlay und Keyboard-Shortcuts in [LumeoServiceExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Extensions/LumeoServiceExtensions.cs#L8).
- Positiv ist, dass die Pipeline jetzt nicht mehr nur die Library baut, sondern auch die Docs-App und zusätzlich auf verwundbare Pakete prüft in [ci.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/ci.yml#L10).
- Auch die Testbasis ist groß und schnell. `1126` grüne Tests sind für eine Component-Library dieser Größe ein echter Pluspunkt.
- Die verbleibenden Probleme liegen nicht mehr in offensichtlichen Kernbrüchen, sondern in präzisen Rand- und Robustheitsdefekten: `async void` in Providern, teilweise zu loses JS-Testsetup, einige stille `catch`-Blöcke und weiterhin eine sehr breite `DataGrid`-Komponente.
- Mein Gesamturteil ist daher klar besser als zuvor: nicht mehr nur “produktionsnah mit Fallstricken”, sondern inzwischen eine solide Library mit noch einigen technischen Restbaustellen.
- Einschätzung: `solide Library`
- Größte 3 Stärken:
- Instanzspezifisches Interop-Routing und nachgezogener Cleanup in [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L153) und vielen Overlay-Komponenten
- Solide CI-Barriere mit `-warnaserror`, Docs-Build und Vulnerability-Check in [ci.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/ci.yml#L23)
- Breite Testbasis mit `1126` erfolgreichen Tests
- Größte 3 Risiken:
- `ToastProvider` und `OverlayProvider` verwenden weiter `async void`
- Kritische JS-/Interop-Pfade werden im Testsetup noch zu großzügig durch `JSRuntimeMode.Loose` maskiert
- `DataGrid` bleibt architektonisch zu breit und damit regressionsanfällig

## 2. Was technisch gut ist

- Titel
- Interop-Cross-Talk wurde an den kritischen Stellen sauber behoben
- Warum gut
- Drawer-, Carousel-, Resize-, OTP- und BackToTop-Callbacks dispatchen jetzt instanzbezogen statt global. Das beseitigt echte Mehrinstanz-Bugs.
- Betroffene Dateien/Module
- [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L153)
- [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L190)
- Auswirkung auf Qualität/Wartbarkeit
- Vorhersagbareres Verhalten für Konsumenten und deutlich geringeres Risiko von Cross-Talk zwischen Komponenteninstanzen.

- Titel
- `positionFixed` hat jetzt einen echten Cleanup-Pfad und wird in vielen Aufrufern korrekt verwendet
- Warum gut
- `UnpositionFixed` existiert nicht mehr nur als theoretische API, sondern wird in mehreren Overlays im Cleanup aufgerufen.
- Betroffene Dateien/Module
- [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L110)
- [PopoverContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Popover/PopoverContent.razor#L81)
- [DropdownMenuContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DropdownMenu/DropdownMenuContent.razor#L119)
- [MenubarContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Menubar/MenubarContent.razor#L53)
- [NavigationMenuContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/NavigationMenu/NavigationMenuContent.razor#L57)
- [HoverCardContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/HoverCard/HoverCardContent.razor#L50)
- Auswirkung auf Qualität/Wartbarkeit
- Weniger globale Listener-Leaks und konsistenteres Verhalten bei wiederholt geöffneten Floating-UI-Komponenten.

- Titel
- `DataGrid`-Server-Loading-State ist robuster geworden
- Warum gut
- Der Cancellation-Pfad über `OperationCanceledException` setzt `_serverLoading` jetzt über `finally` zuverlässig zurück.
- Betroffene Dateien/Module
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L1085)
- Auswirkung auf Qualität/Wartbarkeit
- Weniger Race-Conditions bei schneller Suche, Paging oder Request-Wechseln.

- Titel
- Overlay-Semantik für `Close` und `Cancel` ist heute sauberer modelliert
- Warum gut
- `OverlayService` transportiert explizit, ob ein Overlay gecancelt oder mit Ergebnis geschlossen wurde.
- Betroffene Dateien/Module
- [OverlayService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/OverlayService.cs#L7)
- [OverlayProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Overlay/OverlayProvider.razor#L107)
- Auswirkung auf Qualität/Wartbarkeit
- Weniger Mehrdeutigkeit im API-Vertrag für Konsumenten.

- Titel
- CI ist inzwischen substanziell besser
- Warum gut
- Die Pipeline baut Library und Docs-App, testet und führt einen Vulnerability-Check aus.
- Betroffene Dateien/Module
- [ci.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/ci.yml#L20)
- Auswirkung auf Qualität/Wartbarkeit
- Höhere Wahrscheinlichkeit, Regressions- und Paketprobleme vor Merge zu entdecken.

## 3. Konkrete Schwächen

- Titel
- Provider verwenden weiter `async void`
- Kategorie: Bug-Risiko
- Betroffene Dateien/Module
- [ToastProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Toast/ToastProvider.razor#L51)
- [OverlayProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Overlay/OverlayProvider.razor#L94)
- Erklärung mit konkretem Codebezug
- `HandleShow`, `HandleDismiss`, `HandleUpdate` sowie `HandleShow` und `HandleClose` im Overlay-Provider sind weiter `async void`. Die Fehler werden zwar geloggt, aber der Pfad bleibt schwer kontrollierbar und nicht sauber awaitbar.
- Risikoauswirkung
- Sporadische Lifecycle-/Renderer-Rennen und schwer testbare Fehlerpfade.
- Priorität: mittel

- Titel
- Test-Setup maskiert JS-/Interop-Defekte weiter durch `Loose`-Mode
- Kategorie: Wartbarkeit
- Betroffene Dateien/Module
- [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L13)
- Erklärung mit konkretem Codebezug
- `ctx.JSInterop.Mode = JSRuntimeMode.Loose` und ein loses Modul sorgen dafür, dass viele kaputte oder unvollständige Interop-Aufrufe trotzdem grün bleiben.
- Risikoauswirkung
- Die Tests geben mehr Sicherheit vor als sie im JS-lastigen Bereich tatsächlich liefern.
- Priorität: mittel

- Titel
- `DataGrid` bleibt architektonisch zu breit
- Kategorie: Architektur
- Betroffene Dateien/Module
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L1)
- Erklärung mit konkretem Codebezug
- Die Komponente bündelt Renderlogik, Sortierung, Filter, Persistenz, Export, Server-Loading, Timer und Fehlerbehandlung in einer großen Datei.
- Risikoauswirkung
- Änderungen bleiben teuer, schwer isolierbar und regressionsanfällig.
- Priorität: hoch

- Titel
- Mehrere produktionsrelevante Fehler werden weiterhin stumm geschluckt
- Kategorie: Wartbarkeit
- Betroffene Dateien/Module
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L382)
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L990)
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L1018)
- [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L58)
- [echarts-interop.js](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/wwwroot/js/echarts-interop.js#L148)
- Erklärung mit konkretem Codebezug
- Mehrere `catch { }`-Blöcke oder reine `Console.Error.WriteLine(...)`-Pfadbehandlungen verbergen Fehlerbilder statt sie strukturiert zu modellieren.
- Risikoauswirkung
- Produktionsprobleme bleiben schlecht diagnostizierbar.
- Priorität: mittel

- Titel
- Einige kritische Fixes sind noch immer nicht gezielt regressionsgesichert
- Kategorie: Testqualität
- Betroffene Dateien/Module
- [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L435)
- Erklärung mit konkretem Codebezug
- Es gibt jetzt Tests für instanzbezogenes Routing und Toast-Dismiss per `toastId`, aber weiterhin keine klaren Regressionstests für `UnregisterToastSwipe`, Overlay `Close` vs. `Cancel`, `UnpositionFixed`-Cleanup und den `DataGrid`-Cancellation-Pfad.
- Risikoauswirkung
- Randpfad-Regressionen können wieder unbemerkt einziehen.
- Priorität: mittel

## 4. Wahrscheinliche Bugs

- Datei
- [ToastProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Toast/ToastProvider.razor#L51)
- Methode/Komponente
- `HandleShow`, `HandleDismiss`, `HandleUpdate`
- Warum vermutlich Bug
- Kein klarer funktionaler Bug im Normalpfad sichtbar, aber `async void` bleibt ein echter Risikopfad bei Exceptions oder Dispose-Rennen.
- Wie man ihn reproduzieren könnte
- Provider rendern, Timer anstoßen, dann Komponente frühzeitig disposen und parallel weitere Toast-Events senden.
- Wie man ihn beheben könnte
- Event-Handler synchron anmelden und intern in `InvokeAsync(async () => ...)` auf `Task`-basierte Methoden delegieren.

- Datei
- [OverlayProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Overlay/OverlayProvider.razor#L94)
- Methode/Komponente
- `HandleShow`, `HandleClose`
- Warum vermutlich Bug
- Gleiches Muster wie beim Toast-Provider; funktional nicht nachweisbar falsch, aber weiterhin fragil im Fehler- und Lifecycle-Verhalten.
- Wie man ihn reproduzieren könnte
- Overlay öffnen/schließen, während der Provider disposed oder der Renderzyklus unterbrochen wird.
- Wie man ihn beheben könnte
- `async void` eliminieren und Eventbehandlung in sauber awaitbare Methoden verlagern.

## 5. Architekturrisiken

- `DataGrid` funktioniert heute, bleibt aber als Änderungsobjekt zu komplex
- Warum
- Hohe Verantwortungsdichte in einer Komponente verhindert saubere Isolation von Fehlern und Tests.
- Welche Refactorings sinnvoll wären
- Export, Persistenz, Server-Orchestrierung und internen State in getrennte Subsysteme zerlegen.

- `ComponentInteropService` ist funktional besser, aber strukturell weiterhin ein God-Service
- Warum
- Viele unabhängige Browser-Fähigkeiten hängen an einer Klasse.
- Welche Refactorings sinnvoll wären
- Zerlegen in feature-spezifische Interop-Adapter wie `OverlayInterop`, `InputInterop`, `ScrollInterop`, `GridInterop`.

- Tests priorisieren Breite stärker als Risikodichte
- Warum
- Viele einfache Render-/Smoke-Tests, zu wenige Defekt- und Cleanup-Regressionstests.
- Welche Refactorings sinnvoll wären
- Einige “does not throw”-Tests durch verhaltensorientierte Assertions auf Cleanup, Dispatch und Zustandstransitionen ersetzen.

## 6. Test- und Qualitätsbewertung

- Was an den Tests gut ist
- Sehr breite Abdeckung über viele Komponenten-Ordner in `tests/Lumeo.Tests`
- Relevante Interop-Fixes sind inzwischen mit gezielten Service-Tests abgesichert, z. B. instanzspezifisches Dispatching in [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L190)
- Die Suite ist schnell genug, um real als Guardrail zu taugen

- Welche kritischen Bereiche unzureichend abgesichert wirken
- `UnregisterToastSwipe`
- `UnpositionFixed`-Cleanup als explizite Regression
- Overlay `Close` vs. `Cancel`
- `DataGrid`-Cancellation-/Serverpfade
- Harte JS-Interop-Verifikation statt `Loose`-Mode

- Ob CI/CD ausreichend ist
- Deutlich besser als früher und für ein solides OSS-Projekt brauchbar, aber noch nicht maximal scharf.

- Welche Checks fehlen
- Striktere JS-Interop-Assertions in Tests
- Gezielte Defekt-Regressionstests statt weiterer Smoke-Tests
- Optional Browser-Smoke- oder Playwright-Checks für interaktive High-Risk-Komponenten

## 7. Top-10 Maßnahmen

- Problem
- `async void` in `ToastProvider` und `OverlayProvider`
- Nutzen
- Stabilerer Async-/Lifecycle-Pfad
- Aufwand: M

- Problem
- Fehlende Regressionstests für `UnregisterToastSwipe`
- Nutzen
- Absicherung eines frisch gefixten Defektpfads
- Aufwand: S

- Problem
- Fehlende Regressionstests für `UnpositionFixed`-Cleanup
- Nutzen
- Verhindert Rückfall in globale Listener-Leaks
- Aufwand: M

- Problem
- Fehlende Overlay-Tests für `Close` vs. `Cancel`
- Nutzen
- Absicherung der korrigierten API-Semantik
- Aufwand: S

- Problem
- Fehlende `DataGrid`-Tests für Cancellation/Server-Requests
- Nutzen
- Absicherung eines riskanten Async-Pfads
- Aufwand: M

- Problem
- `JSRuntimeMode.Loose` als Default im Testsetup
- Nutzen
- Schärfere Interop-Fehlererkennung
- Aufwand: M

- Problem
- `DataGrid` ist zu breit
- Nutzen
- Bessere Änderbarkeit und geringere Regressionskosten
- Aufwand: L

- Problem
- Breite stille `catch`-Blöcke in Kernpfaden
- Nutzen
- Bessere Diagnosefähigkeit in Produktion
- Aufwand: M

- Problem
- `ComponentInteropService` bleibt zu groß
- Nutzen
- Weniger Seiteneffekte und bessere Verantwortlichkeit
- Aufwand: L

- Problem
- Fehlende strukturierte Fehlerstrategie für JS-/Storage-/Chart-Pfade
- Nutzen
- Konsistentere Robustheit und Supportbarkeit
- Aufwand: M

## 8. Scorecard

- Architektur: `7/10`
- Codequalität: `8/10`
- Konsistenz: `8/10`
- Wartbarkeit: `7/10`
- Testqualität: `7/10`
- Produktionsreife: `8/10`
- Entwicklererlebnis: `8/10`
- Zukunftsfähigkeit: `7/10`

## 9. Finale Einschätzung

- Würdest du dieses Repo heute in Produktion einsetzen?
- Ja, deutlich eher als beim ersten Audit.

- Unter welchen Bedingungen ja/nein?
- Ja, für eine produktive Component-Library mit kontrolliertem Einsatz und normaler technischer Disziplin.
- Nein, wenn die Erwartung ist, dass auch die letzten Randpfade und Async-/Interop-Kanten bereits maximal abgesichert sind.

- Was müsste vor einer 1.0 unbedingt noch passieren?
- `async void` aus den Providern entfernen
- die offenen Regressionstests ergänzen
- `DataGrid`- und Interop-Randpfade gezielt härten

