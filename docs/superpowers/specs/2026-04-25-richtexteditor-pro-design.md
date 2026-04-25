# RichTextEditor Pro — Design

**Status:** Approved 2026-04-25 (autonomous decision per "best on market" directive)
**Target package:** `Lumeo.Editor` (lands after package split completes)
**Driver:** Match TipTap / Notion / Slack baseline. Ship features that consumers expect from a 2026 rich text editor without making them shop elsewhere.

## Goal

Make `<RichTextEditor>` a first-class authoring surface — not just a textarea-with-bold-button. Ship the features competitors have, in one cohesive API, with sensible defaults.

## Feature roster (all in this release)

| Feature | Why | Effort | Notes |
|---------|-----|--------|-------|
| **@-mentions** (pluggable triggers) | Chat, comments, reviews. Table-stakes for any 2026 editor. | ~3/4 day | Triggers configurable from day one — add `#`, `$`, `/` later without breaking API |
| **Word (.docx) import** | Enterprise must-have. Mammoth is mature, MIT-licensed, ~80 KB. | ~half day | Server-side default; browser-side opt-in via lazy-loaded mammoth.browser.js (not bundled) |
| **Slash command menu** (`/heading`, `/list`, `/table`…) | Notion/Linear/Outline pattern. Power users expect it. | ~1 day | Reuses the trigger system from mentions |
| **Tables** | Markdown editors have them. Office docs have them. | ~half day | TipTap's `@tiptap/extension-table` |
| **Code blocks with syntax highlighting** | Engineering teams expect this. | ~half day | TipTap's `@tiptap/extension-code-block-lowlight` + `lowlight` |
| **Image upload (drag/drop/paste)** | Without this it's not a real editor. | ~half day | TipTap base + a consumer-supplied `OnImageUpload` callback returning a URL |
| **Smart paste** | HTML cleanup, markdown auto-conversion (`**bold**` → bold while typing), URL → link auto-detection | ~half day | Combines TipTap's `Markdown` and `Link.autolink` |
| **Toolbar Import button** | Surfaces the .docx import in the UI | ~quarter day | Plus generic "paste from clipboard" intelligence |
| **Mention/slash dropdown UI** | Reusable Razor primitive: positioned floating list with keyboard nav | ~half day | Built once, used by both triggers |

**Total: ~5 days of focused engineering.** Each piece is independently shippable; we land them in one PR for one cohesive release.

## API surface

```razor
<RichTextEditor @bind-Value="_html"
                Triggers="_triggers"
                AllowWordImport="true"
                OnWordImportRequested="HandleWordImport"
                OnImageUpload="HandleImageUpload"
                EnableTables="true"
                EnableCodeBlock="true"
                EnableSlashCommand="true"
                EnableMarkdownShortcuts="true"
                Toolbar="@(RichTextEditor.ToolbarPreset.Standard)"
                MaxLength="50000" />
```

All new params are optional with sensible defaults (`Triggers` defaults to a single `@`-mention if `Mentions` is provided; `AllowWordImport` defaults true if `OnWordImportRequested` is wired; etc.). Zero behavior change for current `<RichTextEditor @bind-Value>` consumers.

### Trigger system (one design serves both mentions and slash commands)

```csharp
public record EditorTrigger(
    char Char,                                       // '@', '#', '/', etc.
    Func<string, ValueTask<IEnumerable<TriggerItem>>> ItemSource,
    RenderFragment<TriggerItem>? ItemTemplate = null,
    string? ChipClass = null);                       // null = use default chip styling

public record TriggerItem(
    string Id,
    string Label,
    string? Subtitle = null,
    string? IconName = null,                          // Lucide icon name
    object? Payload = null);                          // app-specific data
```

A `Triggers` parameter on `<RichTextEditor>` registers each. The dropdown UI is shared. Slash command is just `EditorTrigger { Char = '/', ItemSource = BuiltInSlashCommands }` — pre-baked when `EnableSlashCommand="true"`.

### Word import API

```csharp
[Parameter] public bool AllowWordImport { get; set; }
[Parameter] public EventCallback<WordImportRequest> OnWordImportRequested { get; set; }

public record WordImportRequest(IBrowserFile File, Action<string> SetHtml);
```

Toolbar shows an "Import .docx" button when `AllowWordImport`. Clicking opens a file picker. On file selected, fires `OnWordImportRequested` with the browser file. **Consumer's responsibility:** pipe the file to a server endpoint that runs Mammoth.NET, returns HTML, and call `SetHtml`. Lumeo doesn't run conversion in-process (keeps `Lumeo.Editor` server-architecture-agnostic).

To make this trivial, `Lumeo.Editor` ships a static helper:

```csharp
public static class WordImporter
{
    public static async ValueTask<string> ToHtmlAsync(Stream docxStream, WordImportOptions? options = null);
}
```

Implementation wraps `Mammoth` NuGet (MIT, ~80 KB). Consumers call it from their own `[HttpPost("/upload-docx")]` endpoint. Total integration: ~10 lines on the consumer's side.

**Default style map (baked into `WordImporter`):**

`WordImporter.DefaultStyleMap` ships with mappings for English AND German Word default styles, since enterprise docs commonly use both:

```text
p[style-name='Title']         => h1.doc-title:fresh
p[style-name='Titel']         => h1.doc-title:fresh
p[style-name='Subtitle']      => h2.doc-subtitle:fresh
p[style-name='Untertitel']    => h2.doc-subtitle:fresh
p[style-name='Heading 1']     => h1:fresh
p[style-name='Überschrift 1'] => h1:fresh
p[style-name='Heading 2']     => h2:fresh
p[style-name='Überschrift 2'] => h2:fresh
p[style-name='Heading 3']     => h3:fresh
p[style-name='Überschrift 3'] => h3:fresh
p[style-name='Heading 4']     => h4:fresh
p[style-name='Überschrift 4'] => h4:fresh
p[style-name='Body Text']     => p:fresh
p[style-name='Textkörper']    => p:fresh
p[style-name='List Paragraph']=> li:fresh
p[style-name='Listenabsatz']  => li:fresh
r[style-name='Strong']        => strong
r[style-name='Emphasis']      => em
```

Validated against real-world test docs in `tests/docx-fixtures/` covering EN/DE/SL/HR/ES/FR variants of the same template. Consumers can extend or replace via `WordImportOptions { StyleMap = ... }`.

**Browser-side path (opt-in):** if consumer sets `WordImportMode.Browser`, the editor lazy-loads `mammoth.browser.min.js` from a CDN URL the consumer configures. Default CDN: `https://unpkg.com/mammoth@1.x/mammoth.browser.min.js`. ~400 KB, downloaded only when user clicks Import. Not bundled in the package.

### Image upload API

```csharp
[Parameter] public Func<IBrowserFile, ValueTask<string>>? OnImageUpload { get; set; }
```

When set, drag/drop/paste of images calls this and inserts an `<img>` with the returned URL. Consumer handles storage (S3, blob, base64, whatever). When not set, dropped images are inserted as base64 (works out of the box, fine for small docs, warning logged for >1 MB).

## Toolbar presets

```csharp
public enum ToolbarPreset
{
    None,        // No toolbar (consumer-driven via context menu / keyboard)
    Minimal,     // Bold, Italic, Link
    Standard,    // Minimal + Lists, Headings, Quote, Code (DEFAULT)
    Full         // Standard + Tables, Image, Code Block, Word Import
}
```

Plus `[Parameter] public RenderFragment? CustomToolbar { get; set; }` for full override.

## What ships in `Lumeo.Editor`

```
src/Lumeo.Editor/
├── Lumeo.Editor.csproj                   ← references Lumeo (core), Mammoth (NuGet)
├── UI/RichTextEditor/
│   ├── RichTextEditor.razor              ← extended
│   ├── RichTextEditor.razor.css
│   ├── EditorTrigger.cs                  ← new
│   ├── TriggerItem.cs                    ← new
│   ├── TriggerDropdown.razor             ← new (reusable for mentions + slash)
│   ├── SlashCommands.cs                  ← built-in slash command set
│   ├── EditorToolbar.razor               ← new
│   └── WordImporter.cs                   ← server-side .docx → HTML helper
├── _Imports.razor
└── wwwroot/js/
    └── rich-text-editor.js               ← extended with mention/trigger/upload hooks
```

## Wiring the docs page

`docs/Lumeo.Docs/Pages/Components/RichTextEditorPage.razor` (already exists from audit Sprint C2):
- "Basic" demo (current behavior)
- "Mentions" demo (with a static user list)
- "Slash commands" demo (built-in commands)
- "Tables / Code / Images" demo
- "Word import" demo (with a mock server endpoint via JS prompt)

API Reference table updated with new parameters.

## Out of scope (Phase 2)

- **Collaborative editing** (Y.js / OT). Would 4× the spec scope.
- **Track changes / comments** (Word-style suggestions). Big surface, defer.
- **Export to .docx** (round-trip). Mammoth doesn't do this; would need a separate library.
- **Custom block extensions API**. Future: expose a `CustomExtensions` parameter for consumer-provided TipTap nodes.
- **Plugin marketplace**. Way later.

## Risks

- **TipTap version drift.** TipTap is rapidly evolving; we pin a specific version in `rich-text-editor.js` and bump it intentionally per release.
- **Bundle size.** Adding tables + lowlight + markdown extensions inflates `rich-text-editor.js` from current ~6 KB to ~50 KB minified. Still well within the `Lumeo.Editor` package's ~80 KB budget.
- **Mammoth fidelity.** Mammoth handles paragraphs/lists/tables/images well, but loses Word's exotic formatting (text boxes, embedded objects, complex shapes). Document this clearly — most users importing .docx are bringing prose, not Powerpoint.
- **CDN dependency for browser-side import.** If unpkg goes down, browser-side import breaks. Acceptable — consumers can configure their own CDN URL, and the server-side path is independent.

## Sequence

1. **Spec** (this) — committed.
2. **Wait for Sprint H to land** — `Lumeo.Editor` package must exist before code can land in it.
3. **Single agent (Opus, since this is design-heavy)** implements the full feature set in `Lumeo.Editor`. ~5 days human-equivalent, hopefully ~1-2 hours agent.
4. **Verify:** dotnet build, smoke-test in docs site, all 5 demos render.
5. **Single squashed commit** with the new behavior, gated behind opt-in params so existing consumers see no regression.
