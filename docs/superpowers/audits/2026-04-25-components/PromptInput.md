# PromptInput

**Path:** `src/Lumeo/UI/PromptInput/`
**Class:** Other (AI input)
**Files:** PromptInput.razor

## Contract — OK
- `@namespace Lumeo` present. Has `Class`, `AdditionalAttributes`, `@attributes` on root element.
- No raw color literals. No `dark:` prefix.
- Uses `<Icon>` and `<Spinner>` sub-components (not `<Blazicon>` directly).
- Implements IAsyncDisposable. Uses ComponentInteropService.

## API — OK
- Has `Value` + `ValueChanged`, `Placeholder`, `IsLoading`, `OnSend`, `DisableSendOnEmpty`, `LeadingContent`, `TrailingContent`, `MaxHeight`, `MinHeight`.
- No `Disabled` — loading state gates send; omitting explicit `Disabled` is minor.

## Bugs — OK
- JS interop (AiAutosize) called inside `if (firstRender)` guard in OnAfterRenderAsync — correct.
- DisposeAsync is a no-op stub (ValueTask.CompletedTask) — no leak risk.
- No findings.

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/PromptInputPage.razor` (MISSING)
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: `icon`, `spinner` — correct
