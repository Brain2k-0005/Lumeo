using Lumeo.Services.Localization;

namespace Lumeo;

/// <summary>
/// Built-in slash-command catalogue used when
/// <see cref="RichTextEditor.EnableSlashCommand"/> is true and the consumer
/// hasn't supplied their own `/` trigger.
/// </summary>
public static class SlashCommands
{
    /// <summary>Default Notion-style slash command set with EN labels. Prefer
    /// <see cref="LocalizedDefault"/> when a localizer is available.</summary>
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

    /// <summary>Returns the default slash command set with labels resolved through the active localizer.</summary>
    public static IReadOnlyList<TriggerItem> LocalizedDefault(ILumeoLocalizer L) => new TriggerItem[]
    {
        new("h1",      L["Editor.SlashHeading1"],     L["Editor.SlashHeading1Sub"],     "Heading1"),
        new("h2",      L["Editor.SlashHeading2"],     L["Editor.SlashHeading2Sub"],     "Heading2"),
        new("h3",      L["Editor.SlashHeading3"],     L["Editor.SlashHeading3Sub"],     "Heading3"),
        new("bullet",  L["Editor.SlashBulletList"],   L["Editor.SlashBulletListSub"],   "List"),
        new("ordered", L["Editor.SlashNumberedList"], L["Editor.SlashNumberedListSub"], "ListOrdered"),
        new("task",    L["Editor.SlashTaskList"],     L["Editor.SlashTaskListSub"],     "ListChecks"),
        new("quote",   L["Editor.SlashQuote"],        L["Editor.SlashQuoteSub"],        "Quote"),
        new("code",    L["Editor.SlashCodeBlock"],    L["Editor.SlashCodeBlockSub"],    "SquareCode"),
        new("table",   L["Editor.SlashTable"],        L["Editor.SlashTableSub"],        "Table"),
        new("image",   L["Editor.SlashImage"],        L["Editor.SlashImageSub"],        "Image"),
        new("divider", L["Editor.SlashDivider"],      L["Editor.SlashDividerSub"],      "Minus"),
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

    /// <summary>
    /// Filters <see cref="LocalizedDefault"/> by a free-text query using localized labels.
    /// </summary>
    public static ValueTask<IReadOnlyList<TriggerItem>> Filter(string query, ILumeoLocalizer L)
    {
        var localized = LocalizedDefault(L);
        if (string.IsNullOrWhiteSpace(query))
            return ValueTask.FromResult(localized);

        var q = query.Trim();
        var filtered = localized
            .Where(i =>
                i.Label.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Id.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return ValueTask.FromResult<IReadOnlyList<TriggerItem>>(filtered);
    }
}
