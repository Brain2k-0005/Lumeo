# Timeline

**Path:** `src/Lumeo/UI/Timeline/`
**Class:** Other (Data Display)
**Files:** Timeline.razor, TimelineItem.razor

## Contract — OK
- All checks pass for both files.
- No raw color literals. No `dark:` prefix. No direct IJSRuntime.
- Both files have `@namespace Lumeo`, `Class`, `AdditionalAttributes`, and `@attributes="AdditionalAttributes"`.

## API — OK
- `ChildContent`, `Orientation`, `Animated`, `ActiveIndex`, `ActiveIndexChanged`, `Class`, `AdditionalAttributes` all present.
- TimelineItem: `Title`, `Description`, `Time`, `IconContent`, `IsActive`, `Class`, `AdditionalAttributes` all present.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/TimelinePage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (missing from ComponentsIndex)

## CLI — OK
- Registry entry: present (`timeline`)
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: OK (none required)
