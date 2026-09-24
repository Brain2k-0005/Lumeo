# Lumeo.CodeEditor

`CodeEditor` — a code editor component for [Lumeo](https://www.nuget.org/packages/Lumeo),
powered by CodeMirror 6 (lightweight, modular, ~150KB core + on-demand language packs —
far lighter than Monaco). Syntax highlighting, line numbers, minimap and a themeable dark
mode that follows Lumeo's own theme.

Install alongside the Lumeo core package — it registers no services of its own.

## Install

```bash
dotnet add package Lumeo --prerelease
dotnet add package Lumeo.CodeEditor --prerelease
```

```csharp
// Program.cs
builder.Services.AddLumeo();   // the only DI call — core and every satellite share it
```

## Usage

```razor
@using Lumeo

<CodeEditor @bind-Value="_code" Language="csharp" Height="320px" LineNumbers="true" />

@code {
    private string _code = "Console.WriteLine(\"Hello, Lumeo!\");";
}
```

Full docs: https://lumeo.nativ.sh/components/code-editor
