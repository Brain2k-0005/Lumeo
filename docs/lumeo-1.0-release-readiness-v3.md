# Lumeo 1.0 Release Readiness - Verified Current State

Stand: nach erneuter Prüfung des aktuellen Source Codes.

## Verifikation

- `dotnet build src/Lumeo/Lumeo.csproj -c Release` ist erfolgreich.
- `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build` ist erfolgreich mit `1316/1316`.

## Was im aktuellen Stand erledigt wirkt

- Der frühere Chart-Theme-Reinit-Pfad ist jetzt sauberer. Nach einem Themewechsel werden die Chart-Events erneut registriert, und `Group` wird im Reinit-Pfad nicht verloren.
- DataGrid-Autosave ist generation-aware und nicht mehr der alte ungeschützte Fire-and-Forget-Fall.
- Die Laufzeit-Debounce-/Hover-/Delay-Logik ist in `DelayedDispatch` zentralisiert und wird von Tooltip, HoverCard, NavigationMenu, MegaMenu und Combobox konsistenter genutzt.
- Der KeyboardShortcut-Service ist auf einen einheitlichen async Dispose-Pfad vereinfacht.

## Was ich aktuell nicht mehr als Blocker sehe

- Kein aktueller harter Funktionsblocker im Chart-Themewechsel.
- Kein aktueller harter Race-Bug im DataGrid-Autosave-Pfad.
- Kein aktueller Cross-Talk- oder Cleanup-Blocker in den zentralen Interop-Pfaden.
- Kein aktueller API-Leak im KeyboardShortcut-Cleanup.

## Was vor 1.0 noch sauberer sein sollte

### 1. JSInterop-Testschärfe erhöhen

- Datei: [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L16), [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L30)
- Status: Der Test-Stack nutzt weiterhin `JSRuntimeMode.Loose`.
- Risiko: Fehlende oder falsche JS-Aufrufe sind leichter zu übersehen als in einem strengeren Setup.
- Empfehlung: Für kritische Adapter mindestens ein härteres Test-Cluster oder gezielte Negativtests ergänzen.

### 2. Fehlerpfade sichtbarer machen

- Dateien: [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L39), [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L346), [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L70), [CustomizerSidebar.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Shared/CustomizerSidebar.razor#L644)
- Status: Mehrere Fehlerpfade loggen nur nach `Console.Error`.
- Risiko: Für Konsumenten ist nicht immer sichtbar, dass etwas fehlgeschlagen ist.
- Empfehlung: Für DataGrid und Chart mindestens optionale Error-Callbacks oder sichtbare Error-States vorsehen.

### 3. Preview-Testdependency prüfen

- Datei: [Lumeo.Tests.csproj](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Lumeo.Tests.csproj#L12)
- Status: `bunit` ist noch auf `2.0.33-preview`.
- Risiko: Kein Funktionsblocker, aber für eine 1.0 unsauber.
- Empfehlung: Stabilen Stand prüfen oder den Preview-Status bewusst dokumentieren.

## Fazit

Die aktuellen Änderungen haben die früheren harten Blocker weitgehend beseitigt. Ich sehe im aktuellen Stand keine neuen kritischen Defekte, die eine 1.0 grundsätzlich verhindern.

Für eine saubere Freigabe würde ich dennoch die drei Punkte oben als Release-Hygiene behandeln. Wenn diese bewusst akzeptiert oder nachgezogen werden, wirkt Lumeo aus technischer Sicht 1.0-nah und shipbar.
