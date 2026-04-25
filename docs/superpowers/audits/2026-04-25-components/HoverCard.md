# HoverCard

**Path:** `src/Lumeo/UI/HoverCard/`
**Class:** Overlay
**Files:** HoverCard.razor, HoverCardTrigger.razor, HoverCardContent.razor

## Contract — WARN
- HoverCard.razor: missing `[Parameter] public string? Class { get; set; }` (root component has no Class param)
- HoverCard.razor: root `<div>` uses `@attributes` but no `Class` interpolation
- HoverCardContent.razor: IAsyncDisposable present, JSDisconnectedException caught, ComponentInteropService used — OK
- No raw color literals, no `dark:` prefix

## API — WARN
- Overlay class requires `Open`/`OpenChanged` — uses `IsOpen`/`IsOpenChanged` instead (OK, different naming convention)
- Missing `OnOpen`, `OnClose`, `Disabled` callbacks per overlay spec

## Bugs — OK
- No `async void`, no direct IJSRuntime, no discarded Tasks in lifecycle methods
- OnAfterRenderAsync in HoverCardContent calls Interop only when `Context.IsOpen` changes (not unconditionally), but check is not inside `firstRender` guard — WARN: interop runs on every re-render when open

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/HoverCardPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present
- Files declared: 3 of 3
- Missing from registry: none
- Component deps declared: OK (none referenced)
