# Popover

**Path:** `src/Lumeo/UI/Popover/`
**Class:** Overlay / Container
**Files:** Popover.razor, PopoverContent.razor, PopoverTrigger.razor

## Contract — WARN
- Popover.razor: missing `[Parameter] public string? Class { get; set; }` — root `<div>` has no class customization.
- PopoverContent.razor and PopoverTrigger.razor: have `Class`, `AdditionalAttributes`, `@attributes`. OK.
- No raw color literals. No `dark:` prefix.
- PopoverContent implements IAsyncDisposable, catches JSDisconnectedException, uses ComponentInteropService. OK.

## API — WARN
- Overlay class: `IsOpen` + `IsOpenChanged` present on Popover.razor. Good.
- Missing: `OnOpen`, `OnClose`, `Disabled`.
- `Class` missing from Popover.razor wrapper component.

## Bugs — OK
- PopoverContent.OnAfterRenderAsync: JS calls guarded by `if (Context.IsOpen && !_registered)` (not `firstRender`) — consistent with reactive open/close pattern used throughout library.
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/PopoverPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (Overlay group)

## CLI — OK
- Registry entry: present
- Files declared: 3 of 3
- Missing from registry: none
- Component deps declared: none (none referenced)
