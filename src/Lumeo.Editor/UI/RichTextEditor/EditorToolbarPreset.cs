namespace Lumeo;

/// <summary>
/// Toolbar density presets for <see cref="RichTextEditor"/>.
/// </summary>
public enum EditorToolbarPreset
{
    /// <summary>No toolbar at all (consumer-driven via context menu / keyboard).</summary>
    None,
    /// <summary>Bold, Italic, Link only.</summary>
    Minimal,
    /// <summary>Headings, basic marks, lists, link, undo/redo. Default.</summary>
    Standard,
    /// <summary>Standard plus tables, image, code block, AI menu, Word import.</summary>
    Full,
}
