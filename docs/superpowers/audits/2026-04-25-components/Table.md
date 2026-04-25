# Table

**Path:** `src/Lumeo/UI/Table/`
**Class:** Other
**Files:** Table.razor, TableBody.razor, TableCaption.razor, TableCell.razor, TableHead.razor, TableHeader.razor, TableRow.razor

## Contract — OK
- All 7 files have `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes="AdditionalAttributes"`.
- No raw hex/hsl. No `dark:` prefix. No icons.

## API — OK
- For Other class: all sub-components expose `ChildContent`, `Class`, `AdditionalAttributes`. Structural HTML table decomposition matches shadcn/ui Table pattern.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/TablePage.razor` (exists)
- 8 ComponentDemo blocks (via grep)
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (Data Display group)

## CLI — OK
- Registry entry: present (`table`)
- Files declared: 7 of 7
- Missing from registry: none
- Component deps declared: OK (none needed)
