namespace Lumeo;

/// <summary>
/// A single AI action that operates on the current editor selection.
/// </summary>
public sealed record AiAction(string Id, string Label, string? IconName = null);

/// <summary>Built-in AI actions surfaced by the bubble menu / AiActionMenu.</summary>
public static class AiActions
{
    public static AiAction Improve    { get; } = new("improve",    "Improve writing",  "Sparkles");
    public static AiAction Shorten    { get; } = new("shorten",    "Make shorter",     "Minimize2");
    public static AiAction Expand     { get; } = new("expand",     "Make longer",      "Maximize2");
    public static AiAction Translate  { get; } = new("translate",  "Translate",        "Languages");
    public static AiAction Summarize  { get; } = new("summarize",  "Summarize",        "FileText");
    public static AiAction FixGrammar { get; } = new("fix-grammar", "Fix grammar",     "SpellCheck");

    /// <summary>Default action set rendered by the bubble menu's AI button.</summary>
    public static IReadOnlyList<AiAction> Default { get; } = new[]
    {
        Improve, Shorten, Expand, FixGrammar, Summarize, Translate,
    };
}
