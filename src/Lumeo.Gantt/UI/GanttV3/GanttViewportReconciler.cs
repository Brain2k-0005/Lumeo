using Lumeo.Services;

namespace Lumeo.GanttV3;

/// <summary>
/// A value snapshot of every input that can move the GanttV3 viewport
/// (design spec Phase 2; Codex round 14 consolidation). Captured once per
/// reconcile pass in <c>Gantt3</c> and diffed against the previous pass's
/// snapshot by <see cref="GanttViewportReconciler.Decide"/> — the single
/// replacement for the per-parameter tracking fields
/// (<c>_lastSeenDirection</c>/<c>_lastSeenEffectiveShowTreePane</c>/
/// <c>_lastSeenColumnWidth</c>/<c>_lastPushedViewMode</c>) that rounds 11-13
/// accreted one at a time.
/// </summary>
/// <param name="TasksVersion">A monotonic counter <c>Gantt3</c> bumps whenever the
/// effective task set actually changes (structural inequality via
/// <c>GanttState</c>) — a diff on it is the exact <c>tasksChanged</c> signal,
/// captured as a plain value so the whole decision stays a pure snapshot diff.</param>
/// <param name="RenderableEmpty">Whether the FILTERED/renderable row set is empty —
/// i.e. after <see cref="GanttRowModel.FilterValidDurationTasks"/>, the same set
/// that actually reaches the panes (Codex round 14, finding #2). A raw task count
/// would misclassify a list of only invalid-duration tasks as "populated".</param>
/// <param name="ViewMode">The mode the chart is reconciling TO — the incoming
/// parameter for a controlled binding, or the committed <c>GanttState.ViewMode</c>
/// for an uncontrolled one whose parameter re-render must not revert a toolbar zoom.</param>
/// <param name="ColumnWidth">The effective pixel-per-column width for
/// <see cref="ViewMode"/> — a caller's override when set, else the mode's own
/// default. Computed against THIS snapshot's own <see cref="ViewMode"/>, so a mode
/// switch that changes the default width records the width consistent with the mode
/// it belongs to (Codex round 14, finding #1 — the old code recorded the OUTGOING
/// mode's default and then saw a spurious change on the next echo).</param>
/// <param name="ShowTreePane">The resolved <c>EffectiveShowTreePane</c> — a tree-pane
/// visibility change shifts the timeline's leading offset and so needs a recenter.</param>
/// <param name="Direction">The resolved ambient <see cref="LayoutDirection"/> — a flip
/// re-interprets the physical scroll position (RTL normalization), so it triggers a
/// center-preserving recenter even when the leading offset itself is unchanged.</param>
internal readonly record struct GanttViewportSnapshot(
    int TasksVersion,
    bool RenderableEmpty,
    GanttViewMode ViewMode,
    int ColumnWidth,
    bool ShowTreePane,
    LayoutDirection Direction)
{
    /// <summary>
    /// Pixels of the shared scroll pane preceding the timeline's origin under THIS
    /// snapshot's geometry — a sibling tree pane's width in LTR, else 0. The exact
    /// value a live-scroll-center reading captured under this snapshot must be
    /// decoded against (see <c>Gantt3.ResolveCurrentCenterDateAsync</c>).
    /// </summary>
    public double LeadingOffset => GanttViewportGeometry.LeadingOffset(ShowTreePane, Direction);
}

/// <summary>Where the reconciled <c>VisibleRange</c> comes from for a pass.</summary>
internal enum GanttRangeSource
{
    /// <summary>Leave <c>VisibleRange</c> untouched — only the scroll target (if any) moves.</summary>
    Keep,

    /// <summary>Rebuild from the (new) task set's own min/max via <c>ComputeInitialRange</c>.</summary>
    TaskDerived,

    /// <summary>Rebuild as a padded window self-centered on the captured live center (a genuine mode switch reshapes columns).</summary>
    SelfCenteredOnCapture,
}

/// <summary>What date the one-shot scroll intent targets for a pass.</summary>
internal enum GanttScrollTarget
{
    /// <summary>No scroll intent is emitted — the DOM keeps its current position.</summary>
    None,

    /// <summary>The captured live-scroll center (preserve what the user was looking at). Requires a capture.</summary>
    CapturedCenter,

    /// <summary>An emptiness transition: Today when it falls inside the new range, else the new range's own midpoint.</summary>
    TodayOrMidpoint,
}

/// <summary>The outcome of one snapshot diff.</summary>
/// <param name="NeedsLiveCenterCapture">True when the pass must read the pane's live
/// scroll center (under the OLD geometry) before committing anything — i.e. whenever
/// <see cref="Target"/> is <see cref="GanttScrollTarget.CapturedCenter"/> or the range
/// self-centers on that capture.</param>
/// <param name="Range">Where the reconciled <c>VisibleRange</c> comes from.</param>
/// <param name="Target">What the one-shot scroll intent targets.</param>
internal readonly record struct GanttViewportDecision(
    bool NeedsLiveCenterCapture,
    GanttRangeSource Range,
    GanttScrollTarget Target);

/// <summary>Shared geometry helpers for the GanttV3 viewport reconcile.</summary>
internal static class GanttViewportGeometry
{
    /// <summary>
    /// The timeline's leading offset for a given (tree-pane, direction) pair. Only
    /// LTR-with-tree adds the tree's width before the timeline's origin — under RTL
    /// the outer flex row reverses, so the timeline already sits at the content's
    /// physical-left origin (see <c>Gantt3.ScrollHostLeadingOffset</c>'s remarks).
    /// </summary>
    public static double LeadingOffset(bool showTreePane, LayoutDirection direction) =>
        showTreePane && direction == LayoutDirection.Ltr ? GanttScale.TreePaneWidth : 0;
}

/// <summary>
/// The single owner of GanttV3 viewport intent (Codex round 14 consolidation).
/// Pure decision logic: given the previous and current <see cref="GanttViewportSnapshot"/>,
/// <see cref="Decide"/> returns exactly how the range and the (one-shot) scroll
/// intent must move — the whole "tasksChanged × viewModeChanged × emptiness ×
/// geometry" matrix that rounds 11-13 grew across three separate methods, derived
/// here from ONE diff instead of scattered per-parameter flags. Stateless and free
/// of any Blazor/DOM dependency, so the matrix is unit-testable directly on
/// snapshot pairs (mirrors how <c>GanttScale</c>/<c>GanttState</c> were built).
///
/// <c>Gantt3</c> owns the execution around a decision: capture the live center
/// (when required) under the OLD geometry BEFORE committing anything, then commit
/// tasks/mode/range synchronously (one coherent frame — Codex round 14, finding
/// #4), then emit the one-shot scroll intent.
/// </summary>
internal static class GanttViewportReconciler
{
    /// <summary>
    /// Decides how the viewport reconciles from <paramref name="prev"/> to
    /// <paramref name="next"/>. The matrix, derived from the two snapshots plus
    /// <paramref name="taskRangeDisjoint"/>:
    ///
    /// <code>
    /// tasksChanged | viewModeChanged | range                | target
    /// ------------ | --------------- | -------------------- | -----------------------------------------
    /// true         | (either)        | TaskDerived          | emptiness transition ? TodayOrMidpoint
    ///              |                 |                      |   : (mode/geometry changed ? CapturedCenter
    ///              |                 |                      |   : (disjoint range ? TodayOrMidpoint : None))
    /// false        | true            | SelfCenteredOnCapture| CapturedCenter
    /// false        | false           | Keep                 | geometry changed ? CapturedCenter : None
    /// </code>
    ///
    /// "geometry changed" = any of tree-pane / direction / column-width moved. A
    /// pure geometry change never rebuilds the range (only the scroll target moves);
    /// a genuine mode switch reshapes it around the captured center; a task-set
    /// change re-derives it from the new tasks. An emptiness transition (renderable
    /// empty ⇄ populated) always wins the target, since preserving a live center
    /// across it is meaningless.
    ///
    /// <paramref name="taskRangeDisjoint"/> (Codex round 16, finding #4) only
    /// matters in the "nothing else changed" corner: a PLAIN task replacement
    /// (viewMode/geometry both unchanged) whose new task-derived range doesn't
    /// overlap the range currently in effect at all used to leave the DOM's own
    /// untouched scroll position in place under None — meaningless once the range
    /// becomes disjoint from it, landing on empty space unrelated to either the
    /// old or the new tasks. Deliberately does NOT override the mode/geometry-
    /// changed branch above: a tasksChanged+viewModeChanged combination (round
    /// 12/14's own case 4 — an async-loaded task swap arriving together with a
    /// mode switch) keeps preserving the captured center even across a disjoint
    /// replacement, by design — it never resets to Today just because the new
    /// tasks happen to be far away.
    /// </summary>
    /// <param name="taskRangeDisjoint">True when <paramref name="prev"/> and
    /// <paramref name="next"/> both have tasks (no emptiness transition) but the
    /// newly task-derived date range doesn't overlap the range currently in effect
    /// at all — computed by the caller from actual date data, which this snapshot
    /// type deliberately doesn't carry (see its own remarks: everything here reduces
    /// to plain diffable facts, not full range state) so this decision stays a pure
    /// function of simple, cheaply-computed inputs.</param>
    public static GanttViewportDecision Decide(GanttViewportSnapshot prev, GanttViewportSnapshot next, bool taskRangeDisjoint)
    {
        var tasksChanged = next.TasksVersion != prev.TasksVersion;
        var viewModeChanged = next.ViewMode != prev.ViewMode;
        var geometryChanged =
            next.ColumnWidth != prev.ColumnWidth ||
            next.ShowTreePane != prev.ShowTreePane ||
            next.Direction != prev.Direction;
        var emptinessTransition = next.RenderableEmpty != prev.RenderableEmpty;

        GanttRangeSource range;
        GanttScrollTarget target;

        if (tasksChanged)
        {
            // A new task set always re-derives the range from the new tasks'
            // own min/max (mirrors v2, which never keeps a stale window across
            // a task-set change). The target then depends on emptiness first,
            // else on whether the user's live center is worth preserving.
            //
            // taskRangeDisjoint (round 16 finding #4) ONLY overrides the
            // "nothing else changed" None branch — a tasksChanged+viewModeChanged
            // (or +geometryChanged) combination deliberately keeps preserving the
            // captured center even across a disjoint task-range replacement (round
            // 12/14's own case-4 behavior: an async-loaded task swap arriving
            // together with a mode switch preserves continuity with whatever the
            // user was looking at, never resets to Today just because the new
            // tasks happen to be far away). The dispatch's own finding is scoped
            // to the "with unchanged params" case specifically — a plain task
            // replacement with NOTHING else changing, where None used to mean "the
            // DOM's own untouched scroll position" (meaningless once the range
            // becomes disjoint from it), not "the captured center."
            range = GanttRangeSource.TaskDerived;
            target = emptinessTransition
                ? GanttScrollTarget.TodayOrMidpoint
                : (viewModeChanged || geometryChanged)
                    ? GanttScrollTarget.CapturedCenter
                    : (taskRangeDisjoint ? GanttScrollTarget.TodayOrMidpoint : GanttScrollTarget.None);
        }
        else if (viewModeChanged)
        {
            // A genuine mode switch with no task change reshapes the range
            // (new unit/column width) around whatever the user was looking at.
            range = GanttRangeSource.SelfCenteredOnCapture;
            target = GanttScrollTarget.CapturedCenter;
        }
        else
        {
            // No task or mode change. A tree-pane/direction/column-width change
            // moves only the scroll target — the date RANGE itself is unaffected.
            range = GanttRangeSource.Keep;
            target = geometryChanged ? GanttScrollTarget.CapturedCenter : GanttScrollTarget.None;
        }

        return new GanttViewportDecision(
            NeedsLiveCenterCapture: target == GanttScrollTarget.CapturedCenter,
            Range: range,
            Target: target);
    }
}
