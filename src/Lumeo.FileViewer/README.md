# Lumeo.FileViewer

`FileViewer` — a universal file viewer for [Lumeo](https://www.nuget.org/packages/Lumeo).
Detects file type from MIME type / extension and renders inline: PDF (via
`Lumeo.PdfViewer`), images, video, audio, Markdown, JSON, CSV, source code (via
`Lumeo.CodeEditor`) and plain text. Pluggable renderer registry; auth-aware via a
configurable `HttpClient`. Unknown types fall back to a Download CTA.

Install alongside the Lumeo core package — it registers no services of its own.

## Install

```bash
dotnet add package Lumeo --prerelease
dotnet add package Lumeo.FileViewer --prerelease
```

```csharp
// Program.cs
builder.Services.AddLumeo();   // the only DI call — core and every satellite share it
```

## Usage

```razor
@using Lumeo

<FileViewer Src="/files/report.pdf" ShowToolbar="true" ShowDownload="true" />
```

Full docs: https://lumeo.nativ.sh/components/file-viewer
