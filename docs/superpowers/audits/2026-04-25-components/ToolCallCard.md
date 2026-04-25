# ToolCallCard

**Path:** `src/Lumeo/UI/ToolCallCard/`
**Class:** Display
**Files:** ToolCallCard.razor

## Contract — WARN
- Uses `bg-emerald-500` and `text-emerald-500` Tailwind utility classes (hardcoded Tailwind color, not a CSS variable). Violates "no raw color literals" convention — should use a CSS variable (e.g., `bg-success`).
- Uses `<Icon>` component (not `<Blazicon>`), which is an internal Lumeo component.
- No `dark:` prefix. No raw hex/rgb. `Class` and `AdditionalAttributes` present.
- Registry lists `icon` and `spinner` as deps — correct.

## API — OK
- Display component: `ToolName`, `Status`, `Input`, `Output`, `ErrorMessage`, `DurationMs`, `DefaultOpen`, `Class`, `AdditionalAttributes` — appropriate for this component type.

## Bugs — OK
- No findings.

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/ToolCallCardPage.razor` (MISSING)
- 0 ComponentDemo blocks
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`tool-call-card`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (`icon`, `spinner` listed)
