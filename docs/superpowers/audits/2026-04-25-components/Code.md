# Code

**Path:** `src/Lumeo/UI/Code/`
**Class:** Display
**Files:** Code.razor

## Contract — OK
- All checks pass.

## API — OK
- `Variant` (inline/block), `Size`, `ChildContent`, `Class` present.
- No `Variant` enum — uses raw string (`"inline"` / `"block"`); works but not type-safe.

## Bugs — OK
- No findings.

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/CodePage.razor` (MISSING)
- 0 ComponentDemo blocks
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (key `code`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none required)
