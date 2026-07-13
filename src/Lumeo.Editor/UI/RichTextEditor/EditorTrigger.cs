using Microsoft.AspNetCore.Components;

namespace Lumeo;

/// <summary>
/// Registers a typed trigger character (`@`, `#`, `$`, `/`, ...) on the
/// <see cref="RichTextEditor"/>. When the user types the character, the
/// editor calls <see cref="ItemSource"/> with the partial query and renders
/// a floating result list.
/// </summary>
public sealed record EditorTrigger(
    char Char,
    Func<string, ValueTask<IReadOnlyList<TriggerItem>>> ItemSource,
    RenderFragment<TriggerItem>? ItemTemplate = null,
    string? ChipClass = null);

/// <summary>
/// A single row shown in the trigger or slash-command dropdown. Lightweight
/// enough that result lists can be returned from arbitrary backends without
/// allocating UI types.
/// </summary>
public sealed record TriggerItem(
    string Id,
    string Label,
    string? Subtitle = null,
    string? IconName = null,
    object? Payload = null)
{
    // Trim safety: this record is returned to JS from the OnTriggerQuery [JSInvokable]
    // method. JSRuntime's reflection-based serializer must never bind the positional
    // ctor — the trimmer strips its parameter names ("ConstructorContainsNullParameterNames",
    // crashes the component under a trimmed publish). With this parameterless ctor STJ
    // uses property-based (de)serialization instead. Do not remove.
    public TriggerItem() : this("", "") { }
}
