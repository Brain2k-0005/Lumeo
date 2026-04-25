# MegaMenu

**Path:** `src/Lumeo/UI/MegaMenu/`
**Class:** Overlay
**Files:** MegaMenu.razor, MegaMenuGroup.razor, MegaMenuPanel.razor, MegaMenuLink.razor, MegaMenuItem.razor

## Contract — WARN
- All 5 files: `@namespace Lumeo` present
- All have `Class` and `AdditionalAttributes` params
- All carry `@attributes="AdditionalAttributes"` on root element
- MegaMenuItem.razor: `InvokeAsync(async () => ...)` called without `await` in `HandleMouseEnter` (line 83) — discarded Task
- No raw color literals, no `dark:` prefix
- Icons via Blazicons (`<Blazicon Svg="@Icon">`, `<Blazicon Svg="Lucide.ChevronDown">`)
- Overlay checks: IAsyncDisposable NOT implemented (MegaMenuItem only has IDisposable); no ComponentInteropService used (pure Blazor hover state — acceptable for this design)

## API — WARN
- Overlay class: no `Open`/`OpenChanged` pair (state managed internally via CascadingValue context); `Disabled` present in MegaMenuItem
- Missing `OnOpen`, `OnClose` callbacks per overlay spec

## Bugs — WARN
- MegaMenuItem.HandleMouseEnter: `InvokeAsync(async () => ...)` without `await` — discarded Task, potential unobserved exception

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/MegaMenuPage.razor` (exists)
- 2 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 5 of 5
- Missing from registry: none
- Component deps declared: OK (none; Blazicons structural gap)
