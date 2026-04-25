# Sheet

**Path:** `src/Lumeo/UI/Sheet/`
**Class:** Overlay / Container
**Files:** Sheet.razor, SheetClose.razor, SheetContent.razor, SheetDescription.razor, SheetFooter.razor, SheetHeader.razor, SheetTitle.razor, SheetTrigger.razor

## Contract — OK
- All 8 files have `@namespace Lumeo` as first line.
- `Class` + `AdditionalAttributes` present in all files.
- `@attributes="AdditionalAttributes"` on root elements (SheetContent: on dialog div).
- No raw color literals. No `dark:` prefix.
- Icons via `<Blazicon Svg="Lucide.X" />` in SheetContent close button.
- `IAsyncDisposable` implemented in SheetContent.
- `JSDisconnectedException` caught in SheetContent.Cleanup.
- Uses `ComponentInteropService` — correct, no direct `IJSRuntime`.
- Scroll lock + focus trap properly managed.

## API — WARN
- Overlay class: `IsOpen`+`IsOpenChanged` present (naming differs from `Open`/`OpenChanged` convention).
- Missing: `OnOpen`, `OnClose`, `Disabled` callbacks on root Sheet.

## Bugs — OK
- `OnAfterRenderAsync` uses `_wasOpen` flag pattern (not firstRender) — safe.
- No async void, no event leaks.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/SheetPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`sheet`)
- Files declared: 8 of 8
- Missing from registry: none
- Component deps declared: none — missing `Blazicons.Lucide` packageDependency (structural gap)
