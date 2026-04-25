# Alert

**Path:** `src/Lumeo/UI/Alert/`
**Class:** Display
**Files:** Alert.razor

## Contract — OK
- All checks pass.

## API — WARN
- Display class: Size param absent (not critical for Alert but checklist flags it).
- Variant present. All practical params present (Title, Description, IsDismissible, OnDismiss, ShowIcon, AutoDismissMs, IconContent, Class).

## Bugs — OK
- Timer disposed in Dispose(). AutoDismissCallback uses InvokeAsync correctly.
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/AlertPage.razor` (exists)
- 7 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (uses Blazicon inline, no declared component dep needed per registry-gen gap)
