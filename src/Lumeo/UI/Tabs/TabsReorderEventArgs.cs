namespace Lumeo;

/// <summary>
/// Payload for <see cref="TabsList.OnReorder"/>. Raised after a successful
/// drag-and-drop reorder of <see cref="TabsTrigger"/> headers when the
/// enclosing <see cref="TabsList"/> has <c>Reorderable="true"</c>.
/// </summary>
/// <param name="FromIndex">Zero-based position of the moved trigger before the drop.</param>
/// <param name="ToIndex">Zero-based position the trigger was dropped onto.</param>
/// <param name="MovedTabValue">The <c>Value</c> of the trigger that was moved.</param>
/// <remarks>
/// The library does <b>not</b> mutate the underlying tab collection — the
/// consumer's handler is expected to update its own data source and trigger a
/// re-render so the new order takes effect.
/// </remarks>
public sealed record TabsReorderEventArgs(int FromIndex, int ToIndex, string MovedTabValue);
