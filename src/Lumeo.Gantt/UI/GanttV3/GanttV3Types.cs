using System.ComponentModel;

namespace Lumeo.GanttV3;

/// <summary>
/// Discriminates WHY a <see cref="GanttTaskUpdate"/> fired (design spec "Public
/// API" &gt; Additive &gt; "Unified commit gate with source discriminator" — the
/// REUI <c>onEventUpdate</c> "source" analog). Defined in full now (Phase 2, T1)
/// even though <see cref="Progress"/>/<see cref="Create"/> are not yet raised by
/// any code path — T2 (progress drag) and T3 (drag-create) are the tasks that
/// fire them — so promoting this enum later never requires a breaking rename or
/// a second, incompatible discriminator type.
///
/// <c>public</c> (not <c>internal</c>): this crosses <c>GanttTimeline</c>/
/// <c>Gantt3</c>'s public <c>EventCallback&lt;GanttTaskUpdate&gt;</c> parameters
/// (both unavoidably public components — see <see cref="GanttRowKind"/>'s remarks
/// for the CS0053/Razor-compiler background), so a less-accessible type here
/// would not compile. Recorded in PublicAPI.Unshipped.txt; <see
/// cref="EditorBrowsableAttribute"/>(Never) keeps it out of consumer IntelliSense
/// until the Phase-4 rename, per this task's explicit instruction (T2/T3's
/// GanttRowKind/GanttVisibleRow predate that instruction and were left as-is).
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum GanttTaskUpdateSource
{
    /// <summary>The task's Start/End shifted together (drag-move) — duration unchanged.</summary>
    Move,
    /// <summary>The task's Start moved via the left resize handle; End unchanged (REUI parity — v2 has no left-edge resize).</summary>
    ResizeStart,
    /// <summary>The task's End moved via the right resize handle; Start unchanged (v2 parity — v2's only resize direction).</summary>
    ResizeEnd,
    /// <summary>The task's Progress changed via the progress handle (T2 — not yet raised).</summary>
    Progress,
    /// <summary>A new task was created via drag-create on an empty track (T3 — not yet raised).</summary>
    Create,
}

/// <summary>
/// Unified commit-gate payload (design spec "Public API" &gt; Additive &gt;
/// "Unified commit gate with source discriminator (onEventUpdate analog) ...
/// OnTaskUpdate(GanttTaskUpdate{Task, Source})"). Raised by <see
/// cref="Lumeo.GanttTimeline"/> and re-raised by <see cref="Lumeo.Gantt3"/>
/// alongside the v2-parity <c>OnDateChange</c> event, so a consumer that wants
/// "any edit, whatever the gesture" has one callback instead of one per gesture.
///
/// <c>public</c> — see <see cref="GanttTaskUpdateSource"/>'s remarks for why
/// (crosses the same public EventCallback parameter boundary).
/// </summary>
/// <param name="Task">The task/milestone AFTER the edit is applied.</param>
/// <param name="Source">Which gesture produced this update.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record GanttTaskUpdate(GanttTask Task, GanttTaskUpdateSource Source);

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

/// <summary>
/// Live SCHEDULING drag-drop validation context (design spec Phase 2, T2 —
/// <c>Func&lt;GanttTask, GanttDropContext, bool&gt;? CanDrop</c>, the REUI
/// <c>canDropEvent</c> analog applied to move/resize dragging rather than tree-pane
/// reordering). Evaluated by a consumer-supplied predicate while a move/resize drag
/// is in flight (<see cref="Lumeo.GanttTimeline.ValidateDrop"/>), so the drop position
/// can be rejected (ghost painted invalid, drop reverts) before the pointer is released.
///
/// <b>Naming note:</b> the plan's T2 task text describes this parameter as
/// "<c>GanttDropContext</c> from T1 types: proposed Start/End, Source" — but the
/// <see cref="GanttDropContext"/> record ALREADY defined above (Phase 2 plan's own
/// "Additive" list) is a DIFFERENT shape, purpose-built for Phase 3's tree-pane ROW
/// reorder (<c>TargetParentId</c>/<c>TargetIndex</c>/<c>TargetTaskId</c> — "where in
/// the hierarchy", not "what dates"). Reusing that name/shape here would either break
/// Phase 3's future row-reorder validation or require inventing a second, differently
/// named type for THAT purpose instead — so this SCHEDULING validation context gets
/// its own name instead. Flagged for the reviewer: this is a deliberate deviation from
/// the plan's literal wording, not an oversight.
///
/// <c>public</c> (not <c>internal</c>): crosses <see cref="Lumeo.Gantt3"/>/
/// <see cref="Lumeo.GanttTimeline"/>'s public <c>Func&lt;GanttTask,
/// GanttScheduleDropContext, bool&gt;? CanDrop</c> parameters (same CS0053
/// public-parameter-can't-expose-a-less-accessible-type constraint as
/// <see cref="GanttTaskUpdateSource"/>'s remarks explain). <see
/// cref="EditorBrowsableAttribute"/>(Never) keeps it out of consumer IntelliSense
/// until the Phase-4 rename, per this task's explicit instruction.
/// </summary>
/// <param name="ProposedStart">The task's <see cref="GanttTask.Start"/> if this drop were committed.</param>
/// <param name="ProposedEnd">The task's <see cref="GanttTask.End"/> if this drop were committed.</param>
/// <param name="Source">
/// Which gesture produced this candidate position — always <see cref="GanttTaskUpdateSource.Move"/>,
/// <see cref="GanttTaskUpdateSource.ResizeStart"/>, or <see cref="GanttTaskUpdateSource.ResizeEnd"/>;
/// never <see cref="GanttTaskUpdateSource.Progress"/> (progress dragging is not validated —
/// <c>CanDrop</c> is a scheduling/REUI concept, not a progress-percentage one) or
/// <see cref="GanttTaskUpdateSource.Create"/> (T3's own concern).
/// </param>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record GanttScheduleDropContext(
    DateTime ProposedStart,
    DateTime ProposedEnd,
    GanttTaskUpdateSource Source);
