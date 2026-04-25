# RadioGroup

**Path:** `src/Lumeo/UI/RadioGroup/`
**Class:** Form input
**Files:** RadioGroup.razor, RadioGroupCard.razor, RadioGroupItem.razor

## Contract — OK
- All files have `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes`.
- No raw color literals. No `dark:` prefix. No Blazicon SVGs.

## API — WARN
- Has `Value` + `ValueChanged`. Good.
- Missing: `Disabled` (on RadioGroup root — items have it individually), `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`.
- RadioGroupCard lacks `Disabled` parameter.

## Bugs — OK
- No async void, no discarded Tasks.
- ComponentInteropService used in RadioGroupItem/RadioGroupCard for `FocusElement` — correct.
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/RadioGroupPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (Form group, "radio-group")

## CLI — OK
- Registry entry: present
- Files declared: 3 of 3
- Missing from registry: none
- Component deps declared: none (none referenced)
