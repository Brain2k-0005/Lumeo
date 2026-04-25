# Sparkles

**Path:** `src/Lumeo/UI/Sparkles/`
**Class:** Other
**Files:** Sparkles.razor

## Contract — WARN
- Inline `style` attribute on sparkle elements uses `color:@Color;` — Color defaults to `var(--color-primary)` (CSS var, OK), but consumers can pass raw hex/color literals via the Color param; no raw literals baked into the component source itself.
- No `dark:` prefix found.
- No raw hex literals in source.

## API — OK
- For Other class: has `ChildContent`, `Count`, `MinSize`, `MaxSize`, `Color`, `Class`, `AdditionalAttributes`.

## Bugs — OK
- No findings.

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/SparklesPage.razor` (MISSING)
- 0 ComponentDemo blocks
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`sparkles`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none needed)
