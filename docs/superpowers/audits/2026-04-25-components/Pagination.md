# Pagination

**Path:** `src/Lumeo/UI/Pagination/`
**Class:** Other (Navigation)
**Files:** Pagination.razor, PaginationContent.razor, PaginationEllipsis.razor, PaginationItem.razor, PaginationNext.razor, PaginationPrevious.razor

## Contract — OK
- All files have `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes`.
- No raw color literals. No `dark:` prefix.
- Uses `<Blazicon>` in PaginationNext, PaginationPrevious, PaginationEllipsis.

## API — OK
- Navigation utility component. PaginationItem has `IsActive` + `OnClick`. PaginationNext/Previous have `Disabled` + `OnClick`. Pagination has `ChildContent`.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/PaginationPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (Navigation group)

## CLI — OK
- Registry entry: present
- Files declared: 6 of 6
- Missing from registry: none
- Component deps declared: none (none referenced)
