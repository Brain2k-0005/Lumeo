# ThemeSwitcher

**Path:** `src/Lumeo/UI/ThemeSwitcher/`
**Class:** Other (Utility)
**Files:** ThemeSwitcher.razor

## Contract — WARN
- `style="background-color: @scheme.PreviewColor;"` uses an inline style with a runtime value (from ThemeService data). Not a raw hex literal in markup but a dynamic CSS value — minor: PreviewColor strings in ThemeService may contain raw hex/hsl. Flag as WARN.
- Event subscription `Theme.OnThemeChanged += OnThemeChanged` in `OnAfterRenderAsync` (firstRender guard present); unsubscribed in `Dispose()` — OK.

## API — OK
- Utility component; only `Class` and `AdditionalAttributes` needed. All present.

## Bugs — WARN
- `Theme.OnThemeChanged += OnThemeChanged` subscribed inside `OnAfterRenderAsync`/firstRender; unsubscribed in `IDisposable.Dispose()` — subscription/disposal pattern correct.
- `InvokeAsync(StateHasChanged)` called from event handler (correct pattern).
- No `async void` found.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ThemeSwitcherPage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (`theme-switcher`)

## CLI — OK
- Registry entry: present (`theme-switcher`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none listed; ThemeService is a service not a component dep)
