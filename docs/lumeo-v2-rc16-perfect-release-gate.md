# Lumeo v2-rc.16 Perfect Release Gate

Stand: 2026-04-30

Dieses Dokument ersetzt die frueheren v1-Readiness-Dokumente als Entscheidungsgrundlage. Der Massstab ist jetzt nicht mehr "gut genug fuer 1.0", sondern: `v2-rc.16` muss als vollwertiger Release Candidate ohne bekannte Komponentenfehler, ohne versteckte halbfertige Features und ohne Docs-Luecken behandelt werden.

## Aktueller verifizierter Stand

- `dotnet build Lumeo.slnx -c Release --no-restore`: gruen, `0` Warnings, `0` Errors.
- `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build`: `1744` passed, `1` skipped.
- `dotnet test tests/Lumeo.Docs.Tests/Lumeo.Docs.Tests.csproj -c Release --no-build`: `20` passed.
- `dotnet test tests/Lumeo.RegistryGen.Tests/Lumeo.RegistryGen.Tests.csproj -c Release --no-build`: `16` passed.
- Registry-Generierung meldet: `173` items, davon `131` components, `16` patterns, `8` blocks, `18` guides.
- `src/Lumeo/registry/registry.json` steht auf Version `2.0.0`.

Wichtig: Gruene Tests bedeuten hier nur "keine bekannte automatische Regression". Fuer das Ziel "alle Komponenten perfekt" reicht das allein nicht. Fuer `v2-rc.16` braucht es zusaetzlich ein manuelles und automatisiertes Vollstaendigkeits-Gate.

## Release-Definition fuer v2

Ein Component ist erst v2-ready, wenn alle Punkte gelten:

- Er rendert stabil in Default-, Disabled-, Loading-, Empty-, Error- und Edge-State, sofern diese Zustaende fachlich existieren.
- Er hat keine versteckten Features, die im Code existieren, aber nicht dokumentiert, getestet oder visuell erreichbar sind.
- Er hat keine "Missing Features", also keine offensichtlichen API-Luecken im Vergleich zum eigenen Component-Typ. Beispiele: Input ohne `Disabled`, Overlay ohne Cleanup, Data-Komponente ohne Empty/Error-State, interaktive Komponente ohne Keyboard-/ARIA-Verhalten.
- Alle oeffentlichen Parameter, Events und ChildContent-Slots sind in der Docs-Seite erklaert oder bewusst als intern/nicht relevant ausgeschlossen.
- Alle interaktiven Features haben mindestens einen Test oder einen dokumentierten manuellen Browser-Check.
- JSInterop, Timer, Event-Handler und Dispose-Pfade sind pro Instanz sauber und erzeugen keinen Cross-Talk.
- Die Docs-Seite zeigt echte, funktionierende Beispiele statt nur dekorative Demos.

## Nicht verhandelbare V2-Grossschritte

### 1. Component-Contract-Audit fuer alle 131 Components

Problem:
Die Library ist jetzt gross genug, dass einzelne Komponenten leicht "funktionieren", aber trotzdem versteckte Luecken haben: undokumentierte Parameter, Events ohne Beispiel, Features ohne Test, Docs-Demos ohne echte Interaktion oder Edge-States.

Was gemacht werden muss:

- Fuer jeden Registry-Component eine Contract-Zeile erstellen: Component, Source-Dateien, Docs-Seite, Tests, JSInterop, States, bekannte Luecken.
- Parameter und Events aus dem Source gegen die Docs-Seite vergleichen.
- Jede Component-Seite pruefen auf: Basic Usage, Variants, Disabled/ReadOnly, Loading/Empty/Error, Accessibility, API table, Edge cases.
- Jede interaktive Component pruefen auf: Keyboard, Focus, ARIA, ClickOutside, Escape, Cleanup, Multi-Instance-Verhalten.
- Jede datengetriebene Component pruefen auf: empty data, null data, large data, async loading, error display, localization/culture.
- Jede Overlay-/Floating-Component pruefen auf: scroll lock, focus trap, outside click, unposition cleanup, z-index, mobile behavior.
- Jede JSInterop-Component pruefen auf: register/unregister symmetry, per-instance dispatch, `JSDisconnectedException` cleanup, no global event leak.

Ergebnis:
Eine Datei wie `docs/lumeo-v2-component-contract-matrix.md`, in der alle `131` Components sichtbar abgehakt werden. Ohne diese Matrix ist "alle Components perfekt" nicht belegbar.

Ausmass:
Gross. Das ist kein kleiner Fix, sondern ein kompletter Vollstaendigkeitsdurchlauf ueber die Library.

### 2. Docs-Completeness-Audit fuer alle Component-, Pattern-, Block- und Guide-Seiten

Problem:
Die Docs sind fuer v2 Teil des Produkts. Wenn ein Feature nur im Code existiert, aber nicht in der Docs-Seite vorkommt, ist es praktisch ein Hidden Feature. Wenn die Docs ein Verhalten versprechen, das die Component nicht sauber kann, ist es ein Produktfehler.

Was gemacht werden muss:

- Alle Docs-Routen aus Registry/Sitemap gegen echte Razor-Seiten vergleichen.
- Jede Component-Seite muss eine klare API-Sektion haben.
- Jede Component-Seite muss mindestens ein realistisches Beispiel haben.
- Komplexe Komponenten brauchen mehrere Szenarien: DataGrid, Chart, Scheduler, Gantt, RichTextEditor, FileUpload, Form, Overlay, Command, Combobox, Select, TreeSelect, Transfer, PickList.
- Pattern- und Block-Seiten duerfen keine Attrappen sein, die wie fertige Features aussehen, aber keine echte Interaktion haben.
- Alle Code-Beispiele muessen mit der aktuellen API kompilierbar sein.
- Search, Navigation, Sidebar, OnThisPage, Preview Cards, OG Cards und Prerender muessen fuer den Docs-Release als echte Runtime-Flaeche gelten.

Ergebnis:
Eine Datei wie `docs/lumeo-v2-docs-completeness-matrix.md`, die jede Docs-Seite mit Status `OK`, `Needs Fix`, `Missing API`, `Missing Example`, `Behavior Drift` bewertet.

Ausmass:
Gross. Das ist ein zweiter Vollstaendigkeitsdurchlauf, diesmal ueber die Produkt-Dokumentation.

### 3. Browser-/Interop-Hardening statt nur bUnit

Problem:
bUnit ist stark fuer Render- und Component-Tests, aber viele v2-Risiken liegen im Browser: Focus, Resize, Overlay-Positioning, scroll, pointer events, keyboard shortcuts, charts, scheduler/gantt/editor JS modules, prerendered docs.

Was gemacht werden muss:

- Mindestens eine Browser-Smoke-Suite fuer Docs starten.
- Stichproben muessen echte Interaktion ausfuehren: Dialog oeffnen/schliessen, Select/Combobox bedienen, DataGrid sortieren/suchen, Chart rendern, Sidebar/OnThisPage navigieren.
- JSInterop-Tests fuer kritische Register/Unregister-Paare erweitern.
- `JSRuntimeMode.Loose` darf im Basis-Testsetup bleiben, aber kritische Komponenten brauchen Strict-/Verify-Tests.
- Prerender-Ausgabe muss gegen Shell-only HTML, fehlende Titles, fehlende OG Tags und kaputte Routes geprueft werden.

Ergebnis:
Eine kleine, aber harte Browser-Suite. Nicht viele Tests, aber die wichtigsten Produktpfade muessen im echten Browser einmal laufen.

Ausmass:
Mittel bis gross. Technisch kleiner als die Matrix-Arbeit, aber sehr wichtig fuer Vertrauen.

## Aktuelle technische Restpunkte

### DataGrid Debounce

Dateien:

- `src/Lumeo.DataGrid/UI/DataGrid/DataGrid.razor`
- `src/Lumeo.DataGrid/UI/DataGrid/DataGridServerService.cs`

Befund:
Der Debounce-Pfad arbeitet noch mit einem synchronen `Action`, obwohl der Aufrufer async Arbeit startet. Das ist kein aktuell bewiesener Crash, aber fuer v2 sollte dieser Pfad sauber auf `Func<Task>` oder den vorhandenen `DelayedDispatch`-Stil umgestellt werden.

V2-Bewertung:
Sollte vor `v2.0.0` erledigt werden. Fuer `rc.16` nur akzeptabel, wenn bewusst dokumentiert und durch Tests abgesichert.

### OnThisPage Navigation Handler

Datei:

- `docs/Lumeo.Docs/Shared/OnThisPage.razor`

Befund:
`OnLocationChanged` ist noch `async void`. Fuer eine globale Docs-Komponente mit Retry-Scan ist das ein unschoener Lifecycle-Pfad.

V2-Bewertung:
Sollte vor `v2.0.0` erledigt werden, weil die Docs-Seite Teil des Produkts ist.

### Ein uebersprungener Test

Datei:

- `tests/Lumeo.Tests/Editor/WordImporterTests.cs`

Befund:
`ToHtmlAsync_RealDocument_ContainsHeadingsAndParagraphs` ist skipped.

V2-Bewertung:
Ein Skip ist fuer RC nicht automatisch ein Blocker. Fuer einen "perfekten" v2-Anspruch muss aber begruendet sein, warum er skipped ist, oder der Test muss wieder aktiviert werden.

## Was nicht mehr als offener Blocker zaehlt

Diese Punkte wirkten in den frueheren Reviews kritisch, sind im aktuellen Stand aber nicht mehr als offene Blocker sichtbar:

- Interop-Cross-Talk in Swipe/Resize/BackToTop/Otp/ColumnResize.
- ToastSwipe Register/Unregister-Key-Mismatch.
- `positionFixed` ohne Cleanup.
- ColorPicker-Lightness-Regler.
- Cascader-Nullability-Vertrag.
- Chart-Reinit nach Themewechsel inklusive Group-Rejoin.
- DataGrid-ServerRequest Loading-Race.
- KeyboardShortcut sync/async Dispose-Inkonsistenz.
- Alert AutoDismiss-Cleanup.
- Tooltip/Combobox/HoverCard timerbasierte Threading-Probleme.

## V2 Go/No-Go-Regel

Go fuer `v2-rc.16`:

- Build gruen.
- Tests gruen.
- Keine bekannten harten Runtime-Bugs.
- Die zwei Restpunkte `DataGrid Debounce` und `OnThisPage async void` sind als RC-Risiken akzeptiert oder direkt gefixt.
- Docs deployen, prerendern und verifizieren erfolgreich.

No-Go fuer `v2.0.0 final`:

- Wenn es keine Component-Contract-Matrix fuer alle `131` Components gibt.
- Wenn Docs-Seiten Features zeigen, die nicht funktionieren.
- Wenn oeffentliche Parameter/Events in den Docs fehlen.
- Wenn kritische JSInterop-Komponenten nur durch loose bUnit-Tests abgesichert sind.
- Wenn der skipped Editor-Test nicht begruendet oder entfernt/fixed ist.
- Wenn neue Component-Seiten ohne API-, State- und Accessibility-Abdeckung hinzukommen.

## Empfohlene Arbeitsreihenfolge

1. `DataGrid Debounce` und `OnThisPage async void` fixen.
2. Skipped Editor-Test klaeren.
3. Component-Contract-Matrix aus Registry generieren oder manuell anlegen.
4. Docs-Completeness-Matrix fuer alle Component-Seiten anlegen.
5. Browser-Smoke-Suite fuer die wichtigsten v2-Flows einfuehren.
6. Danach erst `v2.0.0 final` freigeben.

## Finale Einschaetzung

Lumeo ist aktuell nicht mehr in einem "grosses Refactor vor Release" Zustand. Der Code wirkt stabiler, die Tests sind deutlich gewachsen, und die frueheren harten Bugs sind weitgehend raus.

Aber fuer deinen jetzigen Anspruch ist die richtige Aussage:

`v2-rc.16` kann ein guter Release Candidate sein. `v2.0.0 final` sollte erst kommen, wenn die Vollstaendigkeit aller 131 Components und aller Docs-Seiten belegbar ist.

Perfekt heisst hier nicht "ich sehe gerade keinen Bug". Perfekt heisst: jede Component hat einen sichtbaren Vertrag, jede Docs-Seite beweist diesen Vertrag, und jeder kritische Browser-/Interop-Pfad hat mindestens einen echten Check.
