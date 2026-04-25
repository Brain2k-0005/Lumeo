# ToggleGroup

**Path:** `src/Lumeo/UI/ToggleGroup/`
**Class:** Trigger
**Files:** ToggleGroup.razor, ToggleGroupItem.razor

## Contract — OK
- Both files pass all checks.
- `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes` on root — present in both.
- No raw color literals. No `dark:` prefix.
- `ToggleGroupItem` uses `[Inject] Lumeo.Services.IComponentInteropService Interop` (interface, not IJSRuntime) — OK.
- `JSDisconnectedException` caught in `ToggleGroupItem.OnAfterRenderAsync` — OK.

## API — OK
- `Type`, `Variant`, `Size`, `Value` + `ValueChanged`, `SelectedValues` + `SelectedValuesChanged`, `Disabled` (on item), `ChildContent` — all present.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ToggleGroupPage.razor` (exists)
- 6 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (`toggle-group`)

## CLI — OK
- Registry entry: present (`toggle-group`)
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: OK (none listed; Button.ButtonPressEffect referenced but Button is a sibling)
