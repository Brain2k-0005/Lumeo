# Lumeo.PdfViewer

`PdfViewer` — inline PDF rendering for [Lumeo](https://www.nuget.org/packages/Lumeo),
powered by Mozilla pdf.js. Page navigation, zoom, search and download, no plugin or
native dependency.

Install alongside the Lumeo core package — it registers no services of its own.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.PdfViewer
```

```csharp
// Program.cs
builder.Services.AddLumeo();   // the only DI call — core and every satellite share it
```

## Usage

```razor
@using Lumeo

<PdfViewer Src="/files/report.pdf" ShowToolbar="true" ShowPageNav="true" />
```

Full docs: https://lumeo.nativ.sh/components/pdf-viewer
