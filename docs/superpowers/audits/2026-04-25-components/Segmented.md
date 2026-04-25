# Segmented

**Path:** `src/Lumeo/UI/Segmented/`
**Class:** Other
**Files:** Segmented.razor

## Contract — OK
- `@namespace Lumeo` as first line. `Class` + `AdditionalAttributes` present.
- `@attributes="AdditionalAttributes"` on root div.
- No raw color literals. No `dark:` prefix.
- No icons used (icon slot in SegmentedOption is a RenderFragment).

## API — OK
- Has `Value`+`ValueChanged`, `Options` (List<SegmentedOption>), `Block`.
- `SegmentedOption` supports `Disabled`, `Label`, `Value`, `IconContent`.
- Not a traditional form input (no `Invalid`, `ErrorText` etc.) — acceptable for this component type.

## Bugs — OK
- No async void, no JS interop, no event subscriptions needing disposal.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/SegmentedPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`segmented`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: none — correct
