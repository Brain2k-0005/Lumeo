# Lumeo 1.0 Release Readiness - Current State

Stand: nach den letzten Codeänderungen und erneuter Prüfung.

## Verifikation

- `dotnet build src/Lumeo/Lumeo.csproj -c Release` ist erfolgreich.
- `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build` ist erfolgreich mit `1316/1316`.

## Was jetzt sauber wirkt

- Der frühere Chart-Theme-Reinit-Risiko-Pfad ist deutlich besser abgesichert. `Chart.razor` ruft nach einem Themewechsel `RegisterChartEventsAsync()` auf, und diese Methode verbindet auch `Group` erneut.
- Der DataGrid-Autosave-Pfad ist generation-aware und nicht mehr der alte ungeschützte Fire-and-Forget-Ansatz. `DataGridLayoutService` prüft die Generation vor dem Persistieren.
- Der Timer-/Debounce-Pfad ist in einen gemeinsamen Helper ausgelagert. `DelayedDispatch` zentralisiert das Scheduling und wird in `Tooltip`, `HoverCard`, `NavigationMenuItem`, `MegaMenuItem` und `Combobox` verwendet.
- Der KeyboardShortcut-Service ist jetzt API-konsistent auf einen async Cleanup-Pfad vereinfacht.

## Was vor 1.0 noch zwingend sauber sein sollte

### 1. Interop-Testschärfe erhöhen

- Datei: [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L16), [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L30)
- Zustand: Der Test-Stack nutzt weiterhin `JSRuntimeMode.Loose`.
- Risiko: JS-Aufrufe können leichter unbemerkt fehlen oder abweichen.
- Was ich vor 1.0 haben will: Kritische Adapter mit härteren Assertions oder eigenen Negativtests.

### 2. Release-Fehler sichtbarer machen

- Dateien: [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L39), [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L346), [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L59), [CustomizerSidebar.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Shared/CustomizerSidebar.razor#L644)
- Zustand: Mehrere Fehlerpfade loggen nur nach `Console.Error`.
- Risiko: Für Konsumenten sind Fehler dann schwerer zu erkennen und zu behandeln.
- Was ich vor 1.0 haben will: Mindestens für DataGrid und Chart sichtbare Fehlerzustände oder optionale Error-Callbacks.

### 3. Preview-Testdependency prüfen

- Datei: [Lumeo.Tests.csproj](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Lumeo.Tests.csproj#L12)
- Zustand: `bunit` steht noch auf `2.0.33-preview`.
- Risiko: Nicht produktionskritisch, aber für eine 1.0 eher unsauber.
- Was ich vor 1.0 haben will: Entweder eine stabile Version oder eine bewusste Dokumentation, warum das Preview-Paket notwendig bleibt.

## Was ich aktuell nicht mehr als Blocker sehe

- Chart-Themewechsel ist kein klarer Funktionsblocker mehr.
- DataGrid-Autosave ist nicht mehr der alte Race-Fehler.
- Die zentralen Timer-/Hover-/Debounce-Pfade sind konsistenter umgesetzt.
- KeyboardShortcut-Cleanup ist nicht mehr inkonsistent.

## Fazit

Ich würde den aktuellen Stand nicht mehr als „major bug blocked“ einstufen. Die Library ist technisch näher an 1.0 als zuvor.

Für eine saubere 1.0-Freigabe bleiben aber noch drei echte Release-Hygiene-Themen offen:

1. Interop-Tests härten.
2. Fehler sichtbarer machen.
3. Preview-Testdependency prüfen.

Wenn du willst, kann ich daraus als Nächstes eine knappe Go/No-Go-Liste machen, also nur `ship`, `fix before ship`, `optional`. 
