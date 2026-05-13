using Lumeo.Services.Localization;

namespace Lumeo;

/// <summary>
/// A single AI action that operates on the current editor selection.
/// </summary>
public sealed record AiAction(string Id, string Label, string? IconName = null);

/// <summary>Built-in AI actions surfaced by the bubble menu / AiActionMenu.</summary>
public static class AiActions
{
    // EN fallback IDs — kept so existing code that references AiActions.Improve etc.
    // still works. The labels here are the English defaults; use LocalizedDefault to
    // get locale-aware labels at render time.
    public static AiAction Improve { get; } = new("improve", "Improve writing", "Sparkles");
    public static AiAction Shorten { get; } = new("shorten", "Make shorter", "Minimize2");
    public static AiAction Expand { get; } = new("expand", "Make longer", "Maximize2");
    public static AiAction Translate { get; } = new("translate", "Translate", "Languages");
    public static AiAction Summarize { get; } = new("summarize", "Summarize", "FileText");
    public static AiAction FixGrammar { get; } = new("fix-grammar", "Fix grammar", "SpellCheck");

    /// <summary>Default action set with EN labels. Prefer <see cref="LocalizedDefault"/> when a
    /// localizer is available so labels reflect the active locale.</summary>
    public static IReadOnlyList<AiAction> Default { get; } = new[]
    {
        Improve, Shorten, Expand, FixGrammar, Summarize, Translate,
    };

    /// <summary>Returns the default action set with labels resolved through the active localizer.</summary>
    public static IReadOnlyList<AiAction> LocalizedDefault(ILumeoLocalizer L) => new[]
    {
        new AiAction("improve",    L["Editor.AiImproveWriting"], "Sparkles"),
        new AiAction("shorten",    L["Editor.AiMakeShorter"],   "Minimize2"),
        new AiAction("expand",     L["Editor.AiMakeLonger"],    "Maximize2"),
        new AiAction("fix-grammar",L["Editor.AiFixGrammar"],    "SpellCheck"),
        new AiAction("summarize",  L["Editor.AiSummarize"],     "FileText"),
        new AiAction("translate",  L["Editor.AiTranslate"],     "Languages"),
    };
}
