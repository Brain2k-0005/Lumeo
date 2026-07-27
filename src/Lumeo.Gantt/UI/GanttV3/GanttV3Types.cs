namespace Lumeo.GanttV3;

/// <summary>
/// Payload for a completed tree-pane row reorder (design spec "Public API" &gt;
/// Additive &gt; <c>AllowRowReorder</c> + <c>EventCallback&lt;GanttRowReorder&gt;</c>).
/// Raised once, after drop, with the task's old and new position — and, since a
/// row can be dragged under a different parent now that <see cref="GanttTask.ParentId"/>
/// exists, its old and new parent as well. A reorder that only changes index
/// (same parent) leaves <see cref="PreviousParentId"/>/<see cref="NewParentId"/> equal.
///
/// <c>internal</c> for now (see <see cref="GanttState"/> for why): promoted to public
/// API alongside <c>AllowRowReorder</c>/<c>OnRowReorder</c> when the tree pane actually
/// wires up drag-drop (Phase 3).
/// </summary>
/// <param name="TaskId">Id of the task/row that was moved.</param>
/// <param name="PreviousParentId">The task's <see cref="GanttTask.ParentId"/> before the move (null = root).</param>
/// <param name="NewParentId">The task's <see cref="GanttTask.ParentId"/> after the move (null = root).</param>
/// <param name="PreviousIndex">Sibling index (within <see cref="PreviousParentId"/>'s children) before the move.</param>
/// <param name="NewIndex">Sibling index (within <see cref="NewParentId"/>'s children) after the move.</param>
internal sealed record GanttRowReorder(
    string TaskId,
    string? PreviousParentId,
    string? NewParentId,
    int PreviousIndex,
    int NewIndex);

/// <summary>
/// Live drag-drop validation context (design spec "Public API" &gt; Additive &gt;
/// <c>Func&lt;GanttTask, GanttDropContext, bool&gt;? CanDrop</c> — the REUI
/// <c>canDropEvent</c> analog). Passed alongside the dragged <see cref="GanttTask"/>
/// to a consumer-supplied predicate evaluated continuously while a row drag is in
/// flight, so the drop target can be rejected (e.g. "no dropping a parent onto its
/// own descendant") before the user releases the pointer.
///
/// <c>internal</c> for now (see <see cref="GanttState"/> for why): promoted to public
/// API alongside <c>CanDrop</c> when the tree pane actually wires up drag-drop (Phase 3).
/// </summary>
/// <param name="TargetParentId">The candidate parent id at the current drop position (null = root).</param>
/// <param name="TargetIndex">The candidate sibling index at the current drop position.</param>
/// <param name="TargetTaskId">Id of the row currently under the pointer, if any (null when hovering empty space below the last row).</param>
internal sealed record GanttDropContext(
    string? TargetParentId,
    int TargetIndex,
    string? TargetTaskId);
