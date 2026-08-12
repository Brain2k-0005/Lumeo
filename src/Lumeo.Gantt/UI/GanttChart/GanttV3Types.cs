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
/// <c>GanttChart</c>'s public <c>OnTaskUpdate</c> gate parameter (both
/// unavoidably public components — see <see cref="GanttRowKind"/>'s remarks
/// for the CS0053/Razor-compiler background), so a less-accessible type here
/// would not compile. Promoted (Phase 4): recorded in PublicAPI.Shipped.txt,
/// <c>[EditorBrowsable(Never)]</c> lifted.
/// </summary>
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
    /// <summary>
    /// The task's Start/End (or just End, for a keyboard resize) changed via
    /// keyboard nudging on a focused bar — Shift+Arrow to move, Shift+Up/Down
    /// to resize the End edge (see <see cref="Lumeo.GanttTimeline"/>'s own
    /// keyboard-navigation remarks). Mirrors ReUI's own keyboard-drag
    /// behavior, which tags every such edit with a single flat "keyboard"
    /// source regardless of whether it was a move or a resize — this enum
    /// does the same rather than adding separate KeyboardMove/
    /// KeyboardResizeEnd members, so a consumer's <c>OnTaskUpdate</c> gate can
    /// cheaply special-case "this came from the keyboard, not a mouse drag"
    /// with one check.
    /// </summary>
    Keyboard,
}

/// <summary>
/// Unified commit-gate payload (design spec "Public API" &gt; Additive &gt;
/// "Unified commit gate with source discriminator (onEventUpdate analog) ...
/// OnTaskUpdate(GanttTaskUpdate{Task, Source})"). Raised by <see
/// cref="Lumeo.GanttTimeline"/> and evaluated by <see cref="Lumeo.GanttChart"/>'s
/// <c>OnTaskUpdate</c> gate BEFORE it commits — see <see cref="GanttUpdateResult"/>
/// for the reject/accept/adjust verdict a consumer's <c>OnTaskUpdate</c> returns.
///
/// <c>public</c> — see <see cref="GanttTaskUpdateSource"/>'s remarks for why
/// (crosses the same public delegate-parameter boundary). Promoted (Phase 4):
/// recorded in PublicAPI.Shipped.txt, <c>[EditorBrowsable(Never)]</c> lifted.
/// </summary>
/// <param name="Task">
/// The task/milestone as it WOULD be committed if <c>OnTaskUpdate</c> accepts
/// (Phase 4 — before promotion this was already-committed; see
/// <see cref="GanttUpdateResult"/>'s own remarks for the pre-commit-gate change).
/// </param>
/// <param name="Source">Which gesture produced this update.</param>
public sealed record GanttTaskUpdate(GanttTask Task, GanttTaskUpdateSource Source);

/// <summary>
/// One arrow-key press on a focused <see cref="Lumeo.GanttBar"/> (wheel-zoom's
/// keyboard-navigation sibling feature). Raised by <see cref="Lumeo.GanttBar"/>'s
/// own <c>OnKeyNavigation</c> and consumed by <see cref="Lumeo.GanttTimeline"/>,
/// which decides what it means: with no modifier, roving focus moves to the
/// previous/next visible row's bar; with <see cref="ShiftKey"/> held, the
/// FOCUSED task's own schedule is nudged instead (Left/Right moves the whole
/// task by a day, Up/Down grows/shrinks its End edge by a day) and routed
/// through the same <c>OnTaskUpdate</c> commit gate a mouse drag uses, tagged
/// <see cref="GanttTaskUpdateSource.Keyboard"/>.
///
/// <c>public</c> — crosses <see cref="Lumeo.GanttBar"/>'s public
/// <c>EventCallback&lt;GanttBarKeyNavigation&gt; OnKeyNavigation</c> parameter
/// (same CS0053 public-parameter-can't-expose-a-less-accessible-type
/// constraint <see cref="GanttTaskUpdateSource"/>'s remarks explain).
/// </summary>
/// <param name="Task">The bar that had focus when the key was pressed.</param>
/// <param name="Key">The raw <c>KeyboardEvent.key</c> value — one of "ArrowUp"/"ArrowDown"/"ArrowLeft"/"ArrowRight".</param>
/// <param name="ShiftKey">Whether Shift was held — the focus-move/schedule-edit discriminator (see the class remarks).</param>
public sealed record GanttBarKeyNavigation(GanttTask Task, string Key, bool ShiftKey);

/// <summary>
/// A candidate override for a commit's Start/End/Progress, offered by a
/// <c>GanttChart.OnTaskUpdate</c> gate when it accepts a
/// <see cref="GanttTaskUpdate"/> but wants to snap it to different values than
/// the raw proposal (e.g. round a drag to the nearest business-hour boundary
/// instead of merely refusing an out-of-hours drop). Mirrors ReUI's
/// <c>onEventUpdate</c> return shape (<c>{ start?, end?, allDay? }</c>) — see
/// <see cref="GanttUpdateResult"/>. <c>AllDay</c> has no Gantt-task analog (a
/// <see cref="GanttTask"/> is always a date range, never an all-day flag), so
/// this substitutes <see cref="Progress"/> instead: the ONE other field every
/// <see cref="GanttChart.OnTaskUpdate"/> commit can carry (progress-handle
/// drags flow through the exact same gate as move/resize/create), which ReUI's
/// own Gantt has no equivalent gesture for at all. Any member left
/// <c>null</c> keeps the corresponding proposed value from <see
/// cref="GanttTaskUpdate.Task"/> unchanged.
/// </summary>
/// <param name="Start">Overrides the committed task's Start when set.</param>
/// <param name="End">Overrides the committed task's End when set.</param>
/// <param name="Progress">Overrides the committed task's Progress (0-100) when set.</param>
public sealed record GanttUpdateAdjustment(DateTime? Start = null, DateTime? End = null, int? Progress = null);

/// <summary>
/// The three-way verdict a <c>GanttChart.OnTaskUpdate</c> gate returns for a
/// proposed <see cref="GanttTaskUpdate"/>: reject it outright (nothing commits,
/// <see cref="Lumeo.GanttChart.TasksChanged"/>/<c>OnDateChange</c>/etc. never
/// fire), accept it exactly as proposed, or accept it WITH a <see
/// cref="GanttUpdateAdjustment"/> that snaps the eventually-committed
/// Start/End/Progress to different values than what was proposed. This is the
/// ONE breaking change in the Phase-4 promotion (design spec Wave E1 / the
/// ReUI-parity audit's E1): <c>OnTaskUpdate</c> was a post-commit
/// <c>EventCallback&lt;GanttTaskUpdate&gt;</c> notification through Phase 3 —
/// the edit had already landed in <c>GanttState</c> by the time it fired, so it
/// could not reject or adjust anything. Converting it to a real gate BEFORE
/// promotion (rather than after, once a consumer could depend on the old
/// void/notification shape) is what keeps this free — see the promotion PR
/// body for the full reasoning.
///
/// Mirrors <c>Lumeo.SchedulerDropResult</c> (<c>src/Lumeo.Scheduler/UI/
/// SchedulerViews/SchedulerInteractionTypes.cs</c>) by design, for the same
/// "one accept/reject/adjust shape across the library" reasoning that type's
/// own remarks give — deliberately not a NEW pattern invented just for Gantt.
///
/// An implicit <c>bool</c> conversion keeps the common "just let it through"
/// case exactly as simple as returning a raw <c>bool</c> — e.g.
/// <c>OnTaskUpdate="update => true"</c> compiles directly against the
/// <c>Func&lt;GanttTaskUpdate, GanttUpdateResult&gt;</c> parameter type, and
/// leaving <c>OnTaskUpdate</c> unset (<c>null</c>, the default) keeps every
/// edit accepted unconditionally — byte-identical to Phase 1-3's own
/// "no OnTaskUpdate handler wired up" behavior.
/// </summary>
public readonly struct GanttUpdateResult : IEquatable<GanttUpdateResult>
{
    /// <summary>Whether the update is permitted at all (with or without an <see cref="Adjustment"/>).</summary>
    public bool Accepted { get; }

    /// <summary>
    /// When set, the Start/End/Progress a consumer wants committed instead of
    /// the raw <see cref="GanttTaskUpdate.Task"/> proposal. Always <c>null</c>
    /// when <see cref="Accepted"/> is <c>false</c> (a rejected update has
    /// nothing to adjust).
    /// </summary>
    public GanttUpdateAdjustment? Adjustment { get; }

    private GanttUpdateResult(bool accepted, GanttUpdateAdjustment? adjustment)
    {
        Accepted = accepted;
        Adjustment = accepted ? adjustment : null;
    }

    /// <summary>Rejects the update outright — nothing commits.</summary>
    public static readonly GanttUpdateResult Reject = new(false, null);

    /// <summary>Accepts the update exactly as proposed — no adjustment.</summary>
    public static readonly GanttUpdateResult Accept = new(true, null);

    /// <summary>Accepts the update, but commits <paramref name="adjustment"/>'s values instead of the raw proposal.</summary>
    public static GanttUpdateResult AcceptWith(GanttUpdateAdjustment adjustment) => new(true, adjustment);

    /// <summary>Keeps <c>OnTaskUpdate="update => true"</c>/<c>false</c> lambdas source-compatible with the delegate type.</summary>
    public static implicit operator GanttUpdateResult(bool accepted) => accepted ? Accept : Reject;

    public bool Equals(GanttUpdateResult other) => Accepted == other.Accepted && Equals(Adjustment, other.Adjustment);
    public override bool Equals(object? obj) => obj is GanttUpdateResult other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Accepted, Adjustment);
    public static bool operator ==(GanttUpdateResult left, GanttUpdateResult right) => left.Equals(right);
    public static bool operator !=(GanttUpdateResult left, GanttUpdateResult right) => !left.Equals(right);
}

/// <summary>
/// Payload for a completed tree-pane row reorder (design spec "Public API" &gt;
/// Additive &gt; <c>AllowRowReorder</c> + <c>EventCallback&lt;GanttRowReorder&gt;</c>).
/// Raised once, after drop, with the task's old and new position.
///
/// <c>public</c> (design spec Phase 3, T6): promoted the moment <c>GanttChart</c>/
/// <c>GanttTree</c> actually wire up drag-drop reorder. Recorded in
/// PublicAPI.Shipped.txt as of the Phase-4 promotion.
///
/// <b>Within-parent/within-bucket only</b> (T6 decision #3 — REUI semantics; a
/// cross-parent move is a <see cref="GanttTask.ParentId"/> EDIT, out of this
/// feature's scope by design, never offered through the drag UI at all):
/// <see cref="PreviousParentId"/> and <see cref="NewParentId"/> are therefore
/// ALWAYS equal for any reorder this library itself ever raises — both fields
/// are kept (rather than a single <c>ParentId</c>) only so a future cross-parent
/// feature could reuse this exact record without a breaking shape change.
///
/// <b>Flat <see cref="GanttTask.GroupLabel"/> grouping</b> (no <see
/// cref="GanttTask.ParentId"/> in play — <see cref="GanttRowModel.UsesHierarchy"/>
/// false for the task set): <see cref="PreviousParentId"/>/<see cref="NewParentId"/>
/// are <c>null</c> throughout (a flat task's <c>ParentId</c> genuinely never
/// changes), and <see cref="PreviousIndex"/>/<see cref="NewIndex"/> are the
/// task's index among its OWN <see cref="GanttTask.GroupLabel"/>-sharing siblings
/// (the "bucket" — see <see cref="Lumeo.GanttV3.GanttReorderModel"/>'s own
/// remarks), NOT a global root-level index — reordering across two DIFFERENT
/// groups is exactly as out-of-scope as a cross-parent move in hierarchy mode.
/// </summary>
/// <param name="TaskId">Id of the task/row that was moved.</param>
/// <param name="PreviousParentId">The task's <see cref="GanttTask.ParentId"/> before the move (null = root, or always null in flat-group mode — see the class remarks).</param>
/// <param name="NewParentId">The task's <see cref="GanttTask.ParentId"/> after the move — always equal to <see cref="PreviousParentId"/> (within-bucket only — see the class remarks).</param>
/// <param name="PreviousIndex">Index within the task's reorder bucket (see the class remarks) before the move.</param>
/// <param name="NewIndex">Index within the task's reorder bucket (see the class remarks) after the move.</param>
public sealed record GanttRowReorder(
    string TaskId,
    string? PreviousParentId,
    string? NewParentId,
    int PreviousIndex,
    int NewIndex);

/// <summary>
/// Live drag-drop validation context for TREE-ROW reorder (design spec "Public
/// API" &gt; Additive &gt; <c>AllowRowReorder</c> — the REUI <c>canDropEvent</c>
/// analog applied to row reorder rather than schedule dragging; see <see
/// cref="GanttScheduleDropContext"/>'s own remarks for why THAT is a separate,
/// differently-shaped type). Passed alongside the dragged <see cref="GanttTask"/>
/// to a consumer-supplied <c>CanDropRow</c> predicate evaluated continuously
/// while a row drag is in flight (<c>GanttTree.ValidateRowDrop</c>), so the drop
/// target can be rejected (e.g. "no dropping a parent onto its own descendant")
/// before the user releases the pointer, then re-checked once more at commit
/// time (<c>GanttTree.CommitRowReorder</c>) — same two-phase discipline
/// <c>GanttTimeline.ValidateDrop</c>/<c>CommitDrag</c> already established for
/// schedule dragging.
///
/// <c>public</c> (design spec Phase 3, T6): promoted the moment <c>GanttChart</c>/
/// <c>GanttTree</c> expose a <c>CanDropRow</c> parameter wired to it. Recorded
/// in PublicAPI.Shipped.txt as of the Phase-4 promotion.
///
/// <b>Flat-group mode</b>: <see cref="TargetParentId"/> is always <c>null</c>
/// (a flat task's <see cref="GanttTask.ParentId"/> never changes — see <see
/// cref="GanttRowReorder"/>'s own remarks) even though the candidate drop
/// position is really scoped to one <see cref="GanttTask.GroupLabel"/> bucket;
/// a consumer that needs the group can resolve it from <see cref="TargetTaskId"/>
/// (or from the dragged task's own <c>GroupLabel</c>, unchanged by a
/// within-bucket move).
/// </summary>
/// <param name="TargetParentId">The dragged task's <see cref="GanttTask.ParentId"/> (null = root, or always null in flat-group mode — see the class remarks). Never a DIFFERENT parent than the dragged task's own — reorder is within-bucket only.</param>
/// <param name="TargetIndex">The candidate index within the dragged task's own reorder bucket at the current drop position.</param>
/// <param name="TargetTaskId">Id of the sibling row currently nearest the pointer, if any (null when hovering past the last sibling).</param>
public sealed record GanttDropContext(
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
/// <c>public</c> (not <c>internal</c>): crosses <see cref="Lumeo.GanttChart"/>/
/// <see cref="Lumeo.GanttTimeline"/>'s public <c>Func&lt;GanttTask,
/// GanttScheduleDropContext, bool&gt;? CanDrop</c> parameters (same CS0053
/// public-parameter-can't-expose-a-less-accessible-type constraint as
/// <see cref="GanttTaskUpdateSource"/>'s remarks explain). Recorded in
/// PublicAPI.Shipped.txt as of the Phase-4 promotion.
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
/// <c>public</c> — crosses <see cref="Lumeo.GanttTimeline"/>/<see cref="Lumeo.GanttChart"/>'s
/// public <c>RollupMath</c>/<c>SummaryTemplate</c> parameters (same CS0053
/// public-parameter-can't-expose-a-less-accessible-type constraint as
/// <see cref="GanttTaskUpdateSource"/>'s remarks explain). Recorded in
/// PublicAPI.Shipped.txt as of the Phase-4 promotion.
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
/// <c>public</c> — crosses <c>Lumeo.GanttChart</c>/<c>Lumeo.GanttTree</c>'s public
/// <c>IReadOnlyList&lt;GanttTreeColumn&gt;? TreeColumns</c> parameters (same
/// CS0053 public-parameter-can't-expose-a-less-accessible-type constraint as
/// <see cref="GanttTaskUpdateSource"/>'s remarks explain). Recorded in
/// PublicAPI.Shipped.txt as of the Phase-4 promotion.
/// </summary>
/// <param name="Title">Header-row label for this column (rendered as plain text — no v2/REUI localization contract for a CONSUMER-supplied column title, same as <c>GanttTask.Name</c> itself).</param>
/// <param name="Width">Fixed pixel width. Unlike the pinned name column (resized by the splitter — see <c>TreePaneWidth</c>), extra columns are NOT individually resizable in T5's scope; a consumer wanting a wider column sets a bigger <see cref="Width"/> directly.</param>
/// <param name="CellTemplate">
/// Per-row cell content for a TASK row (<c>GanttVisibleRow.Task</c> non-null).
/// Never invoked for a <c>GanttRowKind.GroupHeader</c> row (no
/// <see cref="GanttTask"/> to feed it) — that row's cell for this column
/// renders empty, preserving column alignment without a synthetic/null task.
/// </param>
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
