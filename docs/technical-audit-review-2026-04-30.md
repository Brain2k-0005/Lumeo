# Lumeo Technical Audit Review - 2026-04-30

## 1. Executive Summary

- Aktueller Stand: der Code ist deutlich naeher an "production ready" als bei den frueheren Reviews. `dotnet build` ist gruen, und `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build` laeuft aktuell mit `1316` bestandenen und `1` uebersprungenen Test.
- Die grossen frueheren Defekte im Runtime-Teil wirken behoben: Chart-Reinit bei Themewechsel, DataGrid-Autosave, KeyboardShortcut-Dispose und die Interop-Cross-Talk-Probleme sind im aktuellen Source nicht mehr als offene Blocker sichtbar.
- Gleichzeitig ist die Codebase klar komplexer geworden. Neben der eigentlichen Library gibt es jetzt eine ernsthafte Docs-/Prerender-Pipeline mit `scripts/prerender/*`, Cloudflare-Deployment-Hooks und vorbereiteten OG-/Preview-Assets.
- Meine Gesamtbewertung ist deshalb: produktionsnah und fuer eine 1.0 realistisch, aber noch nicht "blind shippen ohne Caveats".
- Groesste 3 Staerken: saubere Lifecycle-Haerte in den zentralen Komponenten, deutlich bessere CI-/Release-Pipeline, und eine grosse Testbasis mit gezielten Strict-Interop-Tests.
- Groesste 3 Risiken: ein noch fragiler Async-Randpfad im Docs-TOC, ein fire-and-forget Debounce-Pfad im DataGrid, und weiterhin zu lockere JSInterop-Defaults im gemeinsamen Test-Setup.

## 2. Was technisch gut ist

- Titel: Chart-Lifecycle und Group-Rejoin sind sauberer als frueher
- Warum gut: `Chart.razor` reinitialisiert bei Themewechsel den ECharts-Container und ruft danach erneut `RegisterChartEventsAsync()` auf. Dort wird auch `Group` wieder verbunden. Das schliesst den frueheren Zustand, in dem Themewechsel das Chart-Grouping still kaputt machen konnten.
- Betroffene Dateien/Module: [src/Lumeo.Charts/UI/Chart/Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo.Charts/UI/Chart/Chart.razor)
- Auswirkung auf Qualitaet/Wartbarkeit: weniger versteckte Zustandsfehler, besseres Verhalten bei dynamischen Theme-Wechseln und klarere Trennung zwischen Init, Reinit und Event-Registrierung.

- Titel: DataGrid-Autosave und Request-Generation sind nachvollziehbar abgesichert
- Warum gut: `DataGridLayoutService` fuehrt eine Save-Generation und verwirft stale Saves, waehrend `DataGridServerService` die Loading-State-Ruecksetzung nur fuer die aktuelle Request-Generation erlaubt. Das ist eine solide Antwort auf die frueheren Race-Conditions.
- Betroffene Dateien/Module: [src/Lumeo.DataGrid/UI/DataGrid/DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo.DataGrid/UI/DataGrid/DataGridLayoutService.cs), [src/Lumeo.DataGrid/UI/DataGrid/DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo.DataGrid/UI/DataGrid/DataGridServerService.cs)
- Auswirkung auf Qualitaet/Wartbarkeit: der Code ist robuster gegen schnelle UI-Aenderungen, und die Persistenz ist nicht mehr blind "last write wins".

- Titel: KeyboardShortcutService hat einen klaren async Dispose-Pfad
- Warum gut: Shortcut-Handles implementieren jetzt nur noch `IAsyncDisposable`, und das Unregister laeuft ueber den Service statt ueber einen gemischten Sync/Async-Hybrid.
- Betroffene Dateien/Module: [src/Lumeo/Services/KeyboardShortcutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/KeyboardShortcutService.cs)
- Auswirkung auf Qualitaet/Wartbarkeit: saubererer Lifecycle, weniger Ghost-Shortcuts und weniger Semantik-Drift zwischen Handle und Service.

- Titel: Release-Pipeline ist jetzt deutlich ernster
- Warum gut: Der Deploy-Workflow baut nicht nur die App, sondern fuehrt auch die Prerender-Stufe und eine Live-Verify-Stufe aus. Die neue `scripts/prerender`-Kette crawlt die Sitemap, schreibt statische Seiten und kann danach gegen eine echte URL pruefen.
- Betroffene Dateien/Module: [.github/workflows/deploy-cloudflare.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/deploy-cloudflare.yml), [scripts/prerender/prerender.mjs](/C:/Users/bemi/RiderProjects/Lumeo/scripts/prerender/prerender.mjs), [scripts/prerender/verify.mjs](/C:/Users/bemi/RiderProjects/Lumeo/scripts/prerender/verify.mjs), [scripts/prerender/server.mjs](/C:/Users/bemi/RiderProjects/Lumeo/scripts/prerender/server.mjs)
- Auswirkung auf Qualitaet/Wartbarkeit: besserer First Paint fuer Docs, weniger "SPA shell" im Crawl, und eine reale Validierung der Release-Artefakte.

- Titel: Die Testbasis ist breit und inzwischen scharf genug an den richtigen Stellen
- Warum gut: Es gibt nicht nur Render-Tests, sondern auch gezielte Service-Tests und eine `StrictInteropTests`-Suite, die konkrete JS-Invocationen verifiziert.
- Betroffene Dateien/Module: [tests/Lumeo.Tests/Services/StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs), [tests/Lumeo.Tests/Services/DataGridServerServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/DataGridServerServiceTests.cs), [tests/Lumeo.Tests/Services/DataGridLayoutServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/DataGridLayoutServiceTests.cs)
- Auswirkung auf Qualitaet/Wartbarkeit: die Tests decken nun mehr als nur "does not throw" ab und sind bei den zentralen Servicepfaden sinnvoller.

## 3. Konkrete Schwächen

- Titel: `OnThisPage` nutzt weiter `async void` als Navigation-Handler
- Kategorie: Bug-Risiko
- Betroffene Dateien/Module: [docs/Lumeo.Docs/Shared/OnThisPage.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Shared/OnThisPage.razor)
- Erklaerung mit konkretem Codebezug: `OnLocationChanged` ist als `async void` implementiert und ruft `RescanWithRetry()` ohne Cancellation- oder Disposal-Guards auf. Die Scan-Schleife laeuft damit auch dann noch weiter, wenn die Komponente bereits im Routenwechsel steckt.
- Risikoauswirkung: unnötige Arbeit nach Navigation, moegliche unobserved Fehlerpfade und ein fragiler Lifecycle-Randfall in einer global eingebundenen Docs-Komponente.
- Prioritaet: mittel

- Titel: DataGrid-Debounce zwingt async Arbeit in einen sync Callback
- Kategorie: Bug-Risiko
- Betroffene Dateien/Module: [src/Lumeo.DataGrid/UI/DataGrid/DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo.DataGrid/UI/DataGrid/DataGrid.razor), [src/Lumeo.DataGrid/UI/DataGrid/DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo.DataGrid/UI/DataGrid/DataGridServerService.cs)
- Erklaerung mit konkretem Codebezug: `DebounceSearch` nimmt nur ein `Action`, waehrend der Aufrufer in `DataGrid.razor` damit `InvokeAsync(async () => { await RequestServerData(); StateHasChanged(); })` fire-and-forget ausfuehrt. Der Rueckgabewert wird ignoriert, und der Timerpfad hat keinen eigenen Fehler- oder Disposal-Guard.
- Risikoauswirkung: Exceptions oder Renderer-Fehler koennen im Debounce-Fall unobserved bleiben; bei Navigation waehrend eines laufenden Debounces ist das der fragilste asynchrone Randpfad im DataGrid.
- Prioritaet: mittel

- Titel: Gemeinsames Test-Setup bleibt absichtlich zu locker
- Kategorie: Testqualitaet
- Betroffene Dateien/Module: [tests/Lumeo.Tests/Helpers/TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs), [tests/Lumeo.Tests/Services/StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs)
- Erklaerung mit konkretem Codebezug: Das Standard-Setup setzt `ctx.JSInterop.Mode = JSRuntimeMode.Loose`, damit viele JS-Fehler nicht sofort auffallen. Die neue Strict-Suite ist gut, aber sie deckt nur einen Teil der interop-intensiven Pfade ab.
- Risikoauswirkung: einige JS-Vertragsbrueche in Komponenten koennen weiter durchrutschen, obwohl die Tests gruen sind.
- Prioritaet: mittel

- Titel: Docs-Fehler werden an einigen Stellen nur geloggt
- Kategorie: Wartbarkeit
- Betroffene Dateien/Module: [docs/Lumeo.Docs/Layout/MainLayout.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Layout/MainLayout.razor), [docs/Lumeo.Docs/Shared/CustomizerSidebar.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Shared/CustomizerSidebar.razor)
- Erklaerung mit konkretem Codebezug: Initialisierungsfehler in Layout-/Customizer-Pfaden landen teilweise in `Console.WriteLine`/`Console.Error` und werden nicht weiter modelliert. Das ist fuer eine Docs-App weniger kritisch als im Kernpaket, aber es macht Fehlerbilder schwerer sichtbar.
- Risikoauswirkung: schwierigere Diagnose bei Shell-/Docs-Regressionen und weniger klare Fehleruebergaenge fuer Benutzer.
- Prioritaet: niedrig

## 4. Wahrscheinliche Bugs

- Datei: [src/Lumeo.DataGrid/UI/DataGrid/DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo.DataGrid/UI/DataGrid/DataGrid.razor)
- Methode/Komponente: `HandleGlobalSearch` + `ScheduleAutoSave`
- Warum vermutlich Bug: Der Debounce-Pfad startet eine async Request-Kette ueber `InvokeAsync(...)`, aber der Rueckgabewert wird nicht beobachtet. Wenn der User waehrend des Debounce-Intervalls navigiert oder der Renderer schon im Dispose ist, ist dieser Pfad der wahrscheinlichste Ort fuer einen unobserved Fehler.
- Wie man ihn reproduzieren koennte: im Server-Mode schnell in das Suchfeld tippen und direkt die Seite wechseln oder das Parent-Layout neu rendern, bevor der Debounce feuert.
- Wie man ihn beheben koennte: `DebounceSearch` auf `Func<Task>` umstellen oder den bereits vorhandenen `DelayedDispatch`-Ansatz nutzen, damit die async Kette explizit und kontrollierbar bleibt.

- Datei: [docs/Lumeo.Docs/Shared/OnThisPage.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Shared/OnThisPage.razor)
- Methode/Komponente: `OnLocationChanged`
- Warum vermutlich Bug: `async void` plus fehlendes Cancellation-Handling ist in einer routengetriebenen Komponente eine typische Quelle fuer nachlaufende, unkoordinierte Workflows. Hier ist das Risiko vor allem unnötige Wiederholungsarbeit und ein schiefer Lifecycle, nicht ein sofort sichtbarer Crash.
- Wie man ihn reproduzieren koennte: schnell zwischen Docs-Seiten navigieren, waehrend die TOC-Komponente noch im Retry-Fenster ist.
- Wie man ihn beheben koennte: `LocationChanged` mit einer cancelbaren Task-Kette kapseln, `async void` vermeiden und bei `DisposeAsync` laufende Rescans abbrechen.

## 5. Architekturrisiken

- Das `DataGrid` bleibt trotz der Verbesserungen ein sehr grosses Feature-Gebilde. Es funktioniert, aber der weitere Ausbau wird leichter, wenn Request-Handling, Layout-Persistenz, Gruppierung und Toolbar-/Fullscreen-UI noch staerker entkoppelt werden.
- Die Docs-Seite ist inzwischen mehr als nur Demo-Content. Mit Prerender, OG-Card-Generierung und einer eigenen Cloudflare-Release-Route entsteht ein zweites Produkt-Subsystem. Das ist gut fuer die Qualitaet, aber es erhoeht die Wartungslast, wenn CI, Sitemap, Docs-Layout und JS-Helpers auseinanderlaufen.
- Die interop-intensiven Komponenten sind besser als frueher, aber die Regel "nicht direkt `IJSRuntime` in Komponenten" braucht weiter konsequente Durchsetzung. Sonst entstehen wieder lokale Sonderwege, die sich im Lifecycle unterschiedlich verhalten.

## 6. Test- und Qualitaetsbewertung

- Gut an den Tests: die Suite ist gross, gruen und inzwischen an den wesentlichen Services konkreter geworden. Die `StrictInteropTests` sind ein echter Gewinn, weil sie exakte JS-Invocationen pruefen statt nur Stillstand.
- Kritisch unzureichend abgesichert: JSInterop ist im Standard-Setup weiterhin loose, und die risikoreichen Pfade `OnThisPage`, Debounce/Dispose im Docs-Layout und echte Browser-Verhalten in der Prerender-Kette sind kaum end-to-end abgesichert.
- CI/CD: deutlich besser als frueher. Der Haupt-Workflow baut Library, Satelliten, Docs-App, Tests und Vulnerability-Check; der Deploy-Workflow fuehrt ausserdem Prerender und Verify aus.
- Was noch fehlt: ein echter Browser-Smoke-Test gegen die prerenderte Docs-Ausgabe, strengere JSInterop-Checks fuer kritische Komponenten und ein kleiner Satz gezielter Lifecycle-Tests fuer Docs-Komponenten mit Timern und Navigation.

## 7. Top-10 Massnahmen

- Problem: `OnThisPage` von `async void` wegziehen
- Nutzen: sauberer Lifecycle, weniger Nachlauf nach Navigation, klarere Fehlerbehandlung
- Aufwand: S

- Problem: DataGrid-Debounce auf `Func<Task>` oder `DelayedDispatch` umstellen
- Nutzen: kein Fire-and-forget im Request-Pfad, bessere Diagnose und weniger Dispose-Risiko
- Aufwand: S

- Problem: Strict JSInterop auf mehr Komponenten ausweiten
- Nutzen: echte Vertragspruefung fuer den interop-lastigen Kern
- Aufwand: M

- Problem: Browser-Smoke-Test fuer prerenderte Docs ergaenzen
- Nutzen: validiert die neue Release-Pipeline dort, wo HTML wirklich ausgeliefert wird
- Aufwand: M

- Problem: Fehlerpfade in Docs/Layout sichtbarer machen
- Nutzen: einfachere Diagnose fuer Shell- und Navigation-Regressionen
- Aufwand: S

- Problem: DataGrid weiter in klarere Subsysteme zerlegen
- Nutzen: weniger Komplexitaet pro Datei, bessere Testbarkeit
- Aufwand: L

- Problem: Timer-/Navigation-Tests fuer Docs-Komponenten einfuehren
- Nutzen: schliesst die letzten Lifecycle-Luecken
- Aufwand: M

- Problem: Persistenz- und Debounce-APIs vereinheitlichen
- Nutzen: weniger Sonderfaelle zwischen AutoSave, Search und Layout-Speicherung
- Aufwand: M

- Problem: Release-Doku fuer Prerender/Cloudflare schliessen
- Nutzen: weniger Wissen im Kopf einzelner Entwickler
- Aufwand: S

- Problem: Neue Pipeline-Artefakte in CI regelmaessig validieren
- Nutzen: fruehe Erkennung von Release-Drift
- Aufwand: S

## 8. Scorecard

- Architektur: 7/10
- Codequalitaet: 8/10
- Konsistenz: 8/10
- Wartbarkeit: 7/10
- Testqualitaet: 8/10
- Produktionsreife: 8/10
- Entwicklererlebnis: 9/10
- Zukunftsfaehigkeit: 7/10

## 9. Finale Einschaetzung

- Wuerde ich dieses Repo heute in Produktion einsetzen? Ja, fuer reale Nutzung und auch fuer eine 1.0-nahe Freigabe.
- Unter welchen Bedingungen ja/nein? Ja, wenn die beiden Async-Randpfade aus Abschnitt 4 nicht ignoriert werden und das Team akzeptiert, dass JSInterop im Standard-Setup nicht maximal streng ist. Nein nur dann, wenn ihr eine 1.0 ohne jegliche Rest-Hygiene-Luecken erwartet.
- Was muesste vor einer 1.0 unbedingt noch passieren? Ich wuerde `OnThisPage` und den DataGrid-Debounce sauber auf echte Task-/Cancellation-Semantik bringen, die interop-nahe Testabdeckung weiter ausbauen und einmal einen Browser-Smoke-Test gegen die prerenderte Docs-Ausgabe laufen lassen. Danach ist das hier aus meiner Sicht in einem sehr guten Zustand fuer 1.0.
