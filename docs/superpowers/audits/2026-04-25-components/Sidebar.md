# Sidebar

**Path:** `src/Lumeo/UI/Sidebar/`
**Class:** Container
**Files:** SidebarComponent.razor, SidebarContent.razor, SidebarFooter.razor, SidebarGroup.razor, SidebarGroupLabel.razor, SidebarHeader.razor, SidebarMenu.razor, SidebarMenuButton.razor, SidebarMenuItem.razor, SidebarProvider.razor, SidebarSeparator.razor, SidebarTrigger.razor

## Contract — OK
- All 12 files have `@namespace Lumeo` as first line.
- `Class` + `AdditionalAttributes` present in all files reviewed.
- `@attributes="AdditionalAttributes"` on root elements.
- No raw color literals. No `dark:` prefix.
- Icons via `<Blazicon Svg="Lucide.*" />` in SidebarTrigger.
- No overlay JS interop; no `IAsyncDisposable` needed.

## API — OK
- Container class: `ChildContent` present everywhere. `SidebarProvider` has `IsCollapsed`+`IsCollapsedChanged`, `Variant`.
- `SidebarMenuButton` has `Href`, `IsActive`, `IconContent`, `LabelContent`, `Tooltip`.

## Bugs — OK
- No JS interop in lifecycle methods. No async void. No event subscriptions.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/SidebarPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`sidebar`)
- Files declared: 12 of 12
- Missing from registry: none
- Component deps declared: none — missing `Blazicons.Lucide` packageDependency (structural gap)
