# Skeleton

**Path:** `src/Lumeo/UI/Skeleton/`
**Class:** Display
**Files:** Skeleton.razor, SkeletonCard.razor, SkeletonCircle.razor, SkeletonText.razor

## Contract — WARN
- All 4 files: `@namespace Lumeo` as first line. `Class` + `AdditionalAttributes` present.
- `@attributes="AdditionalAttributes"` on root elements.
- No `dark:` prefix.
- Skeleton.razor and SkeletonCircle.razor contain inline `<style>` blocks with `@@keyframes skeleton-wave` — not a raw color literal, acceptable.
- Wave branch uses inline `style="animation: skeleton-wave 1.5s ease-in-out infinite"` — not a color literal.

## API — OK
- Display class: `Animation` enum (Pulse/Wave/None) present. `Size` not applicable for this primitive.
- SkeletonCard, SkeletonText, SkeletonCircle are preset composites.
- SkeletonText has `Lines` parameter.

## Bugs — OK
- No JS interop, no lifecycle issues.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/SkeletonPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`skeleton`)
- Files declared: 4 of 4
- Missing from registry: none
- Component deps declared: none — correct (no external component refs)
