# Lumeo v2-rc.18 Deep Technical Review

Datum: 2026-05-05  
Scope: Repository `C:\Users\bemi\RiderProjects\Lumeo`, Source, Docs, API Worker, Tools, Tests und CI. `bin/`, `obj/`, `node_modules/` und Test-Artefakte wurden als generiert behandelt.  
Version im Repo: `Directory.Build.props` meldet `2.0.0-rc.18`.

## 1. Executive Summary

Lumeo ist fuer v2-rc.18 deutlich naeher an einer produktionsnahen Component-Library als an einem Hobbyprojekt: die Paketgrenzen sind erkennbar, Nullability ist fast durchgehend aktiv, die Registry ist konsistent, und der normale Build plus Unit-/bUnit-Testlauf ist gruen. Trotzdem ist der Stand nicht "perfekt" und ich wuerde ihn in dieser Form noch nicht als finalen v2-Release schneiden, weil mehrere Defekte echte Nutzerwirkung haben koennen. Die kritischsten offenen Punkte sitzen nicht in trivialem Styling, sondern in Cross-Instance-State, Async-/Timer-Lifecycle und im Preset-API/CLI-Vertrag. Besonders DataGrid hat zwei belastbare Cross-Talk-Defekte durch statische Drag-State-Holder, die bei mehreren Grids auf einer Seite falsche Reorders ausloesen koennen. Das Preset-Sharing hat eine nachweisbare Namespace-Kollision zwischen lokalen 6-Zeichen-Codes und serverseitigen Worker-IDs; dadurch kann die CLI einen serverseitigen Preset-Code lokal falsch dekodieren und nie vom Worker laden. Die E2E-Suite ist ausgebaut, aber aktuell kein valider Release-Gate: 8 von 16 E2E-Tests schlagen lokal fehl und CI schliesst E2E explizit aus. Positiv ist, dass die Kern-Tests mit 2.111 Tests gruen sind, die Registry alle 131 Komponenten mit Docs-Seiten kennt und die Vulnerability-Checks sauber sind. Gesamturteil: solide Library mit produktionsnaher Basis, aber fuer v2 final noch nicht release-ready ohne gezielte P1-Fixes.

Einschaetzung: produktionsnah, aber noch nicht "v2 final ready".

Groesste 3 Staerken:

- Breite Testbasis: `Lumeo.Tests` 2.111 passed, `Lumeo.Docs.Tests` 20 passed, `Lumeo.RegistryGen.Tests` 16 passed.
- Saubere Modulaufteilung: Core, Charts, DataGrid, Editor, Scheduler, Gantt, Motion, SourceGenerators, Docs, CLI und RegistryGen sind getrennt.
- Registry- und Docs-Abdeckung: `src/Lumeo/registry/registry.json` enthaelt 131 Komponenten, alle mit `hasDocsPage: true`.

Groesste 3 Risiken:

- DataGrid nutzt statischen Drag-State in Row/Header-Cells und kann zwischen Grid-Instanzen cross-talken.
- Preset Worker IDs und lokale Preset-Codes teilen denselben 6-char Base62-Namespace.
- E2E ist aktuell rot und in CI bewusst deaktiviert, obwohl genau diese Tests reale JS-/Keyboard-/Focus-Pfade absichern sollen.

## 2. Was technisch gut ist

### Klare Projekt- und Paketgrenzen

Warum gut: Die Loesung trennt Core-Komponenten (`src/Lumeo`), spezialisierte Pakete (`src/Lumeo.Charts`, `src/Lumeo.DataGrid`, `src/Lumeo.Editor`, `src/Lumeo.Scheduler`, `src/Lumeo.Gantt`, `src/Lumeo.Motion`) und Build-/Tooling-Projekte (`tools/Lumeo.Cli`, `tools/Lumeo.RegistryGen`, `src/Lumeo.SourceGenerators`). Das reduziert Paketkopplung und erlaubt separate NuGet-Artefakte.

Betroffene Dateien/Module: `src/Lumeo/*.csproj`, `src/Lumeo.Charts/*.csproj`, `src/Lumeo.DataGrid/*.csproj`, `tools/Lumeo.Cli/*.csproj`, `.github/workflows/publish.yml`.

Auswirkung: Wartbarkeit und Release-Steuerung sind deutlich besser als bei einer monolithischen Component-Library.

### Nullability ist konsequent aktiviert

Warum gut: Alle produktiven .NET 10 Projekte, Tests und Tools setzen `<Nullable>enable</Nullable>`. Das zwingt viele API-Raender zu expliziterem Umgang mit `null`.

Betroffene Dateien/Module: `src/Lumeo/Lumeo.csproj`, `src/Lumeo.Charts/Lumeo.Charts.csproj`, `src/Lumeo.DataGrid/Lumeo.DataGrid.csproj`, `src/Lumeo.Editor/Lumeo.Editor.csproj`, `src/Lumeo.Scheduler/Lumeo.Scheduler.csproj`, `src/Lumeo.Gantt/Lumeo.Gantt.csproj`, `tests/*/*.csproj`.

Auswirkung: Weniger zufaellige NullReference-Bugs und bessere Consumer-DX in C#.

### Registry und Docs sind systematisch generiert

Warum gut: `src/Lumeo/registry/registry.json` ist maschinell erzeugt und listet Komponenten inklusive Dateien, Dependencies, CSS-Variablen und Docs-Status. Der Build erzeugte 173 Registry Items: 131 Komponenten, 16 Patterns, 8 Blocks, 18 Guides.

Betroffene Dateien/Module: `src/Lumeo/registry/registry.json`, `tools/Lumeo.RegistryGen/Program.cs`, `tests/Lumeo.RegistryGen.Tests`.

Auswirkung: Weniger Drift zwischen Docs, Registry und Source. Fuer Consumer ist das ein klarer DX-Vorteil.

### Viele kritische UI-Pfade sind bereits getestet

Warum gut: Es gibt breite bUnit-Tests fuer Komponenten, Services, DataGrid, Chart, Editor und Docs. DataGrid hat spezialisierte Tests fuer Layout, Server-Requests, Culture, Toolbar und Export. Editor hat RichText- und WordImporter-Tests.

Betroffene Dateien/Module: `tests/Lumeo.Tests/Components`, `tests/Lumeo.Tests/Services`, `tests/Lumeo.Tests/Editor`, `tests/Lumeo.Docs.Tests`.

Auswirkung: Refactorings sind nicht blind. Das erklaert, warum der normale Testlauf trotz grosser Flaeche stabil gruen ist.

### JS-Interop ist teilweise sauber gekapselt

Warum gut: Statt wahlloser `IJSRuntime`-Calls ueberall gibt es zentrale Interop-Schichten wie `ComponentInteropService`, `RichTextInterop`, `KeyboardShortcutService`, plus dedizierte JS-Module. Viele Komponenten implementieren `DisposeAsync` und versuchen JS-seitige Registrierungen abzubauen.

Betroffene Dateien/Module: `src/Lumeo/Services/ComponentInteropService.cs`, `src/Lumeo/Services/Interop/RichTextInterop.cs`, `src/Lumeo/Services/KeyboardShortcutService.cs`, `src/Lumeo.Scheduler/wwwroot/js/scheduler.js`, `src/Lumeo.Gantt/wwwroot/js/gantt-v2.js`.

Auswirkung: Die Architektur ist grundsaetzlich reparierbar, weil JS-Lifecycle nicht komplett ueber die Codebase verstreut ist.

### CI hat solide Basischecks

Warum gut: CI baut mit `-warnaserror`, testet ohne E2E und prueft NuGet-Vulnerabilities. Publishing packt die Hauptpakete getrennt.

Betroffene Dateien/Module: `.github/workflows/ci.yml`, `.github/workflows/publish.yml`.

Auswirkung: Compile-Regressionen und bekannte NuGet-Vulnerabilities werden nicht einfach durchgewunken.

## 3. Konkrete Schwaechen

### P1: DataGrid Column-Reorder nutzt globalen statischen Drag-State

Kategorie: echter Bug / Cross-Talk / Concurrency-Risiko  
Betroffene Dateien/Module: `src/Lumeo.DataGrid/UI/DataGrid/DataGridHeaderCell.razor:210-270`  
Prioritaet: hoch

Konkreter Codebezug:

```razor
ColumnDragState.DragSourceIndex = ColumnIndex;
ColumnDragState.DragSourceId = Column.Id;

private static class ColumnDragState
{
    public static int DragSourceIndex = -1;
    public static string? DragSourceId;
}
```

Erklaerung: Jede DataGrid-Instanz im Prozess teilt denselben statischen `ColumnDragState`. Wenn zwei Grids auf derselben Seite oder in Blazor Server sogar in unterschiedlichen Circuit-Kontexten aktiv sind, kann ein Drag aus Grid A einen Drop in Grid B beeinflussen. Der Code prueft keine Grid-ID und keine Instanzzugehoerigkeit.

Risikoauswirkung: Falsche Spalten werden in der falschen Grid-Instanz verschoben. In Blazor Server ist das zusaetzlich thread-/circuit-uebergreifend problematisch.

Fix-Idee: Drag-State in `DataGrid<TItem>` oder `DataGridContext<TItem>` halten, eine `GridInstanceId` mitgeben und Drops verwerfen, wenn Source- und Target-Grid nicht identisch sind. Danach einen Cross-grid-Test bauen.

### P1: DataGrid Row-Reorder nutzt ebenfalls globalen statischen Drag-State

Kategorie: echter Bug / Cross-Talk / Concurrency-Risiko  
Betroffene Dateien/Module: `src/Lumeo.DataGrid/UI/DataGrid/DataGridRow.razor:241-258`  
Prioritaet: hoch

Konkreter Codebezug:

```razor
DragState.DragSourceIndex = RowIndex;
ParentGrid?.MoveRow(sourceIndex, RowIndex);

private static class DragState
{
    public static int DragSourceIndex = -1;
}
```

Erklaerung: Das gleiche Problem existiert fuer Rows. Bei zwei Grids mit `RowReorderable=true` kann ein Drop auf Grid B einen Source-Index aus Grid A verwenden. Es gibt keinen Source-Grid-Guard.

Risikoauswirkung: Datenreihenfolge kann in der falschen Grid-Instanz veraendert werden. Das ist funktional falsch, nicht nur unschoen.

Fix-Idee: Row-Drag-State pro Grid-Instanz speichern, `MoveRow` nur akzeptieren, wenn Drag-Token zur aktuellen Grid-Instanz gehoert. Test: zwei Grids rendern, in A DragStart ausloesen, in B Drop ausloesen, B muss unveraendert bleiben.

### P1: Preset-Worker-ID und lokaler Preset-Code kollidieren im selben Namespace

Kategorie: echter Bug / API-Design / Robustheit  
Betroffene Dateien/Module: `api/worker.js:13-42`, `src/Lumeo/Theming/LumeoPresetCodec.cs:29-31`, `tools/Lumeo.Cli/ThemeCommands.cs:122-134`  
Prioritaet: hoch

Konkreter Codebezug:

```csharp
if (LumeoPresetCodec.TryDecode(preset, out var decoded))
{
    source = "client-side";
    resolved = ResolveFromDecoded(decoded);
}
else
{
    source = "server";
    resolved = await TryFetchFromWorker(preset);
}
```

Erklaerung: Lokale Preset-Codes sind 6 Zeichen Base62. Worker-IDs sind auch 6 Zeichen Base62. Die CLI versucht immer zuerst lokal zu dekodieren. Weil der lokale Codec nur die Version-Bits prueft und 6-char-Codes akzeptiert, werden einige Server-IDs faelschlich als lokale Codes interpretiert.

Nachweis: Ein lokaler Node-Repro, der Worker-Hash und Codec-Versionstest nachbildet, fand sofort eine kollidierende Worker-ID:

```json
{"i":8,"id":"H3FLCh","obj":{"theme":"x8","baseColor":"slate","radius":"1","dark":true}}
```

Risikoauswirkung: Ein Nutzer teilt einen serverseitigen Preset-Link, die CLI dekodiert ihn lokal falsch, laedt nie vom Server und schreibt eine andere Theme-Konfiguration.

Fix-Idee: Namespaces trennen, z. B. `l_` fuer lokale Codes und `p_` fuer Worker-IDs, oder Worker-IDs auf 10-12 Zeichen verlaengern und lokale Codes strikt mit Prefix akzeptieren. Regressionstest: serverseitige ID, die lokal dekodierbar waere, muss serverseitig aufgeloest werden.

### P1: E2E-Tests sind rot und gleichzeitig nicht CI-gated

Kategorie: Testqualitaet / Release-Gate / Bug-Risiko  
Betroffene Dateien/Module: `tests/Lumeo.Tests.E2E`, `.github/workflows/ci.yml:26-32`  
Prioritaet: hoch

Erklaerung: Der lokale E2E-Lauf gegen die Docs-App ergab 8 passed, 8 failed. CI schliesst `Lumeo.Tests.E2E` per Filter aus:

```yaml
run: dotnet test Lumeo.slnx -c Release --no-build --verbosity normal --filter "FullyQualifiedName!~Lumeo.Tests.E2E"
```

Konkrete Failures:

- `DialogFocusTrapTests` sucht `Open Dialog`, die Docs triggern aktuell `Edit Profile` und `Share`.
- `DropdownKeyboardTests` sucht `Open Menu`, die Docs triggern aktuell `Open`.
- `Search_palette_navigates_to_component_on_click` nutzt `text=Badge`, trifft aber auch Seiteninhalt ausserhalb der Palette.
- `Home_page_above_the_fold_matches_baseline` scheitert, weil `tests/Lumeo.Tests.E2E/Snapshots/home-above-fold.png` fehlt.

Risikoauswirkung: Focus-Trap, Keyboard-Navigation, Search-Palette und visuelle Docs-Regressionen werden nicht release-blockierend erkannt.

Fix-Idee: E2E-Selektoren stabilisieren (`data-testid`, Rolle/Accessible Name), Baseline committen oder Test in Update/Compare splitten, separate CI-Job mit Docs-Server und Playwright-Browsern aktivieren.

### P1: DataGrid EventCallbacks werden an mehreren Stellen fire-and-forget aufgerufen

Kategorie: falsche Async-Nutzung / Bug-Risiko  
Betroffene Dateien/Module: `src/Lumeo.DataGrid/UI/DataGrid/DataGrid.razor:764`, `:1063`, `:1071`, `:1079`, `:1171`, `:1189`  
Prioritaet: hoch

Konkreter Codebezug:

```razor
SelectedItemsChanged.InvokeAsync(_selectedItems.AsReadOnly());
OnColumnReorder.InvokeAsync(args);
OnRowReorder.InvokeAsync(new RowReorderEventArgs<TItem>(item, oldIndex, newIndex));
```

Erklaerung: `EventCallback.InvokeAsync` gibt ein `Task` zurueck. Der Code ignoriert dieses Task an mehreren Stellen und ruft danach `StateHasChanged()`. Async-Consumer-Handler koennen dadurch unobserved fehlschlagen, und gebundener Parent-State kann hinterherlaufen.

Risikoauswirkung: Consumer sieht gelegentlich stale State, Exceptions gehen in Lifecycle-Noise unter oder erscheinen unkontrolliert im Renderer. Gerade Selection/Reorder sind API-Pfade, die Consumer ernsthaft nutzen.

Fix-Idee: Handler auf `async Task` umstellen und `await` verwenden. Bei Eventquellen, die synchron bleiben muessen, `SafeAsyncDispatcher` korrekt einsetzen und Exceptions bewusst loggen.

### P2: DataGrid Autosave filtert stale Generation nur fuer LocalStorage, nicht fuer `OnLayoutSave`

Kategorie: Race Condition / Async-Lifecycle  
Betroffene Dateien/Module: `src/Lumeo.DataGrid/UI/DataGrid/DataGrid.razor:1420-1444`, `src/Lumeo.DataGrid/UI/DataGrid/DataGridLayoutService.cs:84-95`  
Prioritaet: mittel bis hoch

Erklaerung: `ScheduleAutoSave` captured eine Generation und `PersistAsync` ignoriert stale Generationen vor dem LocalStorage-Schreiben. Aber `PersistLayoutAsync` ruft `OnLayoutSave` nach `PersistAsync` immer weiter auf. Wenn ein alter Timer Callback nach Dispose/Reschedule doch noch laeuft, kann ein Consumer ein veraltetes Layout erhalten.

Risikoauswirkung: Server-/Consumer-seitige Layout-Speicherung kann stale Layouts erhalten, obwohl LocalStorage korrekt unterdrueckt wird.

Fix-Idee: Generation vor Snapshot und vor externem Callback pruefen. Alternativ `ScheduleAutoSave` so umbauen, dass nur die aktuellste Generation den Callback ueberhaupt ausfuehrt.

### P2: `SafeAsyncDispatcher` faengt das aeussere `InvokeAsync`-Task nicht ab

Kategorie: falsche Async-Nutzung / Lifecycle-Risiko  
Betroffene Dateien/Module: `src/Lumeo/Services/SafeAsyncDispatcher.cs:24-36`  
Prioritaet: mittel

Konkreter Codebezug:

```csharp
_ = invokeAsync(async () =>
{
    try { await work(); }
    ...
});
```

Erklaerung: Exceptions innerhalb von `work` werden gefangen. Wenn aber `invokeAsync(...)` selbst fehlschlaegt, z. B. Renderer/Circuit schon disposed bevor die Delegate-Ausfuehrung angenommen wird, bleibt das aeussere Task unobserved.

Risikoauswirkung: Genau der zentrale Helper fuer lifecycle-sicheres fire-and-forget kann an der aeusseren Dispatch-Grenze doch noch unobserved Exceptions erzeugen.

Fix-Idee: Internes `RunAsync` starten und darin `await invokeAsync(...)` in einem try/catch kapseln. Noch besser: eine `Task`-returning Variante anbieten und Fire-and-forget nur an wenigen Top-Level-Stellen erlauben.

### P2: Alert AutoDismiss ignoriert das `InvokeAsync`-Task und hat Timer/Dispose-Race

Kategorie: Disposal/Cleanup / falsche Async-Nutzung  
Betroffene Dateien/Module: `src/Lumeo/UI/Alert/Alert.razor:138-152`  
Prioritaet: mittel

Erklaerung: `AutoDismissCallback` ruft `InvokeAsync(async () => ...)` ohne `await` oder `_ =` plus Fehlerbehandlung auf. `Dispose()` disposed nur den Timer, aber ein bereits gestarteter Timer-Callback kann danach noch versuchen, `OnDismiss` und `StateHasChanged` auszufuehren.

Risikoauswirkung: Unobserved Lifecycle-Exceptions und sporadische Updates nach Dispose.

Fix-Idee: `CancellationTokenSource` plus `Task.Delay` verwenden oder `SafeAsyncDispatcher` korrekt erweitern und nutzen. Disposed-Flag pruefen.

### P2: Chart Phantom-Timer kann nach Dispose/State-Wechsel weiter dispatchen

Kategorie: Race Condition / Disposal-Cleanup  
Betroffene Dateien/Module: `src/Lumeo.Charts/UI/Chart/Chart.razor:267-282`, `:367-380`  
Prioritaet: mittel

Erklaerung: Phantom Loading nutzt `System.Threading.Timer` und ruft `_ = InvokeAsync(async () => ...)`. Das innere JS-Update faengt `JSDisconnectedException` und `ObjectDisposedException`, aber das aeussere `InvokeAsync`-Task wird nicht beobachtet. `Timer.Dispose()` wartet nicht auf laufende Callbacks.

Risikoauswirkung: Sporadische Dispatches nach Dispose, stale `_lastJson` und schwer reproduzierbare Lifecycle-Exceptions bei schnellem Navigieren oder Loading-State-Wechsel.

Fix-Idee: `PeriodicTimer` mit `CancellationTokenSource`, `_disposed`-Flag und awaited Shutdown in `DisposeAsync`. Alternativ Dispatcher-Helper korrigieren und Timer Callback strikt generation-guarded machen.

### P2: RichTextEditor JS entfernt nicht alle registrierten Event-Handler

Kategorie: Event-Handler-Leak / Memory-Leak  
Betroffene Dateien/Module: `src/Lumeo.Editor/wwwroot/js/rich-text-editor.js:477-526`, `:903-914`, `:1096-1102`  
Prioritaet: mittel

Konkreter Codebezug:

```js
dom.addEventListener('mousemove', (e) => { ... });
dom.addEventListener('mouseleave', () => { ... });
el.addEventListener('drop', async (e) => { ... });

destroy() { try { handle.remove(); } catch (_) {} },
```

Erklaerung: Die Drag-Handle-Funktion registriert anonyme Listener auf `dom`, zerstoert aber nur das Handle. Der `drop`-Listener auf dem Editor-Element ist ebenfalls anonym und wird beim `destroy(id)` nicht entfernt. Die Closures halten `editor`, `dotNetRef`, Node-Refs und Upload-Funktionen.

Risikoauswirkung: Nach Editor-Destroy koennen DOM-Listener weiterleben, wenn DOM-Knoten laenger existieren oder durch Framework-Lifecycle verzögert entfernt werden. Bei vielen Editor-Mounts kann das Speicher und alte .NET-Refs halten.

Fix-Idee: Listener als benannte Funktionen speichern, in `instances` registrieren und in `destroy(id)` konsequent mit `removeEventListener` entfernen.

### P2: RichTextEditor laedt Kernabhaengigkeiten zur Laufzeit von `esm.sh`

Kategorie: externe Abhaengigkeit / Security-Robustheit / Offline-Risiko  
Betroffene Dateien/Module: `src/Lumeo.Editor/wwwroot/js/rich-text-editor.js:30-55`  
Prioritaet: mittel

Erklaerung: TipTap, ProseMirror, Lowlight und Highlight.js werden per Dynamic Import von `https://esm.sh/...` geladen. Das ist fuer Demos bequem, aber fuer eine produktionsreife Library problematisch: CSP, Offline-Nutzung, Supply-Chain-Kontrolle, CDN-Ausfall und Version-Pinning sind nicht sauber in NuGet/npm kontrolliert.

Risikoauswirkung: RichTextEditor kann in Enterprise/CSP-Umgebungen oder offline gebauten Apps ausfallen. Consumer muessen eine externe CDN-Policy akzeptieren, ohne dass NuGet das sichtbar erzwingt.

Fix-Idee: Assets vendoren oder als optionale peer/static web assets bereitstellen. Mindestens klar dokumentieren und einen self-hosted Modus anbieten.

### P2: Word-Import ist memory-heavy und hat keine Groessenlimits

Kategorie: Performance / Robustheit / DoS-Risiko  
Betroffene Dateien/Module: `src/Lumeo.Editor/wwwroot/js/rich-text-editor.js:1018-1027`, `src/Lumeo.Editor/UI/RichTextEditor/RichTextEditor.razor:329-345`, `src/Lumeo.Editor/UI/RichTextEditor/WordImporter.cs:92-119`  
Prioritaet: mittel

Erklaerung: Der Browser liest die komplette `.docx` als `ArrayBuffer`, baut daraus einen Binary String, erzeugt Base64 und uebergibt alles an .NET. .NET ruft `Convert.FromBase64String`, erzeugt erneut ein Byte-Array und gibt es an Mammoth. Fuer grosse Dateien entstehen mehrere Kopien im Speicher.

Risikoauswirkung: Große DOCX-Dateien koennen Browser-Tab, WASM-Runtime oder Server-Circuit stark belasten. Es gibt keine client- oder serverseitige Maximalgroesse.

Fix-Idee: Max-Size clientseitig vor `arrayBuffer()` und serverseitig vor Decode pruefen. Fehler an UI zurueckgeben. Streaming waere ideal, aber ein hartes Limit ist der noetige v2-Fix.

### P2: Gantt laesst Document-Drag-Listener bei Destroy waehrend Drag liegen

Kategorie: Event-Handler-Leak / Lifecycle-Race  
Betroffene Dateien/Module: `src/Lumeo.Gantt/wwwroot/js/gantt-v2.js:477-490`, `:719-724`  
Prioritaet: mittel

Erklaerung: Beim Drag werden `document.mousemove` und `document.mouseup` registriert und im normalen `mouseup` entfernt. Wenn die Komponente waehrend eines aktiven Drags zerstoert wird, ruft `destroy(id)` nur `host.innerHTML = ''` und `instances.delete(id)` auf. Die lokalen Handler sind nicht in `inst` gespeichert und koennen nicht entfernt werden.

Risikoauswirkung: Route-Change oder Conditional Rendering waehrend Drag kann globale Listener behalten, die auf geloeschte Instanzen zeigen.

Fix-Idee: In `inst._dragCleanup` die Remove-Funktion speichern. Vor neuem Drag und in `destroy(id)` aufrufen.

### P2: Docs nutzen noch `eval` fuer Icon-Scrollreset

Kategorie: Security-/CSP-Risiko  
Betroffene Dateien/Module: `docs/Lumeo.Docs/Pages/Components/IconPage.razor:497-498`  
Prioritaet: mittel

Konkreter Codebezug:

```csharp
await JS.InvokeVoidAsync("eval",
    $"(function(){{var el=document.getElementById('{_gridId}');if(el)el.scrollTop=0;}})()");
```

Erklaerung: `_gridId` ist intern generiert, daher ist die unmittelbare Exploitability gering. Trotzdem bricht `eval` CSP-Kompatibilitaet und widerspricht dem Anspruch einer sauberen v2-Dokumentation.

Risikoauswirkung: Strikte CSP blockiert die Docs-Funktion, Security-Audits flaggen die Seite.

Fix-Idee: Kleine benannte JS-Funktion in `docs/Lumeo.Docs/wwwroot/js/docs.js`, z. B. `window.lumeoDocs.resetScrollTopById(id)`.

### P2: Worker payload-size check vertraut nur `content-length`

Kategorie: Robustheit / Security  
Betroffene Dateien/Module: `api/worker.js:70-77`  
Prioritaet: mittel

Erklaerung: Der Worker prueft `request.headers.get("content-length") > MAX_BODY_BYTES`, liest danach aber `await request.json()`. Wenn der Header fehlt oder falsch ist, wird das Limit nicht vor dem Body-Parse erzwungen.

Risikoauswirkung: Ein Client kann groessere Payloads senden und Worker-CPU/Memory belasten, obwohl `MAX_BODY_BYTES` existiert.

Fix-Idee: Body als Stream oder Text mit realem Byte-Limit lesen. Danach JSON parsen. Header darf nur ein Early-Reject sein, nicht die einzige Grenze.

### P2: Worker ueberschreibt bei Hash-Kollision bestehende Presets

Kategorie: Datenintegritaet / API-Design  
Betroffene Dateien/Module: `api/worker.js:90-99`  
Prioritaet: mittel

Erklaerung: Der Kommentar sagt selbst, dass bei unterschiedlichem Payload unter gleicher ID ueberschrieben wird. Bei 6 Zeichen Base62 und content-addressed IDs ist eine Kollision nicht alltaeglich, aber fuer ein Share-API-Design vermeidbar.

Risikoauswirkung: Ein alter geteilter Link kann ploetzlich eine andere Konfiguration liefern.

Fix-Idee: IDs verlaengern und Collision-Retry mit Salt einfuehren. Bei vorhandener anderer Payload niemals ueberschreiben.

### P3: `dotnet format --verify-no-changes` ist rot

Kategorie: Build-/CI-Qualitaet  
Betroffene Dateien/Module: mehrere Dateien, u. a. `src/Lumeo.Charts/UI/Chart/ChartLabelHelper.cs`, `src/Lumeo.Editor/UI/RichTextEditor/AiAction.cs`, `src/Lumeo.SourceGenerators/LumeoFormGenerator.cs`, `tools/Lumeo.RegistryGen/Program.cs`, `src/Lumeo/Services/Localization/Locales/LumeoDefaultStrings.Arabic.cs`  
Prioritaet: niedrig bis mittel

Erklaerung: Der Format-Check meldet viele Whitespace-/Formatting-Abweichungen. Das ist kein Runtime-Bug, aber ein Release-Gate-Problem, wenn "perfekt" ernst gemeint ist.

Risikoauswirkung: Keine direkte Nutzerwirkung, aber CI-Disziplin und Diff-Qualitaet leiden.

Fix-Idee: `dotnet format Lumeo.slnx --no-restore` ausfuehren und danach `--verify-no-changes` in CI aufnehmen.

### P3: Mobile Docs sind grundsaetzlich responsive, aber Block-Demos erzwingen Desktop-Breiten

Kategorie: UX / Docs-Qualitaet / Mobile-Freundlichkeit  
Betroffene Dateien/Module: `docs/Lumeo.Docs/Pages/Blocks/DashboardBlock.razor:10`, `PricingTableBlock.razor:10`, `SettingsPageBlock.razor:10`, `docs/Lumeo.Docs/Shared/ComponentDemo.razor:103`  
Prioritaet: niedrig bis mittel

Erklaerung: Layout und Mobile Nav sind gut umgesetzt, aber mehrere Blocks nutzen `min-w-[960px]`, `min-w-[700px]`, `min-w-[840px]`. `ComponentDemo` faengt das mit `overflow-x-auto` ab. Das verhindert Layoutbruch, ist aber nicht gleich "mobile perfekt".

Risikoauswirkung: Auf Phones entstehen horizontale Scroll-Demos statt echte mobile Previews.

Fix-Idee: Fuer Blocks entweder echte responsive Varianten bauen oder explizit einen Desktop-Preview-Viewport mit Umschalter "Mobile/Desktop" einfuehren.

## 4. Wahrscheinliche Bugs

### Bug 1: Server-Preset-Code kann lokal falsch dekodiert werden

Datei: `tools/Lumeo.Cli/ThemeCommands.cs`, `src/Lumeo/Theming/LumeoPresetCodec.cs`, `api/worker.js`  
Methode/Komponente: `ThemeCommands.Apply`, `LumeoPresetCodec.TryDecode`, Worker `contentId`

Warum vermutlich Bug: Server-ID und lokaler Code teilen denselben 6-char Base62-Raum. Die CLI versucht zuerst lokal zu dekodieren. Der Repro fand `H3FLCh` als Worker-ID, die lokal als Version-1-Preset akzeptiert wuerde.

Wie reproduzieren: Worker-ID erzeugen, deren Low-Bits Version `1` ergeben, dann `lumeo apply H3FLCh` ausfuehren. Die CLI meldet `via client-side` statt `via server`.

Wie beheben: Prefix oder laengere serverseitige IDs. CLI-Resolver muss Namespace explizit unterscheiden.

### Bug 2: Column-Reorder cross-talkt zwischen DataGrid-Instanzen

Datei: `src/Lumeo.DataGrid/UI/DataGrid/DataGridHeaderCell.razor`  
Methode/Komponente: `HandleDragStart`, `HandleDrop`, `ColumnDragState`

Warum vermutlich Bug: Static State enthaelt nur Source-Index und Source-ID, aber keine Grid-Instanz. Das ist bei mehreren Grids objektiv falsch.

Wie reproduzieren: Zwei DataGrids mit reorderable columns rendern. DragStart auf Grid A, Drop auf Header-Zelle in Grid B simulieren. Grid B verwendet den Source-Index aus A.

Wie beheben: Instanzgebundener Drag-State mit `GridId`; Drop in anderer Instanz ignorieren.

### Bug 3: Row-Reorder cross-talkt zwischen DataGrid-Instanzen

Datei: `src/Lumeo.DataGrid/UI/DataGrid/DataGridRow.razor`  
Methode/Komponente: `HandleDragStart`, `HandleDrop`, `DragState`

Warum vermutlich Bug: Static `DragSourceIndex` ist global und wird nicht an ParentGrid gebunden.

Wie reproduzieren: Zwei row-reorderable Grids rendern. DragStart in A, Drop in B. B ruft `MoveRow(sourceIndex, RowIndex)` mit Source aus A.

Wie beheben: Source-Grid-ID und Drag-Token im ParentGrid halten.

### Bug 4: E2E-Tests pruefen veraltete Docs-Selectors

Datei: `tests/Lumeo.Tests.E2E/Smokes/DialogFocusTrapTests.cs`, `DropdownKeyboardTests.cs`  
Methode/Komponente: Playwright Locators

Warum vermutlich Bug: Tests suchen `Open Dialog` und `Open Menu`, die aktuellen Docs verwenden `Edit Profile`, `Share` und `Open`.

Wie reproduzieren: Docs lokal starten und E2E-Projekt ausfuehren. Drei Dialog- und drei Dropdown-Tests timeouten.

Wie beheben: Stable `data-testid` oder Role+Name verwenden, die Docs bewusst testbar halten.

### Bug 5: Search Palette E2E klickt potentiell falsches `Badge`

Datei: `tests/Lumeo.Tests.E2E/Smokes/SearchPaletteTests.cs:111-119`  
Methode/Komponente: `Search_palette_navigates_to_component_on_click`

Warum vermutlich Bug: `Page.Locator("text=Badge").First` ist global und nicht auf die Palette begrenzt. Der lokale Lauf traf Seiteninhalt statt Result-Button.

Wie reproduzieren: `/components` laden, Palette oeffnen, `badge` suchen, Test ausfuehren. Playwright kann vom Overlay abgefangene Pointer-Events melden.

Wie beheben: Result-Locator auf Palette scopen, z. B. `[role=dialog] button:has-text("Badge")` oder `data-testid="search-result-badge"`.

### Bug 6: Gantt kann globale Drag-Listener nach Destroy behalten

Datei: `src/Lumeo.Gantt/wwwroot/js/gantt-v2.js`  
Methode/Komponente: Drag Handling / `destroy(id)`

Warum vermutlich Bug: `document.addEventListener('mousemove', onMove)` und `mouseup` werden nur im normalen `onUp` entfernt. `destroy(id)` kennt diese Handler nicht.

Wie reproduzieren: Gantt-Task draggen, waehrend Drag durch Navigation/Conditional Rendering Komponente unmounten, danach Document-Listener pruefen oder unerwartete callbacks beobachten.

Wie beheben: Cleanup-Funktion in `inst` speichern und in `destroy(id)` ausfuehren.

## 5. Architekturrisiken

### DataGrid ist funktional stark, aber interne State-Grenzen sind zu weich

Was heute funktioniert: Ein einzelnes Grid mit Standardinteraktionen funktioniert und ist gut getestet.  
Warum spaeter problematisch: Reorder, Selection, Layout-Persistence und Server-Modus sind komplexe State-Maschinen. Static Drag-State und unawaited EventCallbacks sind Indizien, dass Instanzgrenzen und Async-Vertraege nicht konsequent genug sind.  
Sinnvolles Refactoring: `DataGridContext<TItem>` als alleinige State-Grenze fuer Drag/Reorder/Layout-Operationen ausbauen; EventCallbacks konsequent async; Cross-instance Tests als Pflicht.

### JS-Interop ist zentralisiert, aber nicht durchgehend lifecycle-formalisiert

Was heute funktioniert: Viele Components disposen ihre JS-Registrierungen best-effort.  
Warum spaeter problematisch: Timer, globale Event-Listener und CDN-Imports sind schwer zu auditieren, wenn jede Komponente ein eigenes Pattern nutzt.  
Sinnvolles Refactoring: Ein gemeinsamer `JsRegistration`/`InteropHandle`-Abstraktion fuer add/remove Listener, Timers, observers und DotNetObjectReference. Alle JS-Module muessen `destroy(id)` idempotent und vollstaendig machen.

### Docs sind Feature-reich, aber noch kein Release-Gate

Was heute funktioniert: Alle Registry-Komponenten haben Docs-Seiten und die Navigation ist mobil bedacht.  
Warum spaeter problematisch: Docs sind die Demo-Flaeche fuer alle Komponenten. Wenn E2E rot und nicht CI-gated ist, koennen Komponenten "gruen" wirken, obwohl reale Browserpfade defekt sind.  
Sinnvolles Refactoring: Docs-Komponenten mit Test-IDs/ARIA-Kontrakten versehen, E2E gegen diese Kontrakte laufen lassen, mobile Snapshots fuer Kernseiten einfuehren.

### Preset-Sharing API braucht einen stabileren Public Contract

Was heute funktioniert: Kleine Presets koennen ueber Worker/KV gespeichert werden.  
Warum spaeter problematisch: 6-char IDs, Namespace-Kollision und Overwrite-on-collision sind fuer ein oeffentliches Share-Feature zu fragil.  
Sinnvolles Refactoring: Versioniertes ID-Format, klare Prefixe, laengere Worker IDs, serverseitige Tests und CLI-Vertrag dokumentieren.

## 6. Test- und Qualitaetsbewertung

### Ausgefuehrte Checks

- `dotnet build Lumeo.slnx -c Release --no-restore`: erfolgreich, 0 warnings, 0 errors.
- `dotnet test Lumeo.slnx -c Release --no-build --verbosity minimal --filter "FullyQualifiedName!~Lumeo.Tests.E2E"`: erfolgreich.
- `Lumeo.Tests`: 2.111 passed.
- `Lumeo.Docs.Tests`: 20 passed.
- `Lumeo.RegistryGen.Tests`: 16 passed.
- `dotnet test tests/Lumeo.Tests.E2E/Lumeo.Tests.E2E.csproj -c Release --no-build --verbosity minimal`: 8 passed, 8 failed.
- `dotnet format Lumeo.slnx --verify-no-changes --no-restore --verbosity minimal`: fehlgeschlagen wegen Formatting-Diffs.
- `dotnet list Lumeo.slnx package --vulnerable --include-transitive`: keine bekannten vulnerablen Pakete gefunden.
- `npm audit --omit=dev`: 0 vulnerabilities.

### Was an den Tests gut ist

- Breite Komponententests ueber viele UI-Bausteine.
- DataGrid hat deutlich mehr als Smoke-Tests, inklusive Layout, Server-Service, Culture und Export.
- RegistryGen ist getestet und Registry/Docs-Drift wird teilweise abgefangen.
- RichTextEditor und WordImporter haben Basistests.

### Kritische unzureichend abgesicherte Bereiche

- Cross-grid DataGrid Drag/Reorder fehlt.
- Row-Reorder scheint nicht gezielt getestet zu sein.
- Async EventCallback Failure/awaiting ist nicht abgesichert.
- Worker API hat keine sichtbaren Tests fuer ID-Namespace, Collision, Payload-Limit.
- RichTextEditor JS listener cleanup und grosse Word-Dateien sind nicht abgesichert.
- E2E deckt wichtige Pfade ab, ist aber aktuell rot und dadurch nicht vertrauenswuerdig.
- Mobile Docs/Blocks haben keine Viewport-Gates.
- Kein dedizierter Testordner fuer Registry-Komponenten `Chip`, `Icon`, `List`, `Overlay`, `Sortable`; OverlayService ist getestet, aber nicht die volle Component-Oberflaeche.

### CI/CD-Bewertung

CI ist ausreichend fuer Compile- und Unit-Sicherheit, aber nicht ausreichend fuer v2-Release-Readiness. Es fehlen:

- `dotnet format --verify-no-changes`.
- E2E-Workflow mit Docs-Server und Playwright.
- Snapshot-Baselines oder stabiler Visual-Diff.
- `npm audit` oder zumindest CSS-build reproducibility check.
- Worker/API-Tests.
- Public API/package smoke test mit lokal gepacktem NuGet in einer minimalen Consumer-App.

## 7. Top-10 Massnahmen

1. Problem: Preset-Code/Worker-ID Namespace-Kollision. Nutzen: verhindert falsche Theme-Anwendung bei geteilten Presets. Aufwand: M.
2. Problem: DataGrid Column/Row Drag-State ist static. Nutzen: beseitigt cross-instance und Blazor-Server-Cross-Talk. Aufwand: M.
3. Problem: DataGrid EventCallbacks sind unawaited. Nutzen: korrekte Consumer-Async-Vertraege und weniger stale state. Aufwand: M.
4. Problem: E2E ist rot und nicht CI-gated. Nutzen: reale Browser-/Keyboard-/Focus-Pfade werden release-blockierend. Aufwand: M.
5. Problem: DataGrid Autosave kann stale `OnLayoutSave` feuern. Nutzen: verhindert falsche Layoutpersistenz in Consumer-Backends. Aufwand: M.
6. Problem: RichTextEditor JS listener cleanup unvollstaendig. Nutzen: weniger Memory-Leaks und stale DotNetRef-Risiken. Aufwand: M.
7. Problem: Timer/Fire-and-forget Patterns in Alert/Chart/SafeAsyncDispatcher. Nutzen: sauberere Lifecycle-Robustheit bei Navigation/Dispose. Aufwand: M.
8. Problem: Worker payload limit und collision handling fragil. Nutzen: stabilerer oeffentlicher API-Vertrag. Aufwand: S bis M.
9. Problem: `eval` in Docs und CDN-Imports im Editor. Nutzen: bessere CSP-/Enterprise-Tauglichkeit. Aufwand: S fuer `eval`, M/L fuer Editor self-host assets.
10. Problem: Mobile Block-Demos sind Desktop-min-width-basiert. Nutzen: Docs wirken auf Phones nicht nur "benutzbar", sondern wirklich v2-polished. Aufwand: M.

## 8. Scorecard

- Architektur: 7/10
- Codequalitaet: 7/10
- Konsistenz: 7/10
- Wartbarkeit: 7/10
- Testqualitaet: 7/10
- Produktionsreife: 6/10
- Entwicklererlebnis: 7/10
- Zukunftsfaehigkeit: 7/10

Begruendung: Die Basis ist stark genug, um v2 final realistisch zu erreichen. Die Scores werden aber durch P1-Defekte, rote E2E und fehlende Release-Gates klar begrenzt.

## 9. Finale Einschaetzung

Ich wuerde Lumeo v2-rc.18 heute nicht als finalen v2-Release markieren. Ich wuerde es fuer interne Apps, Demos oder kontrollierte Pilotprojekte einsetzen, wenn DataGrid-Reorder, RichTextEditor-CDN-Abhaengigkeit und Preset-Sharing nicht kritisch sind oder bewusst ausgeschlossen werden. Fuer ein oeffentliches v2-Release mit Anspruch "alle Komponenten perfekt, keine Hidden Futures/Missing Futures" muessen die P1-Fixes vorher erledigt werden.

Vor v2 final muss unbedingt passieren:

- DataGrid Cross-Talk entfernen und mit Mehrfachinstanz-Tests absichern.
- Preset-ID-Vertrag versionieren/prefixen und Worker/CLI-Tests ergaenzen.
- E2E stabilisieren, Baseline korrigieren und in CI aktivieren.
- Async-/Timer-Lifecycle in DataGrid, Alert, Chart und SafeAsyncDispatcher bereinigen.
- RichTextEditor Cleanup und Word-Import-Limits fixen.
- Docs mobile und CSP sauber machen, inklusive `eval`-Entfernung.

Release-Entscheidung: v2-rc.18 ist ein guter Release Candidate, aber kein finaler Release Candidate im strengen Sinn. Wenn die Top-5-Massnahmen geschlossen sind und E2E plus Format-Gate gruen in CI laufen, wuerde ich die Library deutlich naeher bei "production ready" einordnen.

