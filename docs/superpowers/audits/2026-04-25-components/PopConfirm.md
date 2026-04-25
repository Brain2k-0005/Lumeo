# PopConfirm

**Path:** `src/Lumeo/UI/PopConfirm/`
**Class:** Overlay
**Files:** PopConfirm.razor

## Contract — WARN
- `@namespace Lumeo` present. Has `Class`, `AdditionalAttributes`.
- Root element is `<Popover>` — `@attributes` applied to `<Popover>`, which itself has no `AdditionalAttributes` param (Popover.razor lacks it on the wrapper) — pass-through chain broken.
- No raw color literals. No `dark:` prefix.
- Not IAsyncDisposable — delegates to Popover sub-component which handles its own lifecycle. PopConfirm itself has no JS interop.
- No JSDisconnectedException needed (no direct interop). No direct IJSRuntime.
- Missing overlay-class params: `Open`/`OpenChanged`, `OnOpen`, `OnClose`, `Disabled`. Uses internal `_isOpen` state.

## API — WARN
- Overlay class: missing `Open`+`OpenChanged` (uses internal state only), missing `OnOpen`, `OnClose`, `Disabled`.
- Has `OnConfirm`, `OnCancel`, `Title`, `Description`, `ConfirmText`, `CancelText`, `IsDestructive`, `Icon`, `Placement`.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/PopConfirmPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no (not listed)

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: `button`, `popover` — correct
