# Technisches Audit – Aktualisierung V3

Stand: 2026-04-02

Prüfungen:
- `dotnet build src/Lumeo/Lumeo.csproj -c Release` erfolgreich
- `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build --verbosity minimal` erfolgreich mit `1161/1161` Tests
- Repo-weite Suche über `src`, `tests`, `.github` und `docs` nach Async-, Cleanup-, Interop-, Fehlerbehandlungs-, Doku- und CI-Mustern

## 1. Executive Summary

- Das Repository ist erneut besser geworden. Der vorherige belastbare Doku-/API-Drift bei `UnregisterToastSwipe` ist inzwischen korrigiert, und der allgemeine Trend bleibt positiv.
- Die Kern-Library wirkt heute robust: Build ist sauber grün, die Testsuite ist auf `1161` erfolgreiche Tests angewachsen, und frühere Runtime-Defekte sind weiterhin nicht wieder aufgetaucht.
- Die früheren High-Risk-Punkte rund um Provider-Async, Interop-Cleanup, `ToastSwipe`, `ColorPicker` und `Cascader` sind im aktuellen Stand weiter sauber.
- Die CI ist weiterhin sinnvoll aufgesetzt mit `-warnaserror`, Docs-Build und Vulnerability-Check.
- Die verbleibenden Schwächen sind heute überwiegend Qualitäts- und Governance-Themen: Das JS-/Interop-Testsetup bleibt wegen `Loose`-Mode weicher als ideal, `bunit` läuft weiter als Preview-Paket, und einige Catch-/Clipboard-/Demo-Pfade leben noch in einer “best effort”-Zone.
- Die wichtigste Einschränkung für langfristige Wartbarkeit bleibt das `DataGrid`: funktional brauchbar, architektonisch aber weiterhin zu breit geschnitten.
- Es gibt im aktuellen Stand keinen klar belegbaren echten Bug in der Kern-Library, der sich aus der aktuellen Codeprüfung unmittelbar ableiten ließ.
- Gesamturteil: solide, produktionsnahe Library mit wenigen verbleibenden technischen Schulden.
- Einschätzung: `solide Library`
- Größte 3 Stärken
- Mehrere frühere Defekte wurden stabil behoben und regressionsseitig abgesichert
- Kern-Library-Async und Interop sind heute deutlich sauberer als in den ersten Audits
- CI und Testbasis sind für das Projektlevel stark
- Größte 3 Risiken
- Interop-Testschärfe bleibt durch `JSRuntimeMode.Loose` begrenzt
- `DataGrid` bleibt ein teurer architektonischer Knoten
- Ein Teil der Fehlerbehandlung ist weiter nur “log or ignore”, besonders außerhalb der Kern-Library

## 2. Was technisch gut ist

- Titel
- Runtime-API und Doku sind bei `UnregisterToastSwipe` wieder synchron
- Warum gut
- Die Service-Doku zeigt jetzt dieselbe Signatur wie die Runtime: `UnregisterToastSwipe(toastId, elementId)`.
- Betroffene Dateien/Module
- [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L280)
- [ComponentInteropPage.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Pages/Docs/Services/ComponentInteropPage.razor#L716)
- Auswirkung auf Qualität/Wartbarkeit
- Weniger Fehlverwendung der API durch Konsumenten und geringerer Doku-Drift.

- Titel
- Provider-Eventverarbeitung ist weiter sauber
- Warum gut
- `ToastProvider` und `OverlayProvider` arbeiten nicht mehr mit `async void`, sondern delegieren per `InvokeAsync(...)` auf `Task`-Methoden.
- Betroffene Dateien/Module
- [ToastProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Toast/ToastProvider.razor#L44)
- [OverlayProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Overlay/OverlayProvider.razor#L88)
- Auswirkung auf Qualität/Wartbarkeit
- Stabilere Lifecycle-Pfade und geringeres Risiko schwer reproduzierbarer Renderer-Probleme.

- Titel
- Interop-Fixes sind heute gut regressionsgesichert
- Warum gut
- Neben den generellen Service-Tests existieren jetzt gezielte Verifikationen für `UnregisterToastSwipe`, `UnpositionFixed` und weitere JS-Aufrufe.
- Betroffene Dateien/Module
- [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L463)
- [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L1)
- Auswirkung auf Qualität/Wartbarkeit
- Die ehemals fragilen Interop-Pfade sind heute deutlich regressionsfester.

- Titel
- CI bleibt als Qualitätsbarriere glaubwürdig
- Warum gut
- Library und Docs-App werden gebaut, Tests laufen, Warnungen sind Fehler, und es gibt einen Vulnerability-Check.
- Betroffene Dateien/Module
- [ci.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/ci.yml#L23)
- Auswirkung auf Qualität/Wartbarkeit
- Gute Merge-/Release-Disziplin für ein Component-Library-Repo.

## 3. Konkrete Schwächen

- Titel
- Interop-Tests sind präziser, aber nicht wirklich strict
- Kategorie: Testqualität
- Betroffene Dateien/Module
- [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L13)
- [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L24)
- [ChartTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Components/Chart/ChartTests.cs#L17)
- Erklärung mit konkretem Codebezug
- Auch die “StrictInteropTests” laufen wegen bUnit-Modulverhalten im `Loose`-Mode und prüfen danach per `VerifyInvoke`. Das ist nützlich, aber unerwartete Calls failen nicht sofort als harter Vertragsbruch.
- Risikoauswirkung
- JS-/Interop-Regressionen können weiterhin durchrutschen, wenn nur ein Ausschnitt des Call-Vertrags geprüft wird.
- Priorität: hoch

- Titel
- `DataGrid` bleibt architektonisch zu breit
- Kategorie: Architektur
- Betroffene Dateien/Module
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L1)
- [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L1)
- Erklärung mit konkretem Codebezug
- Persistenz, Export, Server-Requests, Filter, Sortierung, Fehlerbehandlung und UI-Steuerung bleiben eng gekoppelt.
- Risikoauswirkung
- Hohe Änderungs- und Regressionskosten trotz aktuell funktionalem Zustand.
- Priorität: hoch

- Titel
- Fehlerbehandlung ist verbessert, aber nicht durchgehend konsistent
- Kategorie: Wartbarkeit
- Betroffene Dateien/Module
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L382)
- [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L345)
- [DataGridFilterOperator.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridFilterOperator.cs#L49)
- [echarts-interop.js](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/wwwroot/js/echarts-interop.js#L148)
- Erklärung mit konkretem Codebezug
- Ein Teil der Fehlerpfade loggt inzwischen sauberer, andere Pfade enden weiterhin in stillem oder fast stillem Recovery-Verhalten.
- Risikoauswirkung
- Die Diagnose ist besser als früher, aber nicht systematisch gut.
- Priorität: mittel

- Titel
- Teststack verwendet weiter ein Preview-Paket
- Kategorie: Robustheit
- Betroffene Dateien/Module
- [Lumeo.Tests.csproj](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Lumeo.Tests.csproj#L12)
- Erklärung mit konkretem Codebezug
- `bunit` ist weiterhin auf `2.0.33-preview`.
- Risikoauswirkung
- Das ist kein Laufzeitrisiko für Konsumenten, aber ein unnötiger Bewegungsfaktor in der Testinfrastruktur.
- Priorität: niedrig

- Titel
- Einige Demo-/Docs-Pfade sind technisch lockerer als die Library selbst
- Kategorie: Wartbarkeit
- Betroffene Dateien/Module
- [ThemeServicePage.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Pages/Docs/Services/ThemeServicePage.razor#L235)
- [FileUploadPage.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Pages/Components/FileUploadPage.razor#L125)
- [CustomizerSidebar.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Shared/CustomizerSidebar.razor#L644)
- Erklärung mit konkretem Codebezug
- In der Docs-App gibt es weiter `async void` und stille `catch`-Blöcke. Das betrifft nicht die Kern-Library, aber die Qualität des Showcase-Codes.
- Risikoauswirkung
- Schlechtere Vorbildwirkung und unnötige technische Schulden im Docs-/Demo-Teil.
- Priorität: niedrig

## 4. Wahrscheinliche Bugs

- Zum aktuellen Stand habe ich in der Kern-Library keinen klar belegbaren funktionalen Bug gefunden.
- Die vorher identifizierte Doku-Inkonsistenz ist inzwischen behoben.
- Verbleibende Risiken betreffen aktuell eher Testschärfe, Architektur und Supportbarkeit als offensichtliche Fehlfunktion.

## 5. Architekturrisiken

- `DataGrid` bleibt strukturell teuer
- Warum
- Viele Verantwortlichkeiten sind in wenigen stark gekoppelten Komponenten konzentriert.
- Welche Refactorings sinnvoll wären
- Persistenz, Server-Orchestrierung und Export aus der großen Oberfläche herauslösen.

- Interop-Testarchitektur ist praktisch, aber nicht maximal hart
- Warum
- Der aktuelle Ansatz nutzt `Loose`-Mode plus nachgelagerte Verifikation.
- Welche Refactorings sinnvoll wären
- Mehr gezielte Vertragsprüfungen und wo möglich schärfere Modul-Assertions.

- Docs-Code und Library-Code haben unterschiedliche Qualitätsniveaus
- Warum
- Die Library wurde deutlich härter gemacht als einige Showcase-/Demo-Pfade.
- Welche Refactorings sinnvoll wären
- Docs-App bei Async-/Fehlerbehandlung stärker auf Bibliotheksniveau anheben.

## 6. Test- und Qualitätsbewertung

- Was an den Tests gut ist
- `1161` grüne Tests sind stark.
- Zusätzliche Interop-Regressionen und `StrictInteropTests` erhöhen die Aussagekraft gegenüber früher deutlich.
- Die Suite ist schnell genug, um als echte Guardrail zu dienen.

- Welche kritischen Bereiche unzureichend abgesichert wirken
- Vollständig harte JS-Interop-Verträge ohne `Loose`-Fallback
- Tiefergehende `DataGrid`-Szenarien
- Browsernahe Verifikation bei JS-lastigen Komponenten wie Chart

- Ob CI/CD ausreichend ist
- Ja, für das Projektlevel ist die CI inzwischen gut.

- Welche Checks fehlen
- Noch schärfere JS-/Interop-Validierung
- Optional browsernahe Smoke-Checks für High-Risk-Komponenten

## 7. Top-10 Maßnahmen

- Problem
- `JSRuntimeMode.Loose` im Interop-Testkern
- Nutzen
- Höhere Aussagekraft bei JS-/Interop-Regressionen
- Aufwand: M

- Problem
- `DataGrid` bleibt zu breit
- Nutzen
- Bessere Wartbarkeit und geringere Regressionskosten
- Aufwand: L

- Problem
- Uneinheitliche Catch-/Logging-Strategie
- Nutzen
- Bessere Diagnosefähigkeit
- Aufwand: M

- Problem
- `bunit`-Preview im Teststack
- Nutzen
- Stabilerer Testunterbau
- Aufwand: S

- Problem
- Docs-/Demo-Code liegt qualitativ hinter der Library
- Nutzen
- Bessere Konsistenz und Vorbildwirkung
- Aufwand: M

- Problem
- Mehr harte Interop-Argumentprüfungen für weitere Methoden
- Nutzen
- Früheres Auffallen von JS-Vertragsbrüchen
- Aufwand: M

- Problem
- Mehr `DataGrid`-Integrationstests
- Nutzen
- Höhere Sicherheit in komplexen Zustandsübergängen
- Aufwand: M

- Problem
- Verbleibende generische Recovery-Pfade
- Nutzen
- Klarere Fehlerbilder
- Aufwand: S

- Problem
- Langfristige Entkopplung zentraler Services
- Nutzen
- Weniger Seiteneffekte und klarere Ownership
- Aufwand: L

- Problem
- Browsernahe Tests für JS-lastige Komponenten
- Nutzen
- Weniger Lücke zwischen bUnit und echtem Laufzeitverhalten
- Aufwand: M

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
- Ja, als produktionsnahe Component-Library mit guter technischer Basis.
- Nein nur dann, wenn maximale Strenge bei JS-/Interop-Verträgen und sofortige harte Fehlertransparenz schon heute Pflicht sind.

- Was müsste vor einer 1.0 unbedingt noch passieren?
- Das Interop-Testsetup weiter härten
- `DataGrid` mittelfristig entkoppeln
- Die verbliebenen Catch-/Logging-Inkonsistenzen bereinigen

