# FileUpload

**Path:** `src/Lumeo/UI/FileUpload/`
**Class:** Form input
**Files:** FileUpload.razor

## Contract — OK
- `@namespace Lumeo`, `Class?`, `CaptureUnmatchedValues`, `@attributes` on root div.
- No raw colors, no `dark:` prefixes.
- Icons via `<Blazicon>` (Lucide.Upload, Lucide.File, Lucide.User).

## API — WARN
- Form input class — missing: `Disabled`, `Required`, `Invalid`, `ErrorText`, `HelperText`, `Label` (external label), `Name`, `Value`+`ValueChanged`.
- Has: `OnFilesSelected` EventCallback, `MaxFileSize`, `MaxFiles`, `Multiple`, `Accept`, `Variant`, `ShowProgress`, `Progress`, `ShowThumbnails`, `FileTemplate`.
- No `Value`/`ValueChanged` binding; fires `OnFilesSelected` instead (different pattern).

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/FileUploadPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (`file-upload`)
- Files declared: 1 of 1
- Missing from registry: none
- Component deps declared: OK (input listed)
