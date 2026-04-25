# Slider

**Path:** `src/Lumeo/UI/Slider/`
**Class:** Form input
**Files:** Slider.razor

## Contract — OK
- `@namespace Lumeo` as first line. `Class` + `AdditionalAttributes` present.
- `@attributes="AdditionalAttributes"` on all root div branches.
- No raw color literals. No `dark:` prefix.
- No icon components.

## API — WARN
- Form input class: `Value`+`ValueChanged`, `Disabled`, `Min`, `Max`, `Step` present.
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`, `Placeholder` (text input params not needed), `ReadOnly`.
- Has `IsRange`, `ValueEnd`+`ValueEndChanged`, `AriaLabel`, `ShowTooltip`, `Marks`, `ShowTicks`, `FormatTooltip`, `Culture`, `Orientation`.

## Bugs — OK
- No JS interop. No event subscriptions. Pure Blazor range input.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/SliderPage.razor` (exists)
- 10 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`slider`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: none — correct
