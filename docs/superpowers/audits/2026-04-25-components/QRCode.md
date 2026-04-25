# QRCode

**Path:** `src/Lumeo/UI/QRCode/`
**Class:** Display
**Files:** QRCode.razor, QRCodeEncoder.cs

## Contract — OK
- `@namespace Lumeo` present. Has `Class`, `AdditionalAttributes`, `@attributes` on root element.
- `ForegroundColor` defaults to `"currentColor"` and `BackgroundColor` to `"transparent"` — these are SVG attribute values passed to `fill=`, not CSS color literals in class strings. Acceptable.
- No `dark:` prefix. No Blazicon SVGs (pure SVG generation).

## API — OK
- Display class. Has `Value`, `Size`, `ErrorCorrectionLevel`, `ForegroundColor`, `BackgroundColor`, `IncludeMargin`, `ImageSrc`, `ImageSize`.
- `Variant` not applicable for QR code. `Size` present.

## Bugs — OK
- Exception swallowed in `catch` block (lines 81-87) during QR generation — silently resets state. Could mask encoding errors from consumers but is intentional defensive coding.
- No JS interop. No lifecycle issues.
- No findings.

## Docs — FAIL
- Page: `docs/Lumeo.Docs/Pages/Components/QRCodePage.razor` (MISSING)
- API Reference: MISSING
- Indexed in ComponentsIndex.razor: no

## CLI — OK
- Registry entry: present
- Files declared: 2 of 2 (QRCode.razor + QRCodeEncoder.cs)
- Missing from registry: none
- Component deps declared: none (none referenced)
