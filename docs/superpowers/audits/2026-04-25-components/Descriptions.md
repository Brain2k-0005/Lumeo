# Descriptions

**Path:** `src/Lumeo/UI/Descriptions/`
**Class:** Display
**Files:** Descriptions.razor, DescriptionsItem.razor

## Contract — OK
- All checks pass. Both files: `@namespace Lumeo`, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root.
- No raw colors, no `dark:` prefixes.
- No icons.

## API — OK
- Display class; `Size`/`Variant` not applicable.
- Present: `ChildContent`, `Title`, `Bordered`, `Column`, `Class`, `AdditionalAttributes` (Descriptions).
- DescriptionsItem: `ChildContent`, `Label`, `Class`, `AdditionalAttributes`.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/DescriptionsPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`descriptions`)
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: OK (none needed)
