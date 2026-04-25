# ReasoningDisplay

**Path:** `src/Lumeo/UI/ReasoningDisplay/`
**Class:** Display
**Files:** ReasoningDisplay.razor

## Contract — WARN
- Missing `[Parameter(CaptureUnmatchedValues = true)]` — present. All standard params present.
- Uses `<Icon Name="ChevronRight" .../>` component (not Blazicons.Lucide) — minor inconsistency with convention.
- No raw color literals. No `dark:` prefix.

## API — WARN
- Display class: `Size` and `Variant` parameters absent (niche component, tolerable).
- Has `Text`, `IsStreaming`, `Summary`, `DurationMs`, `DefaultOpen`, `Class`, `AdditionalAttributes`.

## Bugs — OK
- No findings.

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/ReasoningDisplayPage.razor` (MISSING)
- 0 ComponentDemo blocks
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — WARN
- Registry entry: present (`reasoning-display`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: `icon` — OK (uses Icon component); no Blazicons dep needed
