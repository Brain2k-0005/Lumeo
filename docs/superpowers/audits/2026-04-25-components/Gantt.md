# Gantt

**Path:** `src/Lumeo/UI/Gantt/`
**Class:** Other (data visualization)
**Files:** Gantt.razor, GanttTypes.cs

## Contract — OK
- `@namespace Lumeo` first line, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root div.
- Implements `IAsyncDisposable`, catches `JSDisconnectedException`.
- Uses `IComponentInteropService` interface (injected as `Lumeo.Services.IComponentInteropService`) — no direct `IJSRuntime`.
- No raw color literals, no `dark:` prefixes.

## API — OK
- Other class; judgement-based relative to similar Gantt libraries.
- Present: `Tasks`+`TasksChanged`, `ViewMode`, `OnTaskClick`, `OnDateChange`, `OnProgressChange`, `Height`, `Readonly`, `TrailingContent`, `Class`, `AdditionalAttributes`.
- GanttTypes.cs provides `GanttTask` record and `GanttViewMode` enum.

## Bugs — OK
- JS interop in `OnAfterRenderAsync` guarded by `if (!firstRender || _initialized) return` — correct.
- `OnParametersSetAsync` interop gated by `if (!_initialized || _instanceId is null) return` — correct.
- No `async void`, no discarded tasks in lifecycle.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/GanttPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`gantt`)
- Files declared: 2 of 2
- Missing from registry: none (no JS/CSS in component dir)
- Component deps declared: OK (toggle-group listed)
