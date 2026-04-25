# Sortable

**Path:** `src/Lumeo/UI/Sortable/`
**Class:** Other
**Files:** SortableList.razor

## Contract — WARN
- `@namespace Lumeo` as first line. `Class` + `AdditionalAttributes` present.
- `@attributes="AdditionalAttributes"` on root div.
- No raw color literals. No `dark:` prefix.
- Contains inline `<svg>` grip-dots icon (6 circles, >3 lines) instead of `<Blazicon>` — violates icons-via-Blazicons convention.

## API — OK
- Has `Items`+`ItemsChanged` (generic `List<TItem>`), `ItemTemplate`, `Handle`, `Disabled`, `Group`.
- Covers the key API surface for a drag-drop list.

## Bugs — OK
- Pure Blazor drag events, no JS interop. No event subscription leaks.

## Docs — WARN
- Page: `docs/Lumeo.Docs/Pages/Components/SortableListPage.razor` (exists — note: filename is SortableList not Sortable)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — WARN
- Registry entry: present (`sortable`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: `list` — ambiguous, no `<List>` component used directly in SortableList.razor
