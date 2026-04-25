# Drawer

**Path:** `src/Lumeo/UI/Drawer/`
**Class:** Overlay
**Files:** Drawer.razor, DrawerClose.razor, DrawerContent.razor, DrawerDescription.razor, DrawerFooter.razor, DrawerHeader.razor, DrawerTitle.razor, DrawerTrigger.razor

## Contract — OK
- All files: `@namespace Lumeo`, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root.
- DrawerContent: `@implements IAsyncDisposable`, catches `JSDisconnectedException`, uses `ComponentInteropService`.
- No raw colors, no `dark:` prefixes.
- No icon usages in sub-components.

## API — WARN
- `IsOpen`+`IsOpenChanged` present (naming drift from `Open`+`OpenChanged` spec — minor).
- Missing: `OnOpen`, `OnClose`, `Disabled` on root `Drawer.razor`.

## Bugs — OK
- No `async void`. JS interop in `OnAfterRenderAsync` guarded by `_wasOpen` state logic.
- No direct IJSRuntime.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/DrawerPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`drawer`)
- Files declared: 8 of 8
- Missing from registry: none
- Component deps declared: OK (none listed)
