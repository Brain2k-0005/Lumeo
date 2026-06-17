namespace Lumeo;

/// <summary>
/// JSON-serializable snapshot of a <see cref="ResizablePanelGroup"/>'s current
/// panel sizes (percentages, in panel order). Used to persist a user-dragged
/// layout across sessions in tandem with
/// <see cref="ResizablePanelGroup.SavedLayout"/> and
/// <see cref="ResizablePanelGroup.OnLayout"/>.
/// </summary>
/// <param name="Sizes">
/// The panel sizes as percentages of the group, in the same order the panels
/// are declared (matching <see cref="ResizablePanel.Order"/>). Should sum to
/// ~100.
/// </param>
/// <remarks>
/// Mirrors <see cref="TabsLayout"/>: the consumer owns persistence. On init,
/// pass a previously-saved <see cref="ResizableLayout"/> to
/// <see cref="ResizablePanelGroup.SavedLayout"/>; on every resize the group
/// raises <see cref="ResizablePanelGroup.OnLayout"/> with the new snapshot for
/// the consumer to store (local storage, a server, …).
/// </remarks>
public sealed record ResizableLayout(IReadOnlyList<double> Sizes);
