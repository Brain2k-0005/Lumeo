# Statistic

**Path:** `src/Lumeo/UI/Statistic/`
**Class:** Display
**Files:** Statistic.razor

## Contract — OK
- All checks pass. No raw hex/hsl. No `dark:` prefix. Uses `<Blazicon>` for trend icons.

## API — WARN
- Display class: `Size` and `Variant` parameters absent (not applicable for a pure metric display, but checklist requires them where applicable).
- Core data params present: `Title`, `Value`, `Prefix`, `Suffix`, `Precision`, `ShowTrend`, `TrendValue`, `TrendDirection`, `Culture`.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/StatisticPage.razor` (exists)
- 2 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — WARN
- Registry entry: present (`statistic`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: missing `Blazicons.Lucide` package dep (registry-gen does not emit packageDependencies)
