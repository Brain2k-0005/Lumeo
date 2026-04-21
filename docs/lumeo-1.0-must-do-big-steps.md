# Lumeo 1.0 - Unbedingt notwendige große Schritte

Dieser Plan ist absichtlich nicht granular. Er enthält nur die großen Schritte, die aus meiner Sicht vor einer echten 1.0-Freigabe zwingend erledigt werden müssen. Alles andere ist nachrangig.

## Schritt 1: Kritische Laufzeitpfade härten

**Ausmaß:** groß  
**Priorität:** sehr hoch  
**Ziel:** Alle Pfade, die bei echten Nutzern sichtbar brechen können, müssen robust, testbar und deterministic sein.

### Was unbedingt gemacht werden muss

- Den Chart-Refresh nach Theme-Wechsel vollständig absichern.
- Den DataGrid-Autosave- und Request-Pfad race-sicher machen.
- Timer-basierte UI-Pfade auf ein einheitliches, sauberes Dispatch-/Cancellation-Modell bringen.
- Alle kritischen JSInterop-Pfade so testen, dass fehlende oder falsche Calls nicht mehr durchrutschen.

### Konkrete betroffene Bereiche

- [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor)
- [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor)
- [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs)
- [DataGridServerService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridServerService.cs)
- [Tooltip.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Tooltip/Tooltip.razor)
- [HoverCard.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/HoverCard/HoverCard.razor)
- [NavigationMenuItem.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/NavigationMenu/NavigationMenuItem.razor)
- [MegaMenuItem.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/MegaMenu/MegaMenuItem.razor)
- [Combobox.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Combobox/Combobox.razor)
- [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs)
- [StrictInteropTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/StrictInteropTests.cs)

### Warum das zwingend ist

- Diese Pfade betreffen echte Benutzerinteraktionen, nicht nur interne Struktur.
- Wenn hier Fehler bleiben, sind sie für Konsumenten sichtbar, auch wenn die Suite grün ist.
- Diese Stellen erzeugen genau die Art von Problemen, die erst im Feld auffallen: falsche Ladezustände, verlorene Synchronisation, spät feu­ernde Timer, maskierte JS-Aufrufe.

### Exit-Kriterien

- Chart-Themewechsel ist mit einem Test abgesichert.
- DataGrid-Request und Autosave sind gegen Überholen und Spätläufer abgesichert.
- Timer-gestützte UI-Komponenten verwenden überall dasselbe sichere Muster.
- Kritische JSInterop-Tests laufen nicht nur „Loose“, sondern erkennen fehlende oder falsche Invocations zuverlässig.

## Schritt 2: Release-Hygiene und Diagnose auf 1.0-Niveau bringen

**Ausmaß:** groß  
**Priorität:** hoch  
**Ziel:** Die Library muss nicht nur funktionieren, sondern sich auch wie ein reifes Produkt verhalten, wenn etwas schiefgeht.

### Was unbedingt gemacht werden muss

- Fehlerpfade in DataGrid, Chart und Docs so sichtbar machen, dass Konsumenten sie erkennen können.
- Preview-Dependencies und riskante Test-/Build-Artefakte für die 1.0-Phase bereinigen oder bewusst dokumentieren.
- Release- und Verpackungsworkflow klarziehen: Versionierung, Changelog, Docs-Build, Dependency-Checks.

### Konkrete betroffene Bereiche

- [DataGridLayoutService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridLayoutService.cs)
- [DataGridToolbar.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridToolbar.razor)
- [Chart.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Chart/Chart.razor)
- [CustomizerSidebar.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Shared/CustomizerSidebar.razor)
- [MainLayout.razor](/C:/Users/bemi/RiderProjects/Lumeo/docs/Lumeo.Docs/Layout/MainLayout.razor)
- [Lumeo.Tests.csproj](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Lumeo.Tests.csproj)

### Warum das zwingend ist

- Eine 1.0 darf nicht nur „meistens funktionieren“, sondern muss im Fehlerfall nachvollziehbar sein.
- Wenn Fehlkonfigurationen oder Persistenzprobleme nur in der Konsole landen, ist die Diagnose für Anwender zu schwach.
- Preview-basierte Testabhängigkeiten und unklare Release-Schritte machen eine 1.0 unnötig fragil.

### Exit-Kriterien

- Fehler werden für Nutzer sichtbar oder eindeutig über API/Callbacks zurückgemeldet.
- Der Test- und Release-Stack ist ohne unnötige Preview-Abhängigkeiten oder unklare Sonderfälle nachvollziehbar.
- Docs, Build und Tests sind als Teil des Release-Prozesses fest verankert.

## Was ich vor 1.0 nicht mehr als Pflicht sehe

- Weitere kosmetische Refactorings ohne Laufzeitnutzen.
- Kleinere Stiländerungen in bereits funktionierenden Komponenten.
- Zusätzliche Komponenten-Features, die nicht direkt Release-Risiko reduzieren.

## Klare Aussage

Wenn du Lumeo als 1.0 freigeben willst, reichen aus meiner Sicht diese zwei großen Schritte:

1. Laufzeitpfade härten.
2. Release-Hygiene und Diagnose auf Produktniveau bringen.

Alles andere ist nachgeordnet.
