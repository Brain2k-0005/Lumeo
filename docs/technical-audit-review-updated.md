# Technisches Audit – Aktualisierter Stand

Stand: 2026-04-01

Prüfungen:
- `dotnet build src/Lumeo/Lumeo.csproj -c Release` erfolgreich
- `dotnet test tests/Lumeo.Tests/Lumeo.Tests.csproj -c Release --no-build --verbosity minimal` erfolgreich mit `1126/1126` Tests

## 1. Executive Summary

- Das Repository ist klar weiter als beim letzten Audit. Mehrere der früheren High-Severity-Probleme wurden sauber korrigiert, vor allem instanzspezifisches Event-Routing im Interop-Layer, der `DataGrid`-Cancellation-Pfad und die Overlay-Semantik für `Close` vs. `Cancel`.
- Die Struktur ist weiterhin gut lesbar: `src/Lumeo` enthält die eigentliche Library, `docs/Lumeo.Docs` die Showcase-/Dokumentations-App, `tests/Lumeo.Tests` die bUnit-/Service-Tests. Die Modultrennung auf Repo-Ebene ist sauber.
- Die größte technische Stärke ist nach wie vor die breite, konsistente Component-Oberfläche mit einem brauchbaren Service-Layer für Toasts, Overlays, Theme und Interop.
- Positiv ist auch, dass die CI inzwischen sinnvoller ist: Sie baut Library und Docs-App, testet und prüft zusätzlich auf verwundbare Packages in [.github/workflows/ci.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/ci.yml#L10).
- Die Qualitätslage ist aber noch nicht bugfrei. Es bleiben belastbare Defekte in Cleanup- und Randpfaden, insbesondere rund um `positionFixed`, Toast-Swipe-Unregister, `ColorPicker` und `Cascader`.
- Die Testsuite ist quantitativ stark, qualitativ aber weiter nicht risikoorientiert genug. Viele kritische Pfade sind gar nicht oder nur als “does not throw” abgesichert.
- Das Repo ist heute näher an einer soliden Library als beim ersten Review. Für eine wirklich belastbare `1.0` fehlen aber noch ein paar konkrete Bugfixes und gezielte Defekt-Tests.
- Einschätzung: `solide Library`, mit produktionsnaher Qualität, aber noch nicht vollständig abgesichert.
- Größte 3 Stärken:
- Instanzbezogenes Interop-Routing statt globalem Cross-Talk in [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L153)
- Sauberere CI-Barriere inklusive Docs-Build und Vulnerability-Check in [ci.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/ci.yml#L23)
- Deutlich breitere Testabdeckung als zuvor, aktuell `1126` grüne Tests
- Größte 3 Risiken:
- Cleanup für `positionFixed` ist repo-weit inkonsistent und führt weiter zu globalen Listener-Leaks
- Es gibt noch funktionale Defekte in Randfällen, die durch die Testsuite nicht abgedeckt sind
- Große Zustandsmaschinen wie `DataGrid` bleiben schwer testbar und damit regressionsanfällig

## 2. Was technisch gut ist

- Titel: Interop-Cross-Talk wurde an den kritischen Stellen korrekt beseitigt
- Warum gut: Drawer-, Carousel-, Resize-, OTP- und BackToTop-Callbacks dispatchen jetzt instanzbezogen statt an alle registrierten Handler.
- Betroffene Dateien/Module: [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L153), [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L190)
- Auswirkung auf Qualität/Wartbarkeit: Das beseitigt echte Mehrinstanz-Bugs und macht das Verhalten für Library-Konsumenten deutlich vorhersagbarer.

- Titel: `DataGrid` setzt Loading-State bei gecancelten Requests jetzt robust zurück
- Warum gut: Der frühere Fehlerpfad über `OperationCanceledException` endet nicht mehr vor dem Zurücksetzen des Zustands.
- Betroffene Dateien/Module: [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L1085)
- Auswirkung auf Qualität/Wartbarkeit: Debounced Suchen und schnelle Request-Folgen können das Grid nicht mehr so leicht in einem falschen Loading-State zurücklassen.

- Titel: Overlay-API unterscheidet jetzt semantisch sauber zwischen `Close` und `Cancel`
- Warum gut: `OverlayService.OnClose` transportiert jetzt explizit `cancelled`, statt `null` implizit als Cancel zu interpretieren.
- Betroffene Dateien/Module: [OverlayService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/OverlayService.cs#L7), [OverlayProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Overlay/OverlayProvider.razor#L107)
- Auswirkung auf Qualität/Wartbarkeit: Der API-Vertrag ist für Konsumenten deutlich weniger mehrdeutig.

- Titel: Export-Semantik ist ehrlicher als zuvor
- Warum gut: `ExportToExcelAsync` gibt nicht mehr vor, echte XLSX-Dateien zu erzeugen, sondern liefert CSV.
- Betroffene Dateien/Module: [DataGridExportService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGridExportService.cs#L30)
- Auswirkung auf Qualität/Wartbarkeit: Das verhindert formell ungültige Exportdateien und falsche Erwartungshaltungen.

- Titel: Build-/CI-Qualität ist besser als beim ersten Audit
- Warum gut: Build läuft mit `-warnaserror`, Docs-App wird mitgebaut und Package-Vulnerabilities werden geprüft.
- Betroffene Dateien/Module: [ci.yml](/C:/Users/bemi/RiderProjects/Lumeo/.github/workflows/ci.yml#L23)
- Auswirkung auf Qualität/Wartbarkeit: Die Pipeline ist näher an einer echten Release-Barriere statt nur an einem “compiles on my machine”.

## 3. Konkrete Schwächen

- Titel: Toast-Swipe-Unregister verwendet weiterhin den falschen Schlüssel
- Kategorie: echter Bug
- Betroffene Dateien/Module: [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L273)
- Erklärung mit konkretem Codebezug: `RegisterToastSwipe` speichert `_toastSwipeHandlers[toastId]`, `UnregisterToastSwipe` entfernt aber `_toastSwipeHandlers.Remove(elementId)` in [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L280).
- Risikoauswirkung: Handler bleiben hängen; bei Wiederverwendung oder langen Sessions sind Leaks und falsche Dismiss-Callbacks möglich.
- Priorität: hoch

- Titel: `positionFixed`-Cleanup ist nur teilweise implementiert
- Kategorie: Bug-Risiko
- Betroffene Dateien/Module: [components.js](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/wwwroot/js/components.js#L99), [SelectContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Select/SelectContent.razor#L110), [ComboboxContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Combobox/ComboboxContent.razor#L97), [PopoverContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Popover/PopoverContent.razor#L79), [DropdownMenuContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DropdownMenu/DropdownMenuContent.razor#L117), [MenubarContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Menubar/MenubarContent.razor#L51), [NavigationMenuContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/NavigationMenu/NavigationMenuContent.razor#L55), [Cascader.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Cascader/Cascader.razor#L241), [ColorPicker.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/ColorPicker/ColorPicker.razor#L225), [TagInput.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/TagInput/TagInput.razor#L180), [TreeSelect.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/TreeSelect/TreeSelect.razor#L198), [HoverCardContent.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/HoverCard/HoverCardContent.razor#L60)
- Erklärung mit konkretem Codebezug: `components.js` hält Cleanup-Callbacks für globale `scroll`-/`resize`-Listener, aber die meisten Aufrufer rufen `Interop.UnpositionFixed(...)` nicht auf.
- Risikoauswirkung: Globale Event-Handler sammeln sich an, verursachen unnötige Reposition-Arbeit und sind in Summe ein echter Cleanup-Defekt.
- Priorität: hoch

- Titel: `ColorPicker`-Lightness-Regler ist wahrscheinlich ohne Wirkung
- Kategorie: echter Bug
- Betroffene Dateien/Module: [ColorPicker.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/ColorPicker/ColorPicker.razor#L52)
- Erklärung mit konkretem Codebezug: Der Regler für `_lightness` hat keinen `@oninput`-Handler; im File existiert nur `OnHueChanged` in [ColorPicker.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/ColorPicker/ColorPicker.razor#L167).
- Risikoauswirkung: Ein sichtbares UI-Control erfüllt seine Funktion nicht.
- Priorität: hoch

- Titel: `Cascader` verletzt weiter den Nullability-/API-Vertrag
- Kategorie: Bug-Risiko
- Betroffene Dateien/Module: [Cascader.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Cascader/Cascader.razor#L77)
- Erklärung mit konkretem Codebezug: `ValueChanged` ist `EventCallback<List<string>>`, aber `Clear()` ruft `await ValueChanged.InvokeAsync(null!)` in [Cascader.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Cascader/Cascader.razor#L209) auf.
- Risikoauswirkung: Nicht-nullbare Consumer können zur Laufzeit inkonsistenten oder ungültigen State bekommen.
- Priorität: mittel

- Titel: `ToastProvider` und `OverlayProvider` arbeiten weiter mit `async void`
- Kategorie: Bug-Risiko
- Betroffene Dateien/Module: [ToastProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Toast/ToastProvider.razor#L51), [OverlayProvider.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Overlay/OverlayProvider.razor#L94)
- Erklärung mit konkretem Codebezug: Die Event-Handler wurden defensiver gemacht, bleiben aber `async void` und entziehen sich damit sauberer Fehler- und Await-Steuerung.
- Risikoauswirkung: Schwer reproduzierbare Lifecycle- und Renderer-Rennen bleiben möglich.
- Priorität: mittel

- Titel: Der Test-Stack lässt kritische JS-Pfade weiter zu locker durch
- Kategorie: Wartbarkeit
- Betroffene Dateien/Module: [TestContextExtensions.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Helpers/TestContextExtensions.cs#L13)
- Erklärung mit konkretem Codebezug: `JSRuntimeMode.Loose` und ein loses Modul sorgen dafür, dass viele kaputte Interop-Pfade trotzdem grün bleiben.
- Risikoauswirkung: Es gibt weiter ein falsches Sicherheitsgefühl bei JS-lastigen Komponenten.
- Priorität: mittel

## 4. Wahrscheinliche Bugs

- Datei: [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L280)
- Methode/Komponente: `UnregisterToastSwipe`
- Warum vermutlich Bug: Handler werden unter `toastId` gespeichert, aber unter `elementId` entfernt.
- Wie man ihn reproduzieren könnte: Mehrere Toasts mit Swipe registrieren, einen entfernen, dann im Service- oder UI-Test prüfen, dass der Handler weiterhin im Dictionary hängt bzw. weiter feuert.
- Wie man ihn beheben könnte: `UnregisterToastSwipe` auf `toastId` umstellen oder `elementId` und `toastId` konsistent speichern.

- Datei: [ColorPicker.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/ColorPicker/ColorPicker.razor#L52)
- Methode/Komponente: `ColorPicker`
- Warum vermutlich Bug: Der Lightness-Slider besitzt keinen Änderungs-Handler.
- Wie man ihn reproduzieren könnte: Den Lightness-Regler im Browser bewegen; der Farbausgabewert sollte sich nicht ändern.
- Wie man ihn beheben könnte: `OnLightnessChanged` ergänzen und `UpdateFromHsl()` aufrufen.

- Datei: [components.js](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/wwwroot/js/components.js#L99)
- Methode/Komponente: `positionFixed` in Kombination mit mehreren Overlay-Komponenten
- Warum vermutlich Bug: Expliziter Cleanup existiert, wird aber von vielen Konsumenten nicht verwendet.
- Wie man ihn reproduzieren könnte: Popover/Dropdown/HoverCard mehrfach öffnen und schließen; danach sollten globale `scroll`-/`resize`-Listener weiter existieren oder zusätzliche Arbeit bei jedem Event leisten.
- Wie man ihn beheben könnte: Alle Aufrufer an `UnpositionFixed` anbinden und dafür bUnit-/Service-Tests ergänzen.

- Datei: [Cascader.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/Cascader/Cascader.razor#L209)
- Methode/Komponente: `Clear`
- Warum vermutlich Bug: `null!` verletzt den eventseitigen Vertrag.
- Wie man ihn reproduzieren könnte: Komponente an strikt nicht-nullbare Consumer binden und Clear auslösen.
- Wie man ihn beheben könnte: `EventCallback<List<string>?>` verwenden oder immer eine leere Liste senden.

## 5. Architekturrisiken

- `DataGrid` bleibt trotz einzelner Korrekturen ein sehr breites Änderungsobjekt.
- Warum: Rendering, Sortierung, Filter, Server-Loading, Persistenz, Export und Timerlogik leben weiterhin in einer großen Komponente in [DataGrid.razor](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/UI/DataGrid/DataGrid.razor#L1).
- Welche Refactorings sinnvoll wären: Persistenz, Export und Server-Orchestrierung in klar getrennte Services oder interne Controller auslagern.

- Der Interop-Layer ist funktional besser, aber weiterhin ein “God service”.
- Warum: Zu viele unabhängige Browser-Features hängen an einer Klasse in [ComponentInteropService.cs](/C:/Users/bemi/RiderProjects/Lumeo/src/Lumeo/Services/ComponentInteropService.cs#L5).
- Welche Refactorings sinnvoll wären: Separieren in `OverlayInterop`, `ScrollInterop`, `InputInterop`, `TableInterop`.

- Die Teststrategie bleibt stark auf Komponentenbreite statt auf Fehlerrisiken optimiert.
- Warum: Viele einfache Render-Tests, wenige gezielte Defekt- oder Mehrinstanz-Tests.
- Welche Refactorings sinnvoll wären: Weniger “does not throw”, mehr Assertions auf Cleanup, Event-Routing und Zustandstransitionen.

## 6. Test- und Qualitätsbewertung

- Was an den Tests gut ist:
- Die Suite ist groß und schnell; aktuell `1126` Tests grün.
- Für frühere Interop-Defekte gibt es jetzt gezielte Service-Tests, z. B. instanzspezifisches Drawer-/Carousel-/Resize-/BackToTop-Routing in [ComponentInteropServiceTests.cs](/C:/Users/bemi/RiderProjects/Lumeo/tests/Lumeo.Tests/Services/ComponentInteropServiceTests.cs#L190).

- Welche kritischen Bereiche unzureichend abgesichert wirken:
- Kein Test für `UnregisterToastSwipe`
- Kein Test für `UnpositionFixed`-Cleanup in den betroffenen Komponenten
- Kein sichtbarer Test für den `ColorPicker`-Lightness-Pfad
- Kein sichtbarer Test für `Cascader.Clear()` mit Nullability-Vertrag
- Kein risikoorientierter Test für Overlay `Close` vs. `Cancel`

- Ob CI/CD ausreichend ist:
- Deutlich besser als zuvor, aber noch nicht vollständig. Build, Test und Vulnerability-Check sind jetzt sinnvoll vorhanden.

- Welche Checks fehlen:
- Striktere JS-Interop-Tests statt global `Loose`
- Ein kleiner Satz an gezielten Regressionstests für die verbleibenden Randpfade

## 7. Top-10 Maßnahmen

- Problem: `UnregisterToastSwipe` entfernt den falschen Key
- Nutzen: Verhindert Handler-Leaks und falsche Callback-Ausführung
- Aufwand: S

- Problem: `UnpositionFixed` fehlt in vielen Cleanup-Pfaden
- Nutzen: Beseitigt globale Listener-Leaks und unnötige Browser-Arbeit
- Aufwand: M

- Problem: `ColorPicker`-Lightness-Regler ist unverdrahtet
- Nutzen: Behebt einen sichtbaren UI-Defekt
- Aufwand: S

- Problem: `Cascader.Clear()` sendet `null!` trotz nicht-nullbarer API
- Nutzen: Stabilerer Vertrag für Konsumenten
- Aufwand: S

- Problem: `async void` in Providern
- Nutzen: Sauberere Async-/Lifecycle-Steuerung
- Aufwand: M

- Problem: Fehlende Tests für Cleanup- und Randpfade
- Nutzen: Schließt die größten Regression-Lücken
- Aufwand: M

- Problem: `DataGrid` bleibt schwer wartbar
- Nutzen: Reduziert künftige Regressionskosten
- Aufwand: L

- Problem: Interop-Service ist noch zu breit
- Nutzen: Bessere Änderbarkeit und weniger Seiteneffekte
- Aufwand: L

- Problem: Test-Setup nutzt JS-Interop in `Loose`
- Nutzen: Kaputte Interop-Calls fallen früher auf
- Aufwand: M

- Problem: Mehrere Komponenten haben ähnliche Cleanup-Logik dupliziert
- Nutzen: Weniger Inkonsistenz bei zukünftigen Fixes
- Aufwand: M

## 8. Scorecard

- Architektur: `7/10`
- Codequalität: `7/10`
- Konsistenz: `7/10`
- Wartbarkeit: `6/10`
- Testqualität: `7/10`
- Produktionsreife: `7/10`
- Entwicklererlebnis: `8/10`
- Zukunftsfähigkeit: `6/10`

## 9. Finale Einschätzung

- Würde ich dieses Repo heute in Produktion einsetzen?
- Ja, eher als beim ersten Audit. Für einen kontrollierten Einsatz als Component-Library ist der Stand belastbar genug.

- Unter welchen Bedingungen ja/nein?
- Ja, wenn die Library mit etwas technischer Disziplin eingesetzt wird und die verbleibenden Randpfade bekannt sind.
- Nein, wenn `1.0` als “weitgehend regressionssicher und in den kritischen Pfaden abgesichert” verstanden wird.

- Was müsste vor einer 1.0 unbedingt noch passieren?
- `ToastSwipe`-Unregister korrigieren
- `positionFixed`-Cleanup repo-weit schließen
- `ColorPicker` und `Cascader`-Verträge bereinigen
- Die dazugehörigen Defekt-Tests ergänzen
