# Splitter

**Path:** `src/Lumeo/UI/Splitter/`
**Class:** Container
**Files:** Splitter.razor, SplitterDivider.razor, SplitterPane.razor

## Contract — OK
- All three files have `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes="AdditionalAttributes"`.
- `ComponentInteropService` used via `@inject`; no direct `IJSRuntime`.
- No raw hex/hsl literals. No `dark:` prefixes.
- Splitter.razor and SplitterPane.razor implement `IDisposable` (sync); SplitterDivider implements `IDisposable`.
- Not an overlay component — IAsyncDisposable/JSDisconnectedException not required.

## API — OK
- Container class: has `ChildContent` on all three; `Orientation` serves as Size/config.

## Bugs — OK
- No findings. `OnAfterRender` (not async) calls `DistributeInitialSizes()` which is sync — no JS interop inside `OnAfterRender`.
- `ResizeNeighbors` is async and called via context callback (not a lifecycle hook).

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/SplitterPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`splitter`)
- Files declared: 3 of 3
- Missing from registry: none
- Component deps declared: OK (none needed)
