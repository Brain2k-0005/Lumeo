# Dialog

**Path:** `src/Lumeo/UI/Dialog/`
**Class:** Overlay
**Files:** Dialog.razor, DialogClose.razor, DialogContent.razor, DialogDescription.razor, DialogFooter.razor, DialogHeader.razor, DialogTitle.razor, DialogTrigger.razor

## Contract — OK
- All files: `@namespace Lumeo`, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root.
- DialogContent: `@implements IAsyncDisposable`, catches `JSDisconnectedException`, uses `ComponentInteropService` (no direct IJSRuntime).
- No raw colors, no `dark:` prefixes.
- Icons via `<Blazicon>` (Lucide.X).

## API — WARN
- Overlay required params: `IsOpen`+`IsOpenChanged` present (named `IsOpen` not `Open` — minor naming drift vs checklist spec).
- Missing: `OnOpen`, `OnClose`, `Disabled` callbacks on `Dialog.razor`.

## Bugs — OK
- No `async void`. JS interop in `OnAfterRenderAsync` guarded by `_wasOpen` state change logic (equivalent to firstRender guard).
- No direct IJSRuntime.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/DialogPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`dialog`)
- Files declared: 8 of 8
- Missing from registry: none
- Component deps declared: OK (none listed — Blazicons.Lucide implicit; generator gap noted globally)
