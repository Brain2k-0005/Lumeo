# Filter

**Path:** `src/Lumeo/UI/Filter/`
**Class:** Other
**Files:** FilterBar.razor, FilterPill.razor

## Contract — OK
- Both files: `@namespace Lumeo`, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root.
- No raw colors, no `dark:` prefixes.
- FilterPill: icon via `<Blazicon>` (Lucide.X).

## API — OK
- Other class; judgement-based.
- FilterBar: `ChildContent`, `Pills`, `Actions`, `Class`, `AdditionalAttributes` — reasonable slot design.
- FilterPill: `Label`, `Value`, `OnDismiss`, `Class`, `AdditionalAttributes` — covers core chip needs.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/FilterPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`filter`)
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: OK (badge listed)
