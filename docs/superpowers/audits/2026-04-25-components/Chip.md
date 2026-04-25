# Chip

**Path:** `src/Lumeo/UI/Chip/`
**Class:** Display
**Files:** Chip.razor, ChipGroup.razor

## Contract — OK
- All checks pass.
- Uses `IComponentInteropService` via `@inject Lumeo.Services.IComponentInteropService Interop` (interface, not concrete class — slightly different from other components that inject `ComponentInteropService` directly, but still goes through the service).

## API — OK
- `Size` (ChipSize enum), `Variant` (ChipVariant enum), `Closable`, `OnClose`, `Clickable`, `OnClick`, `Class` all present.
- No `Disabled` — minor gap for a Display component (closable chips cannot be disabled).

## Bugs — OK
- No findings.

## Docs — WARN
- Page: `docs/Lumeo.Docs/Pages/Components/ChipPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (key `chip`)
- Files declared: 2 of 2
- Missing from registry: none
- Component deps declared: OK (none required; Blazicons.Lucide not declared in registry — structural gap noted once in preamble)
