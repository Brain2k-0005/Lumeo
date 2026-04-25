# Icon

**Path:** `src/Lumeo/UI/Icon/`
**Class:** Display
**Files:** Icon.razor

## Contract — OK
- First line: `@namespace Lumeo`
- Has `[Parameter] public string? Class { get; set; }`
- Has `[Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }`
- Root element `<Blazicon>` carries `@attributes="AdditionalAttributes"`
- No raw color literals, no `dark:` prefix
- Uses Blazicons (`<Blazicon Svg=...>`) — correct

## API — OK
- Display class: Size param present, Variant not applicable (uses Name/Svg instead)
- All class-required parameters present for a wrapper/utility icon component

## Bugs — OK
- No `async void`, no direct IJSRuntime, no lifecycle JS interop
- No event subscriptions, no discarded Tasks

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/IconPage.razor` (exists)
- Multiple usage demos (tabs with ComponentDemo-equivalent sections)
- API Reference: present (section id="api-reference")
- Indexed in ComponentsIndex.razor: no — Icon not listed in ComponentsIndex

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none — Blazicons.Lucide not declared; structural gap in generator)
