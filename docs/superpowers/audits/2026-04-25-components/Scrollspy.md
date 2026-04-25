# Scrollspy

**Path:** `src/Lumeo/UI/Scrollspy/`
**Class:** Other
**Files:** Scrollspy.razor, ScrollspyLink.razor, ScrollspySection.razor

## Contract — OK
- `@namespace Lumeo` as first line in all three files.
- `Class` + `AdditionalAttributes` present in all files.
- `@attributes="AdditionalAttributes"` on root elements.
- No raw color literals. No `dark:` prefix.
- No icons used.
- `IAsyncDisposable` implemented in Scrollspy.razor.
- `JSDisconnectedException` caught in `DisposeAsync`.
- Uses `ComponentInteropService`, no direct `IJSRuntime`.

## API — OK
- Other class: `ActiveId`+`ActiveIdChanged`, `Offset`, `Smooth` — reasonable API for scrollspy.
- ScrollspyLink has `Target` (EditorRequired). ScrollspySection has `Id` (EditorRequired).

## Bugs — OK
- `OnAfterRenderAsync` guarded by `if (firstRender)`. No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ScrollspyPage.razor` (exists)
- 2 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`scrollspy`)
- Files declared: 3 of 3
- Missing from registry: none
- Component deps declared: none — correct (no sub-component references)
