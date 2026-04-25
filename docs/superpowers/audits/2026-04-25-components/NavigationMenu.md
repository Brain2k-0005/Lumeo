# NavigationMenu

**Path:** `src/Lumeo/UI/NavigationMenu/`
**Class:** Other (Navigation)
**Files:** NavigationMenu.razor, NavigationMenuContent.razor, NavigationMenuHamburger.razor, NavigationMenuIndicator.razor, NavigationMenuItem.razor, NavigationMenuLink.razor, NavigationMenuList.razor, NavigationMenuMobile.razor, NavigationMenuTrigger.razor, NavigationMenuViewport.razor

## Contract — OK
- All files have `@namespace Lumeo`, `Class`, `AdditionalAttributes`, `@attributes`.
- NavigationMenuContent implements IAsyncDisposable, catches JSDisconnectedException, uses ComponentInteropService.
- NavigationMenuItem implements IDisposable (not IAsyncDisposable) — uses sync dispose for DelayedDispatch; no async cleanup needed so this is acceptable.

## API — OK
- Composite nav component. Overlay sub-component (NavigationMenuContent) has IsOpen-equivalent (derived from CascadingParameter context), uses ComponentInteropService.
- NavigationMenuHamburger has IsOpen + IsOpenChanged (two-way binding).
- NavigationMenuMobile wraps Sheet with IsOpen/IsOpenChanged.
- Registry dep `sheet` correctly declared.

## Bugs — WARN
- NavigationMenuItem.HandleMouseLeave: `_ = InvokeAsync(async () => { … })` inside a lambda scheduled by DelayedDispatch — Task discarded pattern, but this is intentional fire-and-forget from a non-async context in timer callback; low risk.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/NavigationMenuPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (Navigation group, "navigation-menu")

## CLI — OK
- Registry entry: present
- Files declared: 10 of 10
- Missing from registry: none
- Component deps declared: `sheet` (correct — NavigationMenuMobile uses Sheet)
