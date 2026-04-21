# Technisches Audit – Aktualisierung

Stand: 2026-04-02

Prüfungen:
- `dotnet build src/Lumeo/Lumeo.csproj -c Release` erfolgreich
- `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build --verbosity minimal` erfolgreich mit `1140/1140` Tests
- Repo-weite Suche über `src`, `tests` und `.github` nach Async-, Cleanup-, Interop-, Nullability- und CI-Mustern

## 1. Executive Summary

- Das Repo ist erneut besser geworden. Mehrere der zuvor offenen Defektpfade sind inzwischen nicht nur gefixt, sondern auch testseitig abgesichert.
- Besonders positiv ist, dass `ToastSwipe`-Unregister, `UnpositionFixed`-Cleanup, `ColorPicker`-Lightness und der `Cascader`-Nullability-Vertrag mittlerweile sauberer gelöst sind.
- Auch die Provider haben technisch nachgezogen: Das frühere `async void` ist in `ToastProvider` und `OverlayProvider` durch synchrone Event-Handler plus `InvokeAsync(...)`-Delegation ersetzt worden.
- Die CI ist weiterhin solide und baut Library und Docs-App, testet und prüft auf verwundbare Pakete.
- Die Test-Suite ist auf `1140` grüne Tests angewachsen und deckt jetzt mehr Interop-Randpfade ab als zuvor.
- Die verbleibenden Schwächen liegen heute weniger in offensichtlichen Defekten als in Robustheit und Testschärfe: `JSRuntimeMode.Loose` maskiert weiterhin JS-Fehler, und einige `catch { }`- bzw. reine Logging-Pfade bleiben diagnostisch schwach.
- Der größte strukturelle Risikoblock bleibt das `DataGrid`, nicht weil es akut kaputt aussieht, sondern weil es weiterhin zu viele Verantwortlichkeiten in einer Komponente bündelt.
- Mein Urteil ist jetzt klar positiver als in den ersten Reviews: Das ist eine solide, produktionsnahe Library mit wenigen offenen technischen Schulden, nicht mehr ein Repo mit mehreren klaren Kernbugs.
- Einschätzung: `solide Library`
- Größte 3 Stärken
- Früher kritische Defektpfade wurden korrigiert und teils mit Regressionstests versehen
- CI und Build-Barriere sind real brauchbar
- Die Komponentenlandschaft ist breit, konsistent und inzwischen deutlich robuster im Cleanup-/Interop-Verhalten
- Größte 3 Risiken
- Das Testsetup ist im JS-/Interop-Bereich noch zu nachgiebig
- `DataGrid` bleibt architektonisch zu breit
- Einige Catch-/Logging-Pfade sind weiterhin zu still oder zu unstrukturiert

## 2. Was technisch gut ist

- Titel
- Provider-Async ist sauberer als zuvor
- Warum gut
- `ToastProvider` und `OverlayProvider` hängen ihre Events jetzt synchron an und delegieren intern per `InvokeAsync(...)` auf `Task`-Methoden, statt `async void` zu verwenden.
- Betroffene Dateien/Module
- [ToastProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Toast/ToastProvider.razor#L44)
- [OverlayProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Overlay/OverlayProvider.razor#L88)
- Auswirkung auf Qualität/Wartbarkeit
- Weniger fragile Async-/Lifecycle-Pfade und besser kontrollierbares Rendering.

- Titel
- Interop-Fixes sind jetzt auch testseitig besser abgesichert
- Warum gut
- Für `UnregisterToastSwipe` und `UnpositionFixed` existieren jetzt explizite Service-Tests statt nur impliziter Nutzung.
- Betroffene Dateien/Module
- [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L463)
- [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L487)
- Auswirkung auf Qualität/Wartbarkeit
- Frisch behobene Fehlerpfade sind regressionsfester.

- Titel
- `ToastSwipe`-Unregister ist jetzt konsistent
- Warum gut
- Der Handler wird unter `toastId` gespeichert und auch unter `toastId` entfernt; der JS-Call bekommt separat das `elementId`.
- Betroffene Dateien/Module
- [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L273)
- Auswirkung auf Qualität/Wartbarkeit
- Kein offensichtlicher Handler-Leak mehr in diesem Pfad.

- Titel
- `positionFixed`-Cleanup ist inzwischen breit nachgezogen
- Warum gut
- Mehrere Floating-Komponenten rufen jetzt `UnpositionFixed(...)` in Cleanup/Dispose auf.
- Betroffene Dateien/Module
- [PopoverContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Popover/PopoverContent.razor#L81)
- [DropdownMenuContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DropdownMenu/DropdownMenuContent.razor#L119)
- [NavigationMenuContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/NavigationMenu/NavigationMenuContent.razor#L57)
- [MenubarContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Menubar/MenubarContent.razor#L53)
- [HoverCardContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/HoverCard/HoverCardContent.razor#L50)
- Auswirkung auf Qualität/Wartbarkeit
- Deutlich geringeres Risiko globaler Listener-Leaks und Layout-Artefakte bei wiederholtem Öffnen/Schließen.

- Titel
- CI ist inzwischen angemessen streng
- Warum gut
- Build läuft mit `-warnaserror`, die Docs-App wird mitgebaut und es gibt einen Vulnerability-Check.
- Betroffene Dateien/Module
- [ci.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/ci.yml#L23)
- Auswirkung auf Qualität/Wartbarkeit
- Das Repo hat eine echte Qualitätsbarriere vor Merge/Release.

## 3. Konkrete Schwächen

- Titel
- JS-Interop-Tests sind weiter zu locker konfiguriert
- Kategorie: Wartbarkeit
- Betroffene Dateien/Module
- [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L13)
- [ChartTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Components/Chart/ChartTests.cs#L17)
- Erklärung mit konkretem Codebezug
- `JSRuntimeMode.Loose` bleibt der Default im zentralen Testsetup; auch Chart-Tests nutzen ein loses Modul. Dadurch fallen fehlende oder falsche JS-Aufrufe nicht zuverlässig auf.
- Risikoauswirkung
- Die Suite kann im Interop-Bereich noch immer grün sein, obwohl konkrete Browser-Aufrufe kaputt sind.
- Priorität: hoch

- Titel
- `DataGrid` bleibt ein architektonisch überbreiter Knoten
- Kategorie: Architektur
- Betroffene Dateien/Module
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L1)
- [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L1)
- Erklärung mit konkretem Codebezug
- Renderlogik, Persistenz, Export, Server-Orchestrierung, Filter, Sortierung und Teile der UI-Steuerung bleiben stark gekoppelt.
- Risikoauswirkung
- Hohe Änderungs- und Regressionkosten; Defekte lassen sich schwerer isolieren als in kleineren Komponenten.
- Priorität: hoch

- Titel
- Es gibt weiterhin stille Catch-Blöcke in produktionsrelevanten Pfaden
- Kategorie: Wartbarkeit
- Betroffene Dateien/Module
- [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L345)
- [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L383)
- [DataGridFilterOperator.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridFilterOperator.cs#L49)
- [TagInput.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/TagInput/TagInput.razor#L172)
- Erklärung mit konkretem Codebezug
- Mehrere Fehlerpfade enden weiterhin in leerem `catch { }`, ohne Logging oder klare Recovery-Semantik.
- Risikoauswirkung
- Diagnose bleibt in Randfällen unnötig schwer.
- Priorität: mittel

- Titel
- Fehlerbehandlung ist konsistenter, aber weiterhin nur textbasiert
- Kategorie: Wartbarkeit
- Betroffene Dateien/Module
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L382)
- [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L58)
- Erklärung mit konkretem Codebezug
- Viele frühere `catch { }`-Stellen wurden auf `Console.Error.WriteLine(...)` verbessert. Das ist besser als Schweigen, aber weiterhin keine strukturierte Fehlerstrategie.
- Risikoauswirkung
- Produktion ist supportbarer als vorher, aber noch nicht wirklich observability-freundlich.
- Priorität: mittel

## 4. Wahrscheinliche Bugs

- Zum aktuellen Stand habe ich in den zuvor kritischen Pfaden keinen klar belegbaren funktionalen Bug mehr gefunden.
- Die früher belastbaren Defekte in `ToastSwipe`, `positionFixed`, `ColorPicker`, `Cascader` und Provider-Async sind nach aktuellem Codezustand behoben.
- Offene Risiken sind derzeit eher Robustheits- und Testschärfe-Themen als akute Funktionsfehler.

## 5. Architekturrisiken

- `DataGrid` funktioniert, bleibt aber ein zu großer Verantwortungsblock
- Warum
- Zu viele Zustände und Features sind in einer zusammenhängenden Oberfläche verdichtet.
- Welche Refactorings sinnvoll wären
- Persistenz, Export und Server-State in separate interne Services oder Controller ziehen.

- Interop ist funktional robuster, aber strukturell weiter zentralisiert
- Warum
- Viele Browser-Features hängen an [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L5).
- Welche Refactorings sinnvoll wären
- Langfristig in fachliche Interop-Adapter aufteilen.

- Tests sind breit, aber nicht überall so scharf wie sie wirken
- Warum
- `Loose`-Mode reduziert den Aussagewert insbesondere für JS-lastige Komponenten.
- Welche Refactorings sinnvoll wären
- Kritische Interop-Komponenten mit strengeren Modulen und expliziten JS-Assertions testen.

## 6. Test- und Qualitätsbewertung

- Was an den Tests gut ist
- `1140` grüne Tests sind für eine Component-Library dieser Größe sehr ordentlich.
- Die Suite enthält inzwischen mehr gezielte Interop-Regressionen, z. B. für `UnregisterToastSwipe` und `UnpositionFixed` in [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L463).

- Welche kritischen Bereiche unzureichend abgesichert wirken
- Strikte JS-Interop-Verifikation
- `DataGrid`-Verhalten unter komplexeren Zustands- und Server-Szenarien
- End-to-end-nahe Browserpfade für Chart/Overlay/Floating-UI

- Ob CI/CD ausreichend ist
- Für eine solide Open-Source-Library ja. Die CI ist inzwischen deutlich besser als am Anfang des Audits.

- Welche Checks fehlen
- Schärfere Interop-Tests statt `Loose`-Mode als Default
- Optional Browser-Smoke-Tests für High-Risk-Komponenten

## 7. Top-10 Maßnahmen

- Problem
- `JSRuntimeMode.Loose` im zentralen Testsetup
- Nutzen
- Realistischere Aussagekraft der Interop-Tests
- Aufwand: M

- Problem
- `DataGrid` bleibt zu breit
- Nutzen
- Bessere Wartbarkeit und geringere Regressionskosten
- Aufwand: L

- Problem
- Leere `catch`-Blöcke in Toolbar-/Hilfspfaden
- Nutzen
- Bessere Diagnose in Randfällen
- Aufwand: S

- Problem
- Nur textbasiertes Fehler-Logging
- Nutzen
- Bessere Observability und Supportbarkeit
- Aufwand: M

- Problem
- Wenige härtere Tests für Chart-/Browserpfade
- Nutzen
- Höhere Sicherheit bei JS-lastigen Features
- Aufwand: M

- Problem
- `DataGrid`-Serverpfade noch relativ schwach getestet
- Nutzen
- Höhere Robustheit bei realer Nutzung
- Aufwand: M

- Problem
- Interop-Service bleibt ein großer Sammelpunkt
- Nutzen
- Klarere Verantwortlichkeiten und geringere Seiteneffekte
- Aufwand: L

- Problem
- Mehrere Randpfade verlassen sich auf stilles Recovery
- Nutzen
- Bessere Fehlersichtbarkeit
- Aufwand: S

- Problem
- Keine strikteren Assertions auf JS-Methodennamen/-argumente
- Nutzen
- Verhindert grüne, aber inhaltlich unzureichende Tests
- Aufwand: M

- Problem
- Langfristige Änderbarkeit des `DataGrid`
- Nutzen
- Stabilerer Weg zu 1.0+
- Aufwand: L

## 8. Scorecard

- Architektur: `7/10`
- Codequalität: `8/10`
- Konsistenz: `8/10`
- Wartbarkeit: `7/10`
- Testqualität: `8/10`
- Produktionsreife: `8/10`
- Entwicklererlebnis: `8/10`
- Zukunftsfähigkeit: `7/10`

## 9. Finale Einschätzung

- Würdest du dieses Repo heute in Produktion einsetzen?
- Ja.

- Unter welchen Bedingungen ja/nein?
- Ja, als produktionsnahe Component-Library mit guter Grundqualität.
- Nein nur dann, wenn maximale Härte gegenüber Browser-/Interop-Randfällen und vollständige Fehlertransparenz schon jetzt Pflicht sind.

- Was müsste vor einer 1.0 unbedingt noch passieren?
- Das Testsetup im Interop-Bereich härten
- `DataGrid` mittelfristig entkoppeln
- Die verbliebenen stillen Catch-/Logging-Pfade bereinigen

