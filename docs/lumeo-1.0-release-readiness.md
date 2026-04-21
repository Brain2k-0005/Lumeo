# Lumeo 1.0 Release Readiness

## Executive Summary

Lumeo ist im aktuellen Stand produktionsnah und technisch deutlich stabiler als zu Beginn der Audits. `dotnet build` ist grün und die Tests laufen mit `1303/1303`.

Ich sehe aktuell keinen offensichtlichen harten Blocker, der die gesamte Library für eine 1.0 grundsätzlich disqualifiziert. Es gibt aber noch Punkte, die ich vor einer echten 1.0-Freigabe absichern würde, weil sie vor allem Diagnose, Testschärfe und Randfall-Robustheit betreffen.

Die stärksten Bereiche sind die modularisierte Interop-Schicht, die klarere Trennung im DataGrid und die inzwischen gute Testabdeckung auf Service-Ebene. Die Rest-Risiken liegen eher in „Release-Hygiene“ als in einer einzelnen großen Funktionslücke.

## Was bereits gut genug für 1.0 wirkt

- Die Interop-Schicht ist nicht mehr monolithisch. `ComponentInteropService` delegiert an spezialisierte Adapter wie [SwipeInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/SwipeInterop.cs#L5), [FloatingPositionInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/FloatingPositionInterop.cs#L5) und [UtilityInterop.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/Interop/UtilityInterop.cs#L5).
- Der DataGrid-Code ist sinnvoller geschnitten als früher. Persistenz und Serverdaten liegen in [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L5) und [DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridServerService.cs#L5).
- Kritische Cleanup-Pfade sind deutlich besser geworden. Viele Overlay- und Positioning-Komponenten rufen `UnpositionFixed(...)` sauber auf, z. B. [SelectContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Select/SelectContent.razor#L110) und [PopoverContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Popover/PopoverContent.razor#L81).
- Die Interop-Tests sind präziser geworden. [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L1) prüft konkrete JS-Invocations statt nur „kein Throw“.

## Must Fix Before 1.0

### 1. JSInterop-Testmodus härten

- Datei: [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L16), [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs#L30)
- Problem: Der Standard-Testkontext und auch die gezielten Interop-Tests laufen im `JSRuntimeMode.Loose`.
- Warum das relevant ist: Das Setup ist bequem, aber es maskiert fehlende oder zusätzliche JS-Aufrufe leichter, als ich es für eine 1.0-Library ideal finde.
- Was ich vor 1.0 verlangen würde: Für kritische Adapter mindestens einen strengeren Test-Cluster oder explizite Negative-Tests für fehlende JS-Calls.

### 2. DataGrid-Autosave-Callback als asynchronen Fire-and-Forget-Pfad entschärfen

- Datei: [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L82), [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L979)
- Problem: `ScheduleAutoSave(Action persistCallback)` triggert nur einen `Action`. In [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L983) wird `InvokeAsync(() => PersistLayoutAsync(gen + 1))` übergeben, aber nicht awaited.
- Warum das relevant ist: Die Generation-Gating-Logik entschärft stale writes, aber Fehler im Persist-Pfad bleiben als fire-and-forget-Risiko bestehen.
- Was ich vor 1.0 verlangen würde: Den Pfad auf `Func<Task>` heben oder zumindest einen Test ergänzen, der späte Persist-/Exception-Fälle abdeckt.

### 3. Logging-only Fehlerpfade sichtbar machen

- Dateien: [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L37), [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor#L345), [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L58), [CustomizerSidebar.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Shared/CustomizerSidebar.razor#L644)
- Problem: Mehrere `catch (Exception ex)`-Blöcke schreiben nur nach `Console.Error` und fahren fort.
- Warum das relevant ist: Für Konsumenten ist dann nicht klar, dass Persistenz, Chart-Init oder Theme-Laden fehlgeschlagen ist.
- Was ich vor 1.0 verlangen würde: Mindestens für DataGrid und Chart sichtbare Fehlerzustände oder opt-in Fehler-Callbacks.

### 4. Chart-Reinit bei Themewechsel regressionssicher absichern

- Datei: [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L89)
- Problem: Der Theme-Wechsel-Pfad initialisiert das Chart neu und ruft danach `RegisterChartEventsAsync()` auf. Das ist richtig, aber ein echtes Regression-Sicherungsproblem bleibt, weil dieser Pfad komplex ist.
- Warum das relevant ist: Laufzeit-Themewechsel sind selten, aber genau die Art Pfad, die ohne gezielten Test wieder kaputtgeht.
- Was ich vor 1.0 verlangen würde: Einen kleinen Smoke-Test, der Themewechsel plus Event- und Group-Verhalten absichert.

## Should Fix Before 1.0

### Timer-gestützte UI-Pfade vereinheitlichen

- Betroffene Stellen: [Tooltip.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Tooltip/Tooltip.razor#L32), [HoverCard.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/HoverCard/HoverCard.razor#L38), [NavigationMenuItem.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/NavigationMenu/NavigationMenuItem.razor#L38), [MegaMenuItem.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/MegaMenu/MegaMenuItem.razor#L97), [Combobox.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Combobox/Combobox.razor#L116)
- Warum: Die Komponenten sind besser geworden, aber sie folgen nicht überall demselben Muster für Timer, Cancellation und async Dispatch.
- Empfehlung: Einen kleinen gemeinsamen Helper oder ein konsistentes Pattern für Debounce/Hover/Close-Callbacks einführen.

### DataGrid- und Chart-Randfälle mit echten Verhaltens-Tests absichern

- Betroffene Stellen: [DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridServerService.cs#L13), [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs#L20), [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor#L89)
- Warum: Die Suite ist groß, aber die wirklich risikoreichen Grenzfälle brauchen noch gezielte Abdeckung.
- Empfehlung: Ein paar kleine Tests für Autosave, Cancellation und Themewechsel bringen hier mehr als weitere Render-Snapshots.

### Test-Dependency-Stack aufräumen

- Datei: [Lumeo.Tests.csproj](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Lumeo.Tests.csproj#L12)
- Problem: Die Tests hängen noch an `bunit` `2.0.33-preview`.
- Warum das relevant ist: Nicht produktionskritisch, aber für 1.0 würde ich keine Preview-Version im Standard-Testpfad lassen, wenn es eine stabile Alternative gibt.
- Empfehlung: Auf eine stabile Version wechseln oder bewusst dokumentieren, warum der Preview-Stand notwendig ist.

## Release Checklist

- `dotnet build src/Lumeo/Lumeo.csproj -c Release`
- `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build`
- Ein paar Smoke-Tests für DataGrid, Chart und Interop im Browserkontext
- Prüfen, ob alle kritischen JS-/Dispose-Pfade wirklich getestet sind
- Release Notes und Changelog erstellen
- Versionierung und Paket-Metadaten prüfen
- Dependency-/Vulnerability-Scan ausführen
- Docs-Build gegen die neue Version laufen lassen

## Finale Bewertung

Lumeo ist aus meiner Sicht produktionsnah und nahe an 1.0, aber noch nicht „perfekt“ im Sinne einer freigabereifen Library ohne weitere Release-Härtung. Ich würde die 1.0-Freigabe mit den oben genannten Must-Fix- und Should-Fix-Punkten verknüpfen.

Wenn du es ganz streng formulierst: Die Library ist shipbar, aber für eine 1.0 will ich noch mehr Testschärfe, sichtbarere Fehlerpfade und ein aufgeräumtes Release-Setup.
