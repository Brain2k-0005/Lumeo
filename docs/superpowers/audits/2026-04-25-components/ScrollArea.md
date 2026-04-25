# ScrollArea

**Path:** `src/Lumeo/UI/ScrollArea/`
**Class:** Container
**Files:** ScrollArea.razor

## Contract — OK
- `@namespace Lumeo` as first line. `Class` + `AdditionalAttributes` present.
- `@attributes="AdditionalAttributes"` on root div.
- No raw color literals. No `dark:` prefix.
- No icons used.

## API — WARN
- Container class: `ChildContent` present. No `Size` parameter (likely unnecessary for a scroll wrapper).
- Scrollbar styling is CSS-only (webkit scrollbar pseudo-elements) — no JS interop needed.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ScrollAreaPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (as "Scroll Area")

## CLI — OK
- Registry entry: present (`scroll-area`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: none — correct, no dependencies
