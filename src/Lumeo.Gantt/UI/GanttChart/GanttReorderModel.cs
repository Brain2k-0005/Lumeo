namespace Lumeo.GanttV3;

/// <summary>
/// Pure, static row-reorder computation for the GanttV3 tree pane (design spec
/// Phase 3, T6 — "Row reorder: ... reorder is within-parent only (REUI
/// semantics; cross-parent moves are ParentId edits = out of scope,
/// document)"). Mirrors <see cref="GanttRowModel"/>/<see cref="GanttRollupModel"/>'s
/// own shape: no Blazor/DOM dependency, unit-testable in isolation.
///
/// ── The "bucket" concept (T6 decision #3) ──
///
/// A reorder never crosses two different "buckets" of siblings: in hierarchy
/// mode, a task's bucket is its own <see cref="GanttTask.ParentId"/> (null =
/// the root bucket); in flat <see cref="GanttTask.GroupLabel"/>-grouped mode
/// (no <see cref="GanttTask.ParentId"/> in play — <see
/// cref="GanttRowModel.UsesHierarchy"/> false), a task's bucket is its own
/// normalized <see cref="GanttTask.GroupLabel"/> (null = the ungrouped-root
/// bucket) — reordering within a GroupLabel bucket is the flat-mode analog of
/// within-parent reordering, since <see cref="GanttRowModel"/>'s own pinned
/// "Hierarchie schlaegt GroupBy" rule means a task set is never in both modes
/// at once. <see cref="GanttRowReorder.PreviousParentId"/>/<see
/// cref="GanttRowReorder.NewParentId"/> stay <c>null</c> throughout flat mode
/// (a flat task's ParentId genuinely never changes) even though the bucket
/// key used internally here is the GroupLabel — see that type's own remarks.
///
/// ── Index semantics ──
///
/// An index is always relative to the task's OWN bucket (<see
/// cref="SiblingsOf"/>'s own ordering, i.e. the task set's ORIGINAL list
/// order filtered to same-bucket members) — never a position in the full task
/// list or in the rendered/collapse-filtered row list.
/// </summary>
internal static class GanttReorderModel
{
    /// <summary>
    /// <paramref name="task"/>'s reorder bucket key — <see cref="GanttTask.ParentId"/>
    /// in hierarchy mode, its own normalized <see cref="GanttTask.GroupLabel"/>
    /// otherwise (see the class remarks). <c>null</c> represents "the root
    /// bucket" in EITHER mode — safe to compare with <c>==</c> since both
    /// branches normalize an empty label to <c>null</c> the same way <see
    /// cref="GanttRowModel.BuildFlatGroupRows"/>/<see
    /// cref="GanttRollupModel.ComputeGroupRollups"/> already do.
    /// </summary>
    internal static string? BucketKey(IReadOnlyList<GanttTask> tasks, GanttTask task) =>
        BucketKey(GanttRowModel.UsesHierarchy(tasks), task);

    /// <summary>
    /// Same as <see cref="BucketKey(IReadOnlyList{GanttTask}, GanttTask)"/>, for a
    /// caller that already knows <see cref="GanttRowModel.UsesHierarchy"/> for the
    /// task set (design spec Phase 3, T7 — <c>Lumeo.GanttTimeline</c>'s own
    /// <c>ColorByGroup</c> resolution needs this key once PER RENDERED ROW; calling
    /// the <c>tasks</c>-taking overload there would re-run <see
    /// cref="GanttRowModel.UsesHierarchy"/>'s own O(n) scan for EVERY row instead
    /// of once for the whole render, the exact O(rows) redundant-scan class <see
    /// cref="Lumeo.GanttV3.GanttReorderModel.ComputeBucketPositions"/> already
    /// avoids for the identical reason — see that method's own remarks).
    /// </summary>
    internal static string? BucketKey(bool usesHierarchy, GanttTask task) =>
        usesHierarchy ? task.ParentId : NormalizedGroupLabel(task.GroupLabel);

    private static string? NormalizedGroupLabel(string? label) => string.IsNullOrEmpty(label) ? null : label;

    /// <summary>
    /// Every task in <paramref name="tasks"/> sharing <paramref name="task"/>'s
    /// own reorder bucket (<see cref="BucketKey(IReadOnlyList{GanttTask}, GanttTask)"/>), INCLUDING <paramref
    /// name="task"/> itself, in the task set's own original relative order —
    /// the same "preserve caller order, group by key" discipline <see
    /// cref="GanttRowModel.BuildHierarchyRows"/>'s own children-by-parent map
    /// and <see cref="GanttRollupModel.ComputeGroupRollups"/>'s own
    /// members-by-group map both already apply.
    /// </summary>
    internal static IReadOnlyList<GanttTask> SiblingsOf(IReadOnlyList<GanttTask> tasks, GanttTask task)
    {
        var hierarchy = GanttRowModel.UsesHierarchy(tasks);
        var key = hierarchy ? task.ParentId : NormalizedGroupLabel(task.GroupLabel);
        var result = new List<GanttTask>();
        foreach (var t in tasks)
        {
            var tKey = hierarchy ? t.ParentId : NormalizedGroupLabel(t.GroupLabel);
            if (tKey == key) result.Add(t);
        }
        return result;
    }

    /// <summary>Sentinel used in place of a <c>null</c> bucket key (the root bucket, either mode — see <see cref="BucketKey(IReadOnlyList{GanttTask}, GanttTask)"/>) so a caller (e.g. a <c>data-reorder-bucket</c> DOM attribute) always has a concrete, non-null string to compare against.</summary>
    internal const string RootBucketSentinel = "\0root";

    /// <summary>
    /// Bulk, O(n) single-pass equivalent of calling <see cref="BucketKey(IReadOnlyList{GanttTask}, GanttTask)"/> +
    /// "this task's index within <see cref="SiblingsOf"/>" for every task in
    /// <paramref name="tasks"/> — <see cref="Lumeo.GanttTree"/>'s own rendering
    /// needs BOTH per row, and computing them independently per row (an O(n)
    /// <see cref="SiblingsOf"/> scan for EACH of up to a virtualized viewport's
    /// worth of rows) would multiply an otherwise-O(n) cost by the rendered row
    /// count for no reason — a single running-counter-per-bucket pass produces
    /// the IDENTICAL ordering <see cref="SiblingsOf"/> would (both walk
    /// <paramref name="tasks"/> in the caller's own original order), so this is
    /// exactly equivalent, just computed once. <see cref="RootBucketSentinel"/>
    /// stands in for a <c>null</c> bucket key.
    /// </summary>
    internal static IReadOnlyDictionary<string, (string BucketKey, int Index)> ComputeBucketPositions(IReadOnlyList<GanttTask> tasks)
    {
        var hierarchy = GanttRowModel.UsesHierarchy(tasks);
        var result = new Dictionary<string, (string BucketKey, int Index)>(tasks.Count);
        var counters = new Dictionary<string, int>();
        foreach (var t in tasks)
        {
            var bucket = (hierarchy ? t.ParentId : NormalizedGroupLabel(t.GroupLabel)) ?? RootBucketSentinel;
            var index = counters.TryGetValue(bucket, out var c) ? c : 0;
            counters[bucket] = index + 1;
            result[t.Id] = (bucket, index);
        }
        return result;
    }

    /// <summary>
    /// Returns a NEW task list with the task identified by <paramref
    /// name="taskId"/> relocated to sit at <paramref
    /// name="newIndexWithinBucket"/> (clamped) among its OWN bucket siblings
    /// (see <see cref="SiblingsOf"/>) — every OTHER (non-sibling) task keeps
    /// its EXACT original list position; only the "slots" already occupied by
    /// bucket-sibling members are redistributed among the reordered sibling
    /// sequence. This is what makes the operation safe to apply directly to
    /// the caller's own (possibly task-interleaved) <see
    /// cref="GanttTask"/> list without disturbing anything outside the one
    /// bucket being reordered.
    ///
    /// Operates against <paramref name="tasks"/> UNFILTERED (the full,
    /// caller-owned list — see <see cref="Lumeo.GanttChart.HandleRowReorderAsync"/>'s
    /// own remarks for why): bucket MEMBERSHIP/ordering is computed over the
    /// <see cref="GanttRowModel.FilterValidDurationTasks"/>-filtered subset
    /// (matching what <see cref="Lumeo.GanttTree"/> actually rendered
    /// indices against), but a filtered-OUT (invalid-duration) task is never
    /// itself a member of that subset, so it always falls through the "every
    /// other task keeps its position" branch below untouched — the exact
    /// behavior wanted (a row nobody could see or drag never moves).
    ///
    /// No-ops (returns <paramref name="tasks"/> unchanged) when <paramref
    /// name="taskId"/> doesn't resolve, when it resolves to a
    /// filtered-out/invalid-duration task (never offered a drag grip in the
    /// first place — defensive, matches every other JSInvokable's "public
    /// surface, don't trust it blindly" posture in this campaign), or when
    /// the clamped target index equals the task's current index (a genuine
    /// no-op move, mirrors <c>GanttTimeline.CommitDrag</c>'s own
    /// <c>movedDays == 0</c> no-op-commit rule).
    /// </summary>
    internal static IReadOnlyList<GanttTask> Move(IReadOnlyList<GanttTask> tasks, string taskId, int newIndexWithinBucket)
    {
        GanttTask? moving = null;
        foreach (var t in tasks)
        {
            if (t.Id == taskId) { moving = t; break; }
        }
        if (moving is null) return tasks;
        if (!GanttRowModel.HasValidDuration(moving)) return tasks;

        var visible = GanttRowModel.FilterValidDurationTasks(tasks);
        var siblings = new List<GanttTask>(SiblingsOf(visible, moving));
        var oldIndex = siblings.FindIndex(t => t.Id == taskId);
        if (oldIndex < 0) return tasks; // unreachable in practice — `moving` has valid duration, so it's always in `visible`'s sibling set

        // Bug fix (Codex review, P1 #1): `newIndexWithinBucket` arrives from
        // the caller (gantt-v3.js's siblingRows()/resolveTarget()) in the
        // PRE-removal, full-bucket index space — siblingRows() deliberately
        // excludes the moving row itself, but each candidate's own
        // `data-reorder-index` attribute is still its ORIGINAL position among
        // ALL siblings (moving task included), so "insert after candidate C"
        // sends C's original index + 1 — which legitimately reaches
        // `siblings.Count` (one past the last valid original index) when C is
        // the LAST sibling, i.e. "drag to the very end". The clamp upper
        // bound must therefore be `siblings.Count` (inclusive "append"
        // position), not `siblings.Count - 1` — the old bound silently
        // conflated "insert immediately before the last sibling" with
        // "append after it" into the same clamped value, corrupting exactly
        // the cases GanttReorderModelTests' "downward" cases now cover.
        var clamped = Math.Clamp(newIndexWithinBucket, 0, siblings.Count);
        if (clamped == oldIndex) return tasks;

        siblings.RemoveAt(oldIndex);
        // `siblings` has now had `moving` removed, shrinking to Count-1 —
        // `clamped` (still in the PRE-removal, Count-slot index space) is
        // only a valid insert position AS-IS when it sits AT OR BEFORE the
        // moving task's old slot (nothing before it shifted). When the
        // target sits AFTER that old slot (clamped > oldIndex), the removal
        // already shifted every later index down by one, so the insert
        // position must be decremented by one to land immediately after the
        // hovered candidate (or truly at the end for the "append" case)
        // instead of one slot too far.
        var insertIndex = clamped > oldIndex ? clamped - 1 : clamped;
        siblings.Insert(insertIndex, moving);

        var siblingIds = new HashSet<string>(siblings.Count, StringComparer.Ordinal);
        foreach (var s in siblings) siblingIds.Add(s.Id);

        var result = new List<GanttTask>(tasks.Count);
        var cursor = 0;
        foreach (var t in tasks)
        {
            if (siblingIds.Contains(t.Id))
            {
                result.Add(siblings[cursor]);
                cursor++;
            }
            else
            {
                result.Add(t);
            }
        }
        return result;
    }
}
