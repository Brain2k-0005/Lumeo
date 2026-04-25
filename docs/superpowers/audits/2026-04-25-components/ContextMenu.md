# ContextMenu

**Path:** `src/Lumeo/UI/ContextMenu/`
**Class:** Overlay
**Files:** ContextMenu.razor, ContextMenuCheckboxItem.razor, ContextMenuContent.razor, ContextMenuGroup.razor, ContextMenuItem.razor, ContextMenuLabel.razor, ContextMenuRadioGroup.razor, ContextMenuRadioItem.razor, ContextMenuSeparator.razor, ContextMenuTrigger.razor

## Contract — WARN
- `ContextMenu.razor` (root) is missing `[Parameter] public string? Class { get; set; }` — the wrapper div has no class binding.
- All other files have `Class` param.
- Overlay checks: `ContextMenuContent` implements `IAsyncDisposable`, catches `JSDisconnectedException`, uses `ComponentInteropService`. OK.

## API — WARN
- `IsOpen` + `IsOpenChanged` present on root.
- Missing: `OnOpen`, `OnClose`, `Disabled` (overlay API requirements).

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ContextMenuPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (key `context-menu`)
- Files declared: 10 of 10
- Missing from registry: none
- Component deps declared: OK (none required)
