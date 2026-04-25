# OtpInput

**Path:** `src/Lumeo/UI/OtpInput/`
**Class:** Form input
**Files:** OtpInput.razor

## Contract — OK
- `@namespace Lumeo` present. Has `Class`, `AdditionalAttributes`, `@attributes` on root element.
- No raw color literals. No `dark:` prefix.
- Implements IAsyncDisposable; uses ComponentInteropService; catches JSDisconnectedException in DisposeAsync.

## API — WARN
- Has `Value` + `ValueChanged`, `Disabled`. Good.
- Missing: `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label`, `Name`.
- Has `OnComplete`, `Masked`, `Separator`, `GroupSizes`, `InputMode`, `Length` — strong feature set.
- Core input API parameters absent for a form-input class component.

## Bugs — OK
- OnAfterRenderAsync: RegisterOtpPaste called inside `if (firstRender)` guard — correct.
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/OtpInputPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes (Form group, "otp-input")

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: `list` (unclear — no List component referenced in source; possible stale dep)
