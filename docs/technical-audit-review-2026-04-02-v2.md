# Technisches Audit – Aktualisierung V2

Stand: 2026-04-02

Prüfungen:
- `dotnet build src/Lumeo/Lumeo.csproj -c Release` erfolgreich
- `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build --verbosity minimal` erfolgreich mit `1161/1161` Tests
- Repo-weite Suche über `src`, `tests`, `.github` und `docs` nach Async-, Cleanup-, Interop-, Nullability-, Doku- und CI-Mustern

## 1. Executive Summary

- Das Repo ist erneut besser geworden. Der Trend der letzten Reviews setzt sich fort: frühere technische Schwachstellen werden nicht nur behoben, sondern zunehmend auch testseitig abgesichert.
- Build und Tests sind sauber grün, die Pipeline bleibt sinnvoll streng, und die Interop-/Cleanup-Pfade wirken heute deutlich kontrollierter als zu Beginn des Audits.
- Besonders positiv ist, dass die früher problematischen Provider-Pfade nicht mehr mit `async void` arbeiten, sondern jetzt synchron auf `InvokeAsync(...)/Task` umgestellt wurden.
- Die Service- und Interop-Tests sind auf `1161` grüne Tests gewachsen und enthalten inzwischen zusätzliche Regressionen für `UnregisterToastSwipe` und `UnpositionFixed`.
- Das größte technische Restthema ist heute nicht mehr ein klarer Kernbug, sondern die Differenz zwischen tatsächlicher Qualität und der Test-/Doku-Schärfe: Interop-Tests laufen weiter in `Loose`-Mode, und die Doku ist an mindestens einer Stelle nicht mehr mit der Runtime-API synchron.
- Architektonisch bleibt `DataGrid` der größte Risikoblock. Nicht wegen eines aktuell belegbaren Defekts, sondern weil zu viele Verantwortlichkeiten in einer zusammenhängenden Oberfläche bleiben.
- Positiv ist auch, dass etliche frühere `catch { }`-Stellen inzwischen auf Logging umgestellt wurden. Ganz konsistent ist die Fehlerstrategie aber noch nicht.
- Mein Urteil ist daher nochmals leicht besser als im letzten Review: Das ist inzwischen eine klar solide, produktionsnahe Library mit überschaubaren, aber noch realen Restbaustellen.
- Einschätzung: `solide Library`
- Größte 3 Stärken
- Frühere Defekte wurden behoben und jetzt stärker regressionsgesichert
- CI-/Build-Qualität ist für das Projektlevel gut
- Interop-, Cleanup- und Provider-Lifecycle sind erkennbar robuster als zuvor
- Größte 3 Risiken
- JS-/Interop-Tests bleiben wegen `Loose`-Mode weniger scharf als sie wirken
- Die Service-Dokumentation ist nicht vollständig synchron zur Runtime-API
- `DataGrid` bleibt architektonisch zu breit für langfristig günstige Wartung

## 2. Was technisch gut ist

- Titel
- Provider-Async ist jetzt sauber modelliert
- Warum gut
- `ToastProvider` und `OverlayProvider` hängen ihre Events synchron an und delegieren intern per `InvokeAsync` auf `Task`-Methoden.
- Betroffene Dateien/Module
- [ToastProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Toast/ToastProvider.razor#L44)
- [OverlayProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Overlay/OverlayProvider.razor#L88)
- Auswirkung auf Qualität/Wartbarkeit
- Deutlich saubererer Async-/Renderer-Pfad als in den früheren Ständen.

- Titel
- Interop-Regressionen sind breiter abgesichert
- Warum gut
- Die Tests decken jetzt zusätzlich `UnregisterToastSwipe` und `UnpositionFixed` explizit ab.
- Betroffene Dateien/Module
- [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L463)
- [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L487)
- [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L1)
- Auswirkung auf Qualität/Wartbarkeit
- Frisch gefixte Interop-Pfade sind weniger regressionsanfällig.

- Titel
- `ToastSwipe`- und Floating-Cleanup-Pfade sind technisch konsistent
- Warum gut
- `UnregisterToastSwipe(string toastId, string elementId)` ist jetzt konsistent zur Speicherung unter `toastId`, und `UnpositionFixed(...)` wird breit genutzt.
- Betroffene Dateien/Module
- [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L280)
- [PopoverContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Popover/PopoverContent.razor#L81)
- [DropdownMenuContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DropdownMenu/DropdownMenuContent.razor#L119)
- [MenubarContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Menubar/MenubarContent.razor#L53)
- [NavigationMenuContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/NavigationMenu/NavigationMenuContent.razor#L57)
- Auswirkung auf Qualität/Wartbarkeit
- Weniger Cleanup-Leaks und weniger implizite Mehrinstanz-Probleme.

- Titel
- CI bleibt sinnvoll streng
- Warum gut
- Build läuft mit `-warnaserror`, die Docs-App wird mitgebaut und ein Vulnerability-Check ist Teil der Pipeline.
- Betroffene Dateien/Module
- [ci.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/ci.yml#L23)
- Auswirkung auf Qualität/Wartbarkeit
- Gute Release-/Merge-Barriere für ein OSS-Component-Projekt.

## 3. Konkrete Schwächen

- Titel
- Service-Doku ist nicht vollständig mit der Runtime-API synchron
- Kategorie: API-Inkonsistenz
- Betroffene Dateien/Module
- [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L280)
- [ComponentInteropPage.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Pages/Docs/Services/ComponentInteropPage.razor#L716)
- Erklärung mit konkretem Codebezug
- Die Runtime-API lautet `UnregisterToastSwipe(string toastId, string elementId)`, die Doku listet weiterhin `UnregisterToastSwipe(elementId)`.
- Risikoauswirkung
- Library-Konsumenten können auf Basis der offiziellen Doku falschen Code schreiben.
- Priorität: hoch

- Titel
- JS-Interop-Tests laufen weiter in `Loose`-Mode
- Kategorie: Testqualität
- Betroffene Dateien/Module
- [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L13)
- [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L24)
- [ChartTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Components/Chart/ChartTests.cs#L17)
- Erklärung mit konkretem Codebezug
- Auch die “StrictInteropTests” arbeiten wegen bUnit-Modulgrenzen im `Loose`-Mode und validieren danach über `VerifyInvoke`. Das ist brauchbar, aber nicht wirklich strict im Sinne von “unerwartete Calls failen sofort”.
- Risikoauswirkung
- Interop-Fehler können weiterhin durchrutschen, wenn nur ein Teil des Call-Vertrags geprüft wird.
- Priorität: mittel

- Titel
- `DataGrid` bleibt der größte Wartungsblock
- Kategorie: Architektur
- Betroffene Dateien/Module
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L1)
- [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L1)
- Erklärung mit konkretem Codebezug
- Persistenz, Export, Filter, Sortierung, Server-Loading, UI-Steuerung und Fehlerbehandlung sind weiterhin stark gekoppelt.
- Risikoauswirkung
- Langfristig hohe Änderungs- und Regressionskosten.
- Priorität: hoch

- Titel
- Fehlerbehandlung ist verbessert, aber noch nicht konsistent
- Kategorie: Wartbarkeit
- Betroffene Dateien/Module
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L382)
- [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L345)
- [DataGridFilterOperator.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridFilterOperator.cs#L49)
- [echarts-interop.js](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/wwwroot/js/echarts-interop.js#L148)
- Erklärung mit konkretem Codebezug
- Ein Teil der früheren stillen Catch-Blöcke loggt jetzt nach `Console.Error`, andere Pfade verschlucken weiter Fehler oder fallen nur auf generische Recovery zurück.
- Risikoauswirkung
- Das Repo ist supportbarer als früher, aber noch nicht durchgehend gut diagnosierbar.
- Priorität: mittel

- Titel
- Teststack verwendet weiter ein Preview-Paket im Kern
- Kategorie: Robustheit
- Betroffene Dateien/Module
- [Lumeo.Tests.csproj](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Lumeo.Tests.csproj#L12)
- Erklärung mit konkretem Codebezug
- `bunit` ist weiter auf `2.0.33-preview`.
- Risikoauswirkung
- Testinfrastruktur selbst kann instabiler oder semantisch beweglicher sein als nötig.
- Priorität: niedrig

## 4. Wahrscheinliche Bugs

- Zum aktuellen Stand habe ich in den früher kritischen Runtime-Pfaden keinen klar belegbaren funktionalen Bug mehr gefunden.
- Die neu identifizierte harte Inkonsistenz betrifft aktuell die Doku, nicht die Runtime: `UnregisterToastSwipe` ist in der Dokumentation falsch beschrieben.

## 5. Architekturrisiken

- `DataGrid` funktioniert, ist aber strukturell weiterhin teuer
- Warum
- Zu viele Verantwortlichkeiten hängen in eng gekoppelten Komponenten.
- Welche Refactorings sinnvoll wären
- Persistenz, Export und Server-State in eigene interne Bausteine trennen.

- Testarchitektur ist stärker geworden, aber noch nicht maximal scharf
- Warum
- `Loose`-Mode bleibt der zentrale Kompromiss im JS-/Interop-Testbereich.
- Welche Refactorings sinnvoll wären
- Kritische Interop-Komponenten mit engeren Assert-Verträgen und expliziteren Argumentprüfungen absichern.

- Docs und Runtime können weiter auseinanderlaufen
- Warum
- Service-Signaturen werden verbessert, aber Doku scheint nicht automatisch an denselben Vertrag gekoppelt.
- Welche Refactorings sinnvoll wären
- API-Doku stärker aus Code ableiten oder Signatur-Checks in Docs-Reviews einführen.

## 6. Test- und Qualitätsbewertung

- Was an den Tests gut ist
- `1161` grüne Tests sind stark.
- Die Suite enthält inzwischen explizite Interop-Regressionen, nicht nur Smoke-Tests.
- `StrictInteropTests` prüfen konkrete JS-Funktionsnamen und Argumentreihenfolgen in [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L1).

- Welche kritischen Bereiche unzureichend abgesichert wirken
- Vollständig strikte Interop-Validierung ohne `Loose`-Fallback
- Komplexere `DataGrid`-Szenarien unter realistischeren Zustandswechseln
- Konsistenz zwischen Runtime-API und Doku

- Ob CI/CD ausreichend ist
- Für dieses Projektlevel ja. Die CI ist heute klar brauchbar.

- Welche Checks fehlen
- Ein expliziter Doku-Konsistenzcheck für Service-APIs
- Noch schärfere Interop-Verifikation auf Testebene

## 7. Top-10 Maßnahmen

- Problem
- Falsche Doku-Signatur für `UnregisterToastSwipe`
- Nutzen
- Verhindert Fehlverwendung durch Konsumenten
- Aufwand: S

- Problem
- `JSRuntimeMode.Loose` im Kern-Testsetup
- Nutzen
- Höhere Aussagekraft der Interop-Tests
- Aufwand: M

- Problem
- `DataGrid` bleibt zu breit
- Nutzen
- Bessere Wartbarkeit und geringere Regressionskosten
- Aufwand: L

- Problem
- Uneinheitliche Catch-/Logging-Strategie
- Nutzen
- Bessere Diagnose in Produktion
- Aufwand: M

- Problem
- `bunit`-Preview im Teststack
- Nutzen
- Stabilerer Testunterbau
- Aufwand: S

- Problem
- Noch wenig automatische Doku-Vertragsprüfung
- Nutzen
- Weniger Drift zwischen Code und Docs
- Aufwand: M

- Problem
- `DataGrid`-Integrationstiefe in Tests
- Nutzen
- Höhere Sicherheit bei komplexen Zustandsübergängen
- Aufwand: M

- Problem
- Striktere Argument-Assertions für weitere Interop-Methoden
- Nutzen
- Frühere Fehlererkennung bei JS-Regressionen
- Aufwand: M

- Problem
- Verbleibende generische Error-Recovery-Pfade
- Nutzen
- Klarere Support- und Betriebsfähigkeit
- Aufwand: S

- Problem
- Langfristige Entkopplung zentraler Infrastrukturservices
- Nutzen
- Geringere Seiteneffekte und klarere Ownership
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
- Nein nur dann, wenn absolute Strenge bei Doku-/Interop-Verträgen und maximale Testhärte schon heute Pflicht sind.

- Was müsste vor einer 1.0 unbedingt noch passieren?
- Die Service-Doku auf den realen API-Vertrag bringen
- Die Interop-Tests weiter härten
- `DataGrid` mittelfristig strukturell entkoppeln

