# Mention

**Path:** `src/Lumeo/UI/Mention/`
**Class:** Form input
**Files:** Mention.razor

## Contract — OK
- `@namespace Lumeo` present
- Has `Class` and `AdditionalAttributes` params
- Root `<div>` carries `@attributes="AdditionalAttributes"`
- IAsyncDisposable implemented; DisposeAsync is a no-op (no JS to clean up) — acceptable
- JSDisconnectedException not explicitly caught (no JS calls in dispose path — OK)
- ComponentInteropService injected and used (`GetTextareaCaretPosition`)
- No raw color literals, no `dark:` prefix
- No inline SVG blocks

## API — WARN
- Form input class: Value + ValueChanged present; Disabled present; Placeholder present
- Missing: Required, Invalid, ErrorText, HelperText, Label, Name, ReadOnly, MaxLength
- Missing 6+ form-input required params

## Bugs — WARN
- HandleBlur: `await Task.Delay(150)` inside async method — timing hack to allow dropdown click to register before blur; not a bug per se but fragile in fast interactions
- `GetTextareaCaretPosition` result accessed without null check in `HandleInput` and `SelectOption` (caretInfo.SelectionStart, caretInfo.Top, caretInfo.Left)

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/MentionPage.razor` (exists)
- 2 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none)
