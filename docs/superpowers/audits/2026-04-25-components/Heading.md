# Heading

**Path:** `src/Lumeo/UI/Heading/`
**Class:** Display
**Files:** Heading.razor

## Contract — OK
- `@namespace Lumeo`, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root element.
- No raw colors, no `dark:` prefixes.
- No icons.

## API — OK
- Display class; `Variant` not applicable (uses `Level` int instead).
- Present: `Level`, `Size`, `Weight`, `Tracking`, `ChildContent`, `Class`, `AdditionalAttributes`.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/HeadingPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`heading`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none needed)
