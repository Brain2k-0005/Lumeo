# Technisches Audit – Aktualisierung V4

Stand: 2026-04-02

Prüfungen:
- `dotnet build src/Lumeo/Lumeo.csproj -c Release` erfolgreich
- `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build --verbosity minimal` erfolgreich mit `1238/1238` Tests
- Repo-weite Suche über `src`, `tests`, `.github` und `docs` nach Async-, Cleanup-, Interop-, Fehlerbehandlungs-, Doku- und CI-Mustern

## 1. Executive Summary

- Das Repository ist weiter gereift. Die früheren harten Defekte rund um Interop-Cross-Talk, Cleanup, `ToastSwipe`, `ColorPicker` und `Cascader` sind im aktuellen Stand nicht mehr sichtbar.
- Die Architektur der Infrastruktur ist besser geworden. `ComponentInteropService` ist jetzt in fachliche Interop-Adapter zerlegt, und `DataGrid` nutzt mit `DataGridLayoutService` und `DataGridServerService` separate Hilfsservices.
- Die Testsuite ist auf `1238` grüne Tests gewachsen. Das ist für eine Component-Library dieser Größe ein gutes Signal.
- Positiv ist auch, dass die früheren Runtime-API-Drifts inzwischen beseitigt sind, inklusive der Doku zu `UnregisterToastSwipe`.
- Die verbleibenden Schwächen sind heute eher Robustheits- und Governance-Themen: `JSRuntimeMode.Loose` bleibt der Standard im zentralen Testsetup, `bunit` ist weiterhin ein Preview-Paket, und einige Docs-/Demo-Pfade bleiben technischer als die eigentliche Library.
- Der größte strukturelle Risikoblock bleibt das `DataGrid`, nicht wegen eines akuten Bugs, sondern wegen der hohen Verantwortungsdichte.
- Insgesamt ist das Repo jetzt klar näher an einer sauberen, produktionsnahen Library als an einem Projekt mit kritischen Funktionsbrüchen.
- Einschätzung: `solide Library`
- Größte 3 Stärken
- Die zuvor kritischen Interop- und Cleanup-Pfade sind jetzt sauberer und stärker abgesichert
- Der Interop-Layer ist in kleine Adapter aufgeteilt
- CI, Build und Tests sind für das Projektlevel stark
- Größte 3 Risiken
- Interop-Tests sind wegen `Loose`-Mode weniger hart als sie aussehen
- `DataGrid` bleibt architektonisch sehr breit
- Docs-/Demo-Code ist teilweise noch lockerer als der Library-Code

## 2. Was technisch gut ist

- Titel
- Interop ist jetzt in fachliche Adapter zerlegt
- Warum gut
- `ComponentInteropService` delegiert an spezialisierte Adapter wie `SwipeInterop`, `FloatingPositionInterop`, `UtilityInterop`, `ResizeInterop` und `ScrollInterop`.
- Betroffene Dateien/Module
- [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L12)
- [SwipeInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/SwipeInterop.cs#L5)
- [FloatingPositionInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/FloatingPositionInterop.cs#L5)
- Auswirkung auf Qualität/Wartbarkeit
- Bessere Verantwortlichkeit, weniger Monsterklasse, weniger Risiko bei Änderungen in einzelnen Browser-Features.

- Titel
- `DataGrid`-Infrastruktur ist klarer getrennt als zuvor
- Warum gut
- Server-Requests und Layout-Persistenz leben jetzt in eigenen Hilfsservices statt weiter in der Hauptkomponente.
- Betroffene Dateien/Module
- [DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridServerService.cs#L5)
- [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L5)
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L236)
- Auswirkung auf Qualität/Wartbarkeit
- Die Komponente ist immer noch groß, aber Zustands- und I/O-Logik sind besser entkoppelt.

- Titel
- Interop-Fixes sind testseitig gut abgesichert
- Warum gut
- Es gibt gezielte Tests für `UnregisterToastSwipe`, `UnpositionFixed`, Callback-Routing und JS-Vertragsverhalten.
- Betroffene Dateien/Module
- [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L1)
- [InteropAdapterTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/Interop/InteropAdapterTests.cs#L1)
- [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L435)
- Auswirkung auf Qualität/Wartbarkeit
- Frühere Regressionen werden heute deutlich eher auffallen.

- Titel
- Service-Doku und Runtime-API sind wieder synchron
- Warum gut
- `UnregisterToastSwipe(toastId, elementId)` ist in der Doku jetzt konsistent zur Implementierung.
- Betroffene Dateien/Module
- [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L230)
- [ComponentInteropPage.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Pages/Docs/Services/ComponentInteropPage.razor#L716)
- Auswirkung auf Qualität/Wartbarkeit
- Weniger API-Fehlverwendung durch Konsumenten, weniger Drift zwischen Docs und Code.

- Titel
- CI ist brauchbar streng
- Warum gut
- Library und Docs-App werden gebaut, die Tests laufen und Vulnerable Packages werden geprüft.
- Betroffene Dateien/Module
- [.github/workflows/ci.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/ci.yml#L23)
- Auswirkung auf Qualität/Wartbarkeit
- Gute Basis für Merge-/Release-Entscheidungen.

## 3. Konkrete Schwächen

- Titel
- JS-Interop-Tests bleiben weniger scharf als sie aussehen
- Kategorie: Testqualität
- Betroffene Dateien/Module
- [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L13)
- [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L24)
- [ChartTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Components/Chart/ChartTests.cs#L17)
- Erklärung mit konkretem Codebezug
- Das zentrale Testsetup läuft weiter in `JSRuntimeMode.Loose`. Die sogenannten Strict-Interop-Tests verifizieren danach per `VerifyInvoke`, aber unerwartete Calls failen nicht sofort hart.
- Risikoauswirkung
- JS-/Interop-Regressionen können weiterhin durchrutschen, wenn nur ein Teil des Vertrags geprüft wird.
- Priorität: hoch

- Titel
- `DataGrid` bleibt architektonisch zu breit
- Kategorie: Architektur
- Betroffene Dateien/Module
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L1)
- [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L1)
- Erklärung mit konkretem Codebezug
- Trotz der neuen Services bündelt die Komponente weiter Rendering, Paging, Sortierung, Filter, Persistenz, Export, Auswahl und Server-Flow.
- Risikoauswirkung
- Hohe Wartungs- und Regressionskosten bei weiteren Features.
- Priorität: hoch

- Titel
- Fehlerbehandlung ist besser, aber noch nicht konsistent genug
- Kategorie: Wartbarkeit
- Betroffene Dateien/Module
- [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L345)
- [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L36)
- [echarts-interop.js](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/wwwroot/js/echarts-interop.js#L148)
- Erklärung mit konkretem Codebezug
- Ein Teil der Fehlerpfade loggt strukturiert nach `Console.Error`, andere Pfade fallen weiter auf generisches Ignore- oder Recovery-Verhalten zurück.
- Risikoauswirkung
- Diagnostik ist brauchbar, aber nicht durchgehend gut.
- Priorität: mittel

- Titel
- Docs-/Demo-Code bleibt teils lockerer als die Library
- Kategorie: Wartbarkeit
- Betroffene Dateien/Module
- [ThemeServicePage.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Pages/Docs/Services/ThemeServicePage.razor#L235)
- [FileUploadPage.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Pages/Components/FileUploadPage.razor#L125)
- [NotificationsPattern.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Pages/Patterns/NotificationsPattern.razor#L297)
- Erklärung mit konkretem Codebezug
- In der Docs-App gibt es weiter `async void`-Handler und vereinzelte stille Catch-Pfade.
- Risikoauswirkung
- Schlechtere Vorbildwirkung und unnötige Schulden im Showcase-Code.
- Priorität: niedrig

- Titel
- Teststack nutzt weiter ein Preview-Paket
- Kategorie: Robustheit
- Betroffene Dateien/Module
- [Lumeo.Tests.csproj](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Lumeo.Tests.csproj#L12)
- Erklärung mit konkretem Codebezug
- `bunit` bleibt auf `2.0.33-preview`.
- Risikoauswirkung
- Testinfrastruktur selbst ist beweglicher als nötig.
- Priorität: niedrig

## 4. Wahrscheinliche Bugs

- Zum aktuellen Stand habe ich keinen klar belegbaren funktionalen Bug in der Kern-Library gefunden.
- Die zuletzt bekannten Kernprobleme sind behoben oder regressionsseitig abgesichert.
- Die offenen Themen sind eher Testschärfe, Architektur und Diagnose als akute Fehlfunktion.

## 5. Architekturrisiken

- `DataGrid` bleibt ein Änderungs-Hotspot
- Warum
- Zu viele Verantwortlichkeiten sind weiterhin in engem Verbund.
- Welche Refactorings sinnvoll wären
- Persistenz, Export und Server-Orchestrierung weiter herausziehen.

- Interop bleibt trotz Zerlegung zentraler Infrastrukturcode
- Warum
- Die Adapterstruktur ist besser, aber viele Browser-Features hängen weiterhin an gemeinsamem Lifecycle- und Disposal-Verhalten.
- Welche Refactorings sinnvoll wären
- Noch klarere Grenzen zwischen Adapter, Service und UI-Konsument ziehen.

- Docs und Library entwickeln sich nicht immer gleich schnell
- Warum
- Die Library wurde härter gemacht als einige Doku-/Demo-Stellen.
- Welche Refactorings sinnvoll wären
- Ein konsistenteres Qualitätsniveau für Showcase-Code anstreben.

## 6. Test- und Qualitätsbewertung

- Was an den Tests gut ist
- `1238` grüne Tests sind stark.
- Es gibt jetzt sowohl breite Service-Tests als auch gezielte Vertragsprüfungen.
- Die Regressionstests decken die ehemals kritischen Interop-Pfade deutlich besser ab.

- Welche kritischen Bereiche unzureichend abgesichert wirken
- Harte JS-/Interop-Verträge ohne `Loose`-Fallback
- Komplexe `DataGrid`-Szenarien mit echten Asynchronitäts- und Zustandsübergängen
- Browsernahe Verifikation für JS-lastige Komponenten wie Chart

- Ob CI/CD ausreichend ist
- Für das Projektlevel ja. Die Pipeline ist inhaltlich brauchbar.

- Welche Checks fehlen
- Noch schärfere JS-/Interop-Validierung
- Optional browsernahe Smoke-Tests für High-Risk-Komponenten

## 7. Top-10 Maßnahmen

- Problem
- `JSRuntimeMode.Loose` im zentralen Testsetup
- Nutzen
- Höhere Aussagekraft der Interop-Tests
- Aufwand: M

- Problem
- `DataGrid` bleibt zu breit
- Nutzen
- Bessere Wartbarkeit und geringere Regressionskosten
- Aufwand: L

- Problem
- Fehlerbehandlung bleibt inkonsistent
- Nutzen
- Bessere Diagnosefähigkeit
- Aufwand: M

- Problem
- Docs-/Demo-Code enthält noch `async void`
- Nutzen
- Einheitlicheres Qualitätsniveau im Repo
- Aufwand: M

- Problem
- `bunit`-Preview im Teststack
- Nutzen
- Stabilerer Testunterbau
- Aufwand: S

- Problem
- Weitere harte Interop-Assertions
- Nutzen
- Früheres Auffallen von JS-Vertragsbrüchen
- Aufwand: M

- Problem
- `DataGrid`-Integrationstiefe erweitern
- Nutzen
- Höhere Sicherheit bei komplexen Zustandsübergängen
- Aufwand: M

- Problem
- Bessere Doku-/Code-Konsistenz sichern
- Nutzen
- Weniger Fehlverwendung durch Konsumenten
- Aufwand: S

- Problem
- Showcase-Code an Library-Qualität angleichen
- Nutzen
- Bessere Vorbildwirkung
- Aufwand: M

- Problem
- Langfristige Entkopplung zentraler Service-Verantwortung
- Nutzen
- Weniger Seiteneffekte und klarere Ownership
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
- Ja, als produktionsnahe Component-Library mit guter technischer Basis.
- Nein nur dann, wenn harte JS-/Interop-Verträge und maximale Durchsetzung gegen Randfehler schon heute zwingend sind.

- Was müsste vor einer 1.0 unbedingt noch passieren?
- Das Interop-Testsetup weiter härten
- `DataGrid` mittelfristig weiter entkoppeln
- Docs-/Demo-Code an die Qualitätslinie der Library angleichen

