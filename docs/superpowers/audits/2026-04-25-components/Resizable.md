# Resizable

**Path:** `src/Lumeo/UI/Resizable/`
**Class:** Container
**Files:** ResizableHandle.razor, ResizablePanel.razor, ResizablePanelGroup.razor

## Contract — OK
- All three files: `@namespace Lumeo` as first line.
- `Class` + `AdditionalAttributes` present in all three files.
- `@attributes="AdditionalAttributes"` on root elements.
- No raw color literals. No `dark:` prefix.
- Icons via `<Blazicon Svg="Lucide.GripVertical/GripHorizontal" />` in ResizableHandle.
- `IAsyncDisposable` implemented in ResizablePanelGroup and ResizableHandle.
- `JSDisconnectedException` caught in ResizableHandle.DisposeAsync.
- Uses `ComponentInteropService`, no direct `IJSRuntime`.

## API — OK
- Container class: `ChildContent` present in all sub-components; `DefaultSizes`, `Direction` on group.
- Resize constraints: `MinSize`, `MaxSize`, `DefaultSize` on panel.

## Bugs — OK
- JS interop in `OnAfterRenderAsync` guarded by `if (firstRender)` in ResizableHandle.
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ResizablePage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — WARN
- Registry entry: present (`resizable`)
- Files declared: 3 of 3 (.razor files all listed)
- Missing from registry: none (no .js/.css in dir)
- Component deps declared: none declared — registry-gen does not emit component deps; ResizablePanelGroup uses `ComponentInteropService.RegisterResizeHandle` but no component dep needed
