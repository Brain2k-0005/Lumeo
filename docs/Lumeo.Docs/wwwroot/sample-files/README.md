# Lumeo FileViewer

A universal preview surface for Blazor — drop in a URL, get the right inline viewer.

## Highlights

- **Auto-detect** by MIME, optional HEAD, or URL extension
- **Built-in renderers** for PDF, image, video, audio, markdown, source code, JSON, CSV, and plain text
- **Auth-aware** fetches via `HttpClient` parameter or `ConfigureRequest` delegate
- **Safety caps** — `MaxBytes` for text fetches, `MaxCsvRows` for tabular data
- **Pluggable** per-kind overrides via `CustomRenderers`

## Quick start

```razor
<FileViewer Src="@DocumentUrl"
            FileName="@Document.Name"
            OnLoaded="HandleLoaded"
            Class="h-[480px]" />
```

## Resolution order

1. Explicit `Kind` parameter (anything other than `Auto` wins)
2. Explicit `MimeType` parameter
3. `HEAD` request `Content-Type` (when `AutoHead="true"`)
4. URL extension

> Markdown is rendered with `.DisableHtml()` — raw `<script>` and `<iframe>` in
> user-supplied markdown never reach the DOM.

For more, see the [docs site](https://lumeo.nativ.sh/components/file-viewer).
