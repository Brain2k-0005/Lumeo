# Lumeo.Editor

`RichTextEditor` — a rich-text editor for [Lumeo](https://www.nuget.org/packages/Lumeo),
powered by TipTap. Integrates with Lumeo's `Form`/`FormField` validation like any other
form control.

Install alongside the Lumeo core package — it registers no services of its own.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.Editor
```

```csharp
// Program.cs
builder.Services.AddLumeo();   // the only DI call — core and every satellite share it
```

```html
<!-- host page (index.html / App.razor) -->
<script src="_content/Lumeo.Editor/js/editor-interop.js"></script>
```

## Usage

```razor
@using Lumeo

<RichTextEditor @bind-Value="_html" Placeholder="Write something…" MinHeight="180" />

@code {
    private string? _html;
}
```

Full docs: https://lumeo.nativ.sh/components/rich-text-editor
