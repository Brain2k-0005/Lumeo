# Kanban

**Path:** `src/Lumeo/UI/Kanban/`
**Class:** Other
**Files:** Kanban.razor, KanbanColumn.razor, KanbanCard.razor

## Contract — WARN
- All 3 files: `@namespace Lumeo` present
- All have `Class` and `AdditionalAttributes` params
- All carry `@attributes="AdditionalAttributes"` on root element
- KanbanColumn.razor: inline SVG `<svg>` (plus icon for Add Card button, ~5 lines) — should use Blazicons
- KanbanCard.razor: no raw color literals, no inline SVG
- No raw hex/rgb/hsl, no `dark:` prefix

## API — OK
- Other class: judgement — Kanban has ChildContent; KanbanColumn has Title, Badge, AllowAdd, OnAdd, HeaderContent, OnDrop; KanbanCard has Title, Description, Labels, Avatar, Draggable, OnClick
- Reasonable API for a kanban board component relative to shadcn/MudBlazor equivalents

## Bugs — OK
- No `async void`, no direct IJSRuntime, no JS interop at all
- No discarded Tasks in lifecycle
- Drag-and-drop handled via native HTML drag events

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/KanbanPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 3 of 3
- Missing from registry: none
- Component deps declared: OK (none declared; Delta component not used internally)
