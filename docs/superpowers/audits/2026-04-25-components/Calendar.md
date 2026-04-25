# Calendar

**Path:** `src/Lumeo/UI/Calendar/`
**Class:** Form input
**Files:** Calendar.razor

## Contract — OK
- All checks pass.

## API — WARN
- Form input class: `Value`+`ValueChanged` present. `Disabled` param absent (uses `IsDateDisabled` Func instead — functional but deviates from convention). `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name` absent (Calendar is a standalone picker, not a form field — omission is arguable but flag per checklist).

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/CalendarPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (no component deps declared; uses Blazicon internally — registry-gen gap, not per-component issue)
