# Scheduler

**Path:** `src/Lumeo/UI/Scheduler/`
**Class:** Other
**Files:** Scheduler.razor, SchedulerTypes.cs

## Contract — OK
- `@namespace Lumeo` as first line of Scheduler.razor; `namespace Lumeo` in SchedulerTypes.cs.
- `Class` and `AdditionalAttributes` present. `@attributes="AdditionalAttributes"` on root div.
- No raw color literals. No `dark:` prefix.
- Icons via `<Blazicon Svg="Lucide.*" />`.
- `IAsyncDisposable` implemented. `JSDisconnectedException` caught in `DisposeAsync`.
- Uses `Lumeo.Services.IComponentInteropService`, no direct `IJSRuntime`.

## API — OK
- Other class: `Events`+`EventsChanged`, `OnEventClick`, `OnDateSelect`, `OnEventChange`, `InitialView`, `Height`, `Editable`, `Selectable`.
- Comprehensive API for a scheduler component.

## Bugs — WARN
- `OnAfterRenderAsync`: JS interop guarded by `if (!firstRender || _initialized) return` — OK.
- `_instanceId = await Interop.SchedulerInitAsync(...)` — no null check before `SchedulerGetTitleAsync` immediately after; protected by try/catch JSException.
- Catches `Microsoft.JSInterop.JSException` on init failure (good), but `Microsoft.JSInterop.JSDisconnectedException` in dispose (good).

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/SchedulerPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — WARN
- Registry entry: present (`scheduler`)
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: `button`, `toggle-group` — OK (Scheduler.razor uses `<Button>` and `<ToggleGroup>`)
