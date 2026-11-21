namespace Lumeo;

/// <summary>
/// A font choice offered by the <see cref="RichTextEditor"/> font-family dropdown.
/// </summary>
/// <param name="Label">Text shown in the dropdown (rendered in the font itself).</param>
/// <param name="Value">The CSS <c>font-family</c> value written to the content as
/// <c>&lt;span style="font-family: …"&gt;</c>, e.g. <c>Georgia, serif</c> or
/// <c>"Inter", sans-serif</c>. <c>null</c> or empty clears the font (falls back to the
/// editor's inherited font) — this is the "Default" entry.</param>
public sealed record EditorFont(string Label, string? Value = null)
{
    /// <summary>True when this entry clears the font family rather than setting one.</summary>
    public bool IsDefault => string.IsNullOrWhiteSpace(Value);
}

/// <summary>
/// Built-in font lists for <see cref="RichTextEditor.Fonts"/>. Every entry is a
/// generic/system stack that needs no download. Custom web fonts must be loaded by the
/// app (<c>@font-face</c> or a Google Fonts <c>&lt;link&gt;</c>) and then passed in
/// <see cref="RichTextEditor.Fonts"/>.
/// </summary>
public static class EditorFonts
{
    /// <summary>The default list: Default (inherit), the three generic families, plus
    /// widely available system fonts.</summary>
    public static IReadOnlyList<EditorFont> Default { get; } = new[]
    {
        new EditorFont("Default"),
        new EditorFont("Sans", "ui-sans-serif, system-ui, sans-serif"),
        new EditorFont("Serif", "ui-serif, Georgia, serif"),
        new EditorFont("Monospace", "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace"),
        new EditorFont("Arial", "Arial, Helvetica, sans-serif"),
        new EditorFont("Verdana", "Verdana, Geneva, sans-serif"),
        new EditorFont("Tahoma", "Tahoma, Geneva, sans-serif"),
        new EditorFont("Georgia", "Georgia, serif"),
        new EditorFont("Times New Roman", "\"Times New Roman\", Times, serif"),
        new EditorFont("Courier New", "\"Courier New\", Courier, monospace"),
    };
}
