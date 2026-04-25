# StreamingText

**Path:** `src/Lumeo/UI/StreamingText/`
**Class:** Other
**Files:** StreamingText.razor

## Contract — WARN
- `dark:prose-invert` Tailwind prefix used inside `RootClass` property (line 30), conditionally when `Prose=true`. Violates no-dark: rule even though it is inside the standard Tailwind Typography plugin convention. Flag: `dark:prose-invert` in StreamingText.razor:30.

## API — OK
- For Other class: has `Text`, `IsStreaming`, `Prose`, `Class`, `AdditionalAttributes`. Fits the component purpose.

## Bugs — OK
- No findings.

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/StreamingTextPage.razor` (MISSING)
- 0 ComponentDemo blocks
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present (`streaming-text`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (none needed)
