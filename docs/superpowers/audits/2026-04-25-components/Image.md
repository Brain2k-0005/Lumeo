# Image

**Path:** `src/Lumeo/UI/Image/`
**Class:** Display
**Files:** Image.razor, ImageGallery.razor

## Contract — OK
- Both files: `@namespace Lumeo` as first line
- Both have `Class` and `AdditionalAttributes` params
- Both carry `@attributes="AdditionalAttributes"` on root element
- Image.razor and ImageGallery.razor implement IAsyncDisposable, catch JSDisconnectedException, use ComponentInteropService
- No raw color literals; `bg-black/80` uses Tailwind opacity modifier (not raw hex)
- No `dark:` prefix
- Icons via Blazicons (`<Blazicon Svg="Lucide.X">` etc.)

## API — OK
- Display class: Size not applicable (uses Width/Height directly); Variant not applicable
- Core params: Src, Alt, Width, Height, Lazy, Fallback, Preview all present

## Bugs — OK
- No `async void`, no direct IJSRuntime
- LockScroll/UnlockScroll via ComponentInteropService
- Dispose catches JSDisconnectedException

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ImagePage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no — Image not listed

## CLI — OK
- Registry entry: present
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: OK (none declared; Blazicons.Lucide structural gap)
