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

/// <summary>
/// The drag-relevant fields <see cref="Lumeo.GanttTimeline"/> pushes to the JS
/// drag engine (design spec Phase 3, T1 — "replace the growing
/// <c>HashCode.Combine</c> options-hash with an options record"). Replaces a
/// plain <c>int</c> hash of the same fields: a record's synthesized structural
/// equality (<c>==</c>/<c>!=</c>, <c>Equals</c>) directly answers "did anything
/// drag-relevant actually change" with no collision risk and no fixed argument
/// ceiling to run into as more fields join it (<c>HashCode.Combine</c> only
/// overloads up to 8 arguments; this was already at 5 and the Phase-3
/// watch-list — reorder/selection/splitter — only adds more candidates).
///
/// Compared once per render in <see cref="Lumeo.GanttTimeline.SyncDragRegistrationAsync"/>
/// against the previously-registered value: an unchanged value skips the
/// <c>GanttV3RegisterDragAsync</c> interop round-trip entirely, the same
/// short-circuit the old hash comparison provided. This is a PERFORMANCE gate
/// only, not a drag-safety one — gantt-v3.js's own <c>registerDrag</c> already
/// snapshots <c>reg.options</c> into a local at the moment a gesture begins
/// (see its own "Snapshot drag options at pointerdown" remarks), so even a
/// registration call that DOES land mid-drag (a genuine options change while a
/// gesture is already in flight) only updates the STORED config for the NEXT
/// gesture — the currently-running one keeps reading its own local snapshot
/// and is structurally unreachable by the swap. Record-equality re-registration
/// therefore changes nothing about drag safety; it keeps the interop call
/// count low exactly as the hash did, just without a hash's collision risk and
/// without capping how many fields can ever describe "the options".
/// </summary>
/// <param name="ColumnWidth"><see cref="Lumeo.GanttTimeline.EffectiveColumnWidth"/> at push time.</param>
/// <param name="PixelsPerDay"><see cref="Lumeo.GanttTimeline.PixelsPerDay"/> at push time (move/resize snap math).</param>
/// <param name="HasCanDrop">Whether <see cref="Lumeo.GanttTimeline.CanDrop"/> is set (gates whether JS ever calls <c>ValidateDrop</c> at all).</param>
/// <param name="AllowCreate"><see cref="Lumeo.GanttTimeline.AllowCreate"/> at push time.</param>
/// <param name="Origin">The row-canvas-space date origin (drag-create's only anchor — see <c>BuildDragOptions</c>'s remarks).</param>
internal readonly record struct GanttInteropOptions(
    int ColumnWidth,
    double PixelsPerDay,
    bool HasCanDrop,
    bool AllowCreate,
    DateTime Origin);

/// <summary>
/// A duration-weighted rollup summary for one hierarchy-parent or flat-group
/// row (design spec Phase 3, T3 — "Duration-weighted progress rollup per
/// parent/group row (<c>GanttRollup</c> record: Start, End, WeightedProgress)").
/// Produced by <see cref="GanttRollupModel"/> (the default math) or by a
/// consumer's own <c>RollupMath</c> override — see
/// <see cref="Lumeo.GanttTimeline.RollupMath"/>'s remarks for the exact
/// per-parent-row invocation contract (direct children only; a nested
/// parent's OWN already-computed rollup stands in for its raw Start/End/
/// Progress, which is what makes a multi-level hierarchy roll up
/// transitively without <c>RollupMath</c> itself needing to know anything
/// about recursion).
///
/// A <c>readonly record struct</c> (not a class) — mirrors
/// <see cref="GanttVisibleRow"/>'s own choice: one of these is computed per
/// parent/group row, every render, so avoiding a per-row heap allocation
/// matters the same way it did there (see that type's remarks).
///
/// <c>public</c> — crosses <see cref="Lumeo.GanttTimeline"/>/<see cref="Lumeo.Gantt3"/>'s
/// public <c>RollupMath</c>/<c>SummaryTemplate</c> parameters (same CS0053
/// public-parameter-can't-expose-a-less-accessible-type constraint as
/// <see cref="GanttTaskUpdateSource"/>'s remarks explain). Recorded in
/// PublicAPI.Unshipped.txt; <see cref="EditorBrowsableAttribute"/>(Never)
/// keeps it out of consumer IntelliSense until the Phase-4 rename, per this
/// task's explicit instruction — DO-NOT-PROMOTE, same as every other
/// GanttV3-namespaced type added this campaign.
/// </summary>
/// <param name="Start">The earliest effective Start across the row's direct children (a nested parent contributes its OWN rolled-up Start, not its raw one).</param>
/// <param name="End">The latest effective End across the row's direct children, same substitution rule as <paramref name="Start"/>.</param>
/// <param name="WeightedProgress">
/// Duration-weighted mean progress across the row's direct children, 0-100
/// (fractional — the renderer rounds for display). See
/// <see cref="GanttRollupModel"/>'s remarks for the exact weighting formula
/// and how a zero-duration milestone child is handled without dividing by
/// zero or dropping out of the average.
/// </param>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly record struct GanttRollup(DateTime Start, DateTime End, double WeightedProgress);

/// <summary>
/// One extra tree-pane column rendered after the pinned name column (design
/// spec Phase 3, T5 — REUI "Multi-column task tree"). The name column itself
/// (indent + expander chrome + label/<c>RowTemplate</c>) is never one of
/// these — it is not resizable-per-column, always first, and always pinned;
/// see <c>Lumeo.GanttTree</c>'s own remarks for why the two are kept
/// structurally separate rather than modeling the name column as "just
/// another <see cref="GanttTreeColumn"/>".
///
/// <c>public</c> — crosses <c>Lumeo.Gantt3</c>/<c>Lumeo.GanttTree</c>'s public
/// <c>IReadOnlyList&lt;GanttTreeColumn&gt;? TreeColumns</c> parameters (same
/// CS0053 public-parameter-can't-expose-a-less-accessible-type constraint as
/// <see cref="GanttTaskUpdateSource"/>'s remarks explain). Recorded in
/// PublicAPI.Unshipped.txt; <see cref="EditorBrowsableAttribute"/>(Never)
/// keeps it out of consumer IntelliSense until the Phase-4 rename —
/// DO-NOT-PROMOTE, same as every other GanttV3-namespaced type added this
/// campaign.
/// </summary>
/// <param name="Title">Header-row label for this column (rendered as plain text — no v2/REUI localization contract for a CONSUMER-supplied column title, same as <c>GanttTask.Name</c> itself).</param>
/// <param name="Width">Fixed pixel width. Unlike the pinned name column (resized by the splitter — see <c>TreePaneWidth</c>), extra columns are NOT individually resizable in T5's scope; a consumer wanting a wider column sets a bigger <see cref="Width"/> directly.</param>
/// <param name="CellTemplate">
/// Per-row cell content for a TASK row (<c>GanttVisibleRow.Task</c> non-null).
/// Never invoked for a <c>GanttRowKind.GroupHeader</c> row (no
/// <see cref="GanttTask"/> to feed it) — that row's cell for this column
/// renders empty, preserving column alignment without a synthetic/null task.
/// </param>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record GanttTreeColumn(string Title, int Width, Microsoft.AspNetCore.Components.RenderFragment<GanttTask> CellTemplate);

/// <summary>
/// The splitter-relevant fields <see cref="Lumeo.GanttTree"/> pushes to the JS
/// splitter-drag engine (design spec Phase 3, T5) — the SAME "record instead
/// of a growing options bag" idiom <see cref="GanttInteropOptions"/> already
/// established for the move/resize/progress drag engine (see its own
/// remarks): a record's structural equality lets <c>GanttTree</c> skip the
/// <c>GanttV3RegisterSplitterDragAsync</c> interop round-trip whenever nothing
/// drag-relevant actually changed, with no second drag idiom introduced.
/// </summary>
/// <param name="Width">The pinned name column's CURRENT effective width in pixels — the live-drag start point the JS side snapshots at pointerdown (mirrors <c>GanttInteropOptions</c>'s own "JS never re-derives, C# is the source of truth" discipline).</param>
/// <param name="MinWidth"><see cref="Lumeo.GanttV3.GanttScale.MinTreePaneWidth"/> — the live JS-side floor during drag.</param>
/// <param name="MaxWidth"><see cref="Lumeo.GanttV3.GanttScale.MaxTreePaneWidth"/> — the live JS-side ceiling during drag.</param>
internal readonly record struct GanttSplitterOptions(double Width, double MinWidth, double MaxWidth);
