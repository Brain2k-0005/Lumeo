# ThemeToggle

**Path:** `src/Lumeo/UI/ThemeToggle/`
**Class:** Other (Utility)
**Files:** ThemeToggle.razor

## Contract — WARN
- Missing `[Parameter] public string? Class { get; set; }` — no `Class` parameter declared. Root button has hardcoded classes only; no `Class` merge.
- `@attributes="AdditionalAttributes"` present on root element — OK.
- No raw color literals. No `dark:` prefix.
- Event subscription `Theme.OnThemeChanged += OnThemeChanged` subscribed in `OnAfterRenderAsync`/firstRender; unsubscribed in `IDisposable.Dispose()` — OK.

## API — OK
- Utility toggle; minimal parameters expected. Functional with `AdditionalAttributes`.

## Bugs — OK
- `InvokeAsync(StateHasChanged)` pattern used correctly in event handler.
- No `async void`.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/ThemeTogglePage.razor` (exists)
- 3 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (`theme-toggle`)

## CLI — OK
- Registry entry: present (`theme-toggle`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none)
