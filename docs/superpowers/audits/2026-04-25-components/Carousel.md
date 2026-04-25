# Carousel

**Path:** `src/Lumeo/UI/Carousel/`
**Class:** Container
**Files:** Carousel.razor, CarouselContent.razor, CarouselItem.razor, CarouselNext.razor, CarouselPrevious.razor

## Contract — OK
- All checks pass.
- Implements `IAsyncDisposable`; catches `JSDisconnectedException`; uses `ComponentInteropService`.

## API — WARN
- `ChildContent` present; `Class` present on all files.
- Missing `Disabled` parameter on root Carousel (not a strict Container requirement but noted).

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/CarouselPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (key `carousel`)
- Files declared: 5 of 5
- Missing from registry: none
- Component deps declared: OK (none required)
