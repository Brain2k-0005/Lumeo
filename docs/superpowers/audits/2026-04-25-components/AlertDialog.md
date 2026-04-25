# AlertDialog

**Path:** `src/Lumeo/UI/AlertDialog/`
**Class:** Overlay
**Files:** AlertDialog.razor, AlertDialogAction.razor, AlertDialogCancel.razor, AlertDialogContent.razor, AlertDialogDescription.razor, AlertDialogFooter.razor, AlertDialogHeader.razor, AlertDialogTitle.razor, AlertDialogTrigger.razor

## Contract — WARN
- AlertDialog.razor (root): no `Class` param, no `AdditionalAttributes`, no `@attributes` — root is a `<CascadingValue>` with no element, so this is acceptable by design.
- AlertDialogContent.razor: IAsyncDisposable OK, JSDisconnectedException caught, ComponentInteropService used.
- All sub-components have Class + AdditionalAttributes + @attributes.

## API — WARN
- Overlay class: `Open`/`OpenChanged` naming convention — component uses `IsOpen`/`IsOpenChanged` instead. Functional equivalent but deviates from standard naming.
- `OnOpen`, `OnClose`, `Disabled` params absent on root AlertDialog.razor.

## Bugs — OK
- No findings. Cleanup is guarded, focus trap and scroll lock properly managed.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/AlertDialogPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present
- Files declared: 9 of 9
- Missing from registry: none
- Component deps declared: spinner (OK)
