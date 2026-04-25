# BottomNav

**Path:** `src/Lumeo/UI/BottomNav/`
**Class:** Other
**Files:** BottomNav.razor, BottomNavFab.razor, BottomNavItem.razor

## Contract — OK
- All checks pass. BottomNavItem uses IComponentInteropService (interface), no direct IJSRuntime. JSDisconnectedException caught in OnAfterRenderAsync.

## API — OK
- All class-required parameters present. BottomNav: ChildContent, AriaLabel, Fixed, Variant, AnimatedIndicator. BottomNavItem: Href, Label, IconContent, Badge, IsActive, OnClick, PressEffect. BottomNavFab: Href, AriaLabel, OnClick, ChildContent.

## Bugs — OK
- BottomNavItem: Nav.LocationChanged += in OnInitialized, -= in Dispose — properly paired.
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/BottomNavPage.razor` (exists)
- 5 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (not listed)

## CLI — OK
- Registry entry: present (key: bottom-nav)
- Files declared: 3 of 3
- Missing from registry: none
- Component deps declared: OK (Button.ButtonPressEffect referenced but Button is peer, not a registry dep)
