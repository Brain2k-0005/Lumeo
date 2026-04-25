# DropdownMenu

**Path:** `src/Lumeo/UI/DropdownMenu/`
**Class:** Overlay
**Files:** DropdownMenu.razor, DropdownMenuCheckboxItem.razor, DropdownMenuContent.razor, DropdownMenuGroup.razor, DropdownMenuItem.razor, DropdownMenuLabel.razor, DropdownMenuRadioGroup.razor, DropdownMenuRadioItem.razor, DropdownMenuSeparator.razor, DropdownMenuTrigger.razor

## Contract — OK
- All files: `@namespace Lumeo`, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root.
- DropdownMenuContent: `@implements IAsyncDisposable`, catches `JSDisconnectedException`, uses `ComponentInteropService`.
- No raw colors, no `dark:` prefixes.
- No inline SVG > 3 lines.

## API — WARN
- `IsOpen`+`IsOpenChanged` present on root (naming drift vs spec `Open`+`OpenChanged` — minor).
- Missing: `OnOpen`, `OnClose`, `Disabled` on root `DropdownMenu.razor`.

## Bugs — OK
- No `async void`. JS interop in `OnAfterRenderAsync` guarded by `_registered` flag.
- No direct IJSRuntime.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/DropdownMenuPage.razor` (exists)
- 6 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`dropdown-menu`)
- Files declared: 10 of 10
- Missing from registry: none
- Component deps declared: OK (none listed — all self-contained)
