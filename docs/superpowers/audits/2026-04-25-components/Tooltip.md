# Tooltip

**Path:** `src/Lumeo/UI/Tooltip/`
**Class:** Overlay
**Files:** Tooltip.razor, TooltipContent.razor, TooltipTrigger.razor

## Contract — WARN
- Tooltip.razor implements `IDisposable` (not `IAsyncDisposable`) — acceptable since it uses no async cleanup, but overlay checklist expects `IAsyncDisposable`.
- No `JSDisconnectedException` caught — Tooltip does not use JS interop directly (pure CSS/Blazor hover state). No ComponentInteropService reference. Acceptable but flagged per overlay checklist.
- `TooltipContent.razor` missing `[Parameter] public string? Class { get; set; }` — Class IS present (line 17). OK.
- `TooltipTrigger.razor` has no `Class` parameter — confirmed absent in code.
- No raw color literals. No `dark:` prefix.

## API — WARN
- Missing `Open`/`OpenChanged` (tooltip visibility is fully internal via mouse events).
- Missing `Disabled` parameter on Tooltip.
- Missing `OnOpen`/`OnClose` callbacks.

## Bugs — OK
- `DelayedDispatch` helper used for show/hide delay — properly disposed in `Dispose()`.
- No `async void`, no direct IJSRuntime.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/TooltipPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (`tooltip`)

## CLI — OK
- Registry entry: present (`tooltip`)
- Files declared: 3 of 3
- Missing from registry: none
- Component deps declared: OK (none)
