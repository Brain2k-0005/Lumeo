namespace Lumeo;

/// <summary>
/// Built-in slash-command catalogue used when
/// <see cref="RichTextEditor.EnableSlashCommand"/> is true and the consumer
/// hasn't supplied their own `/` trigger.
/// </summary>
public static class SlashCommands
{
    /// <summary>Default Notion-style slash command set.</summary>
    public static IReadOnlyList<TriggerItem> Default { get; } = new TriggerItem[]
    {
        new("h1",      "Heading 1",     "Big section heading",            "Heading1"),
        new("h2",      "Heading 2",     "Medium section heading",         "Heading2"),
        new("h3",      "Heading 3",     "Small section heading",          "Heading3"),
        new("bullet",  "Bullet list",   "Simple list",                    "List"),
        new("ordered", "Numbered list", "Ordered list",                   "ListOrdered"),
        new("task",    "Task list",     "Check items off as you go",      "ListChecks"),
        new("quote",   "Quote",         "Block quote",                    "Quote"),
        new("code",    "Code block",    "Syntax-highlighted code",        "SquareCode"),
        new("table",   "Table",         "Insert a 3×3 table",             "Table"),
        new("image",   "Image",         "Upload from your device",        "Image"),
        new("divider", "Divider",       "Horizontal rule",                "Minus"),
    };

    /// <summary>
    /// Filters <see cref="Default"/> by a free-text query (case-insensitive
    /// substring match on label or id). Suitable for use as the `ItemSource`
    /// of an <see cref="EditorTrigger"/>.
    /// </summary>
    public static ValueTask<IReadOnlyList<TriggerItem>> Filter(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return ValueTask.FromResult(Default);

        var q = query.Trim();
        var filtered = Default
            .Where(i =>
                i.Label.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Id.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return ValueTask.FromResult<IReadOnlyList<TriggerItem>>(filtered);
    }
}
