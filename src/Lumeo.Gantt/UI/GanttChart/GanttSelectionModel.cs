namespace Lumeo.GanttV3;

/// <summary>
/// Tri-state selection outcome for a parent/group row (design spec Phase 3, T6
/// — "parent/group checkbox = tri-state select-descendants — follow DataGrid's
/// selection idioms"). Mirrors DataGridHeader's own <c>AllSelected</c>/
/// <c>SomeSelected</c> pair (Checked/IsIndeterminate), collapsed into one value
/// per row rather than two separately-computed bools, since a row is never
/// both.
///
/// <c>internal</c> (unlike <see cref="GanttRollup"/>): never crosses a PUBLIC
/// component parameter — <c>GanttTree</c> computes it from its own public
/// <c>Tasks</c>/<c>SelectedIds</c> parameters and consumes it purely as a
/// private rendering detail (there is no consumer-facing "selection state
/// template" slot in T6's scope the way <c>SummaryTemplate</c> forces
/// <see cref="GanttRollup"/> public).
/// </summary>
internal enum GanttRowSelectionState
{
    /// <summary>No descendant leaf is selected (or, for a leaf row itself, the leaf is not selected).</summary>
    Unselected,

    /// <summary>Some but not all descendant leaves are selected — renders as the checkbox's indeterminate ("mixed") visual.</summary>
    PartiallySelected,

    /// <summary>Every descendant leaf is selected (or, for a leaf row itself, the leaf is selected).</summary>
    Selected,
}

/// <summary>
/// Pure, static selection-state computation for the GanttV3 tree pane's
/// checkbox column (design spec Phase 3, T6 — "Leaf-row checkbox selection...
/// parent/group checkbox = tri-state select-descendants"). Mirrors <see
/// cref="GanttRollupModel"/>'s own shape (no Blazor/DOM dependency,
/// unit-testable in isolation, computed fresh from the same task set <see
/// cref="GanttRowModel"/> builds <see cref="GanttVisibleRow"/>s from) and the
/// SAME "Hierarchie schlaegt GroupBy" branch <see cref="GanttRowModel.UsesHierarchy"/>
/// already governs everywhere else in this campaign — a task set is never in
/// both modes at once, so <see cref="ComputeStates"/> and <see
/// cref="ResolveLeafIds"/> both dispatch on that exact same test.
///
/// ── What <see cref="GanttState.SelectedIds"/> actually contains (T6 decision #1) ──
///
/// ONLY leaf task ids — a hierarchy task with NO children (<see
/// cref="GanttVisibleRow.HasChildren"/> false), or, in flat-group mode, any
/// task row at all (a <see cref="GanttRowKind.GroupHeader"/> row is a
/// synthetic non-task row and can never appear in the set; every real task row
/// in flat mode has no children by construction — see <see
/// cref="GanttRowModel.BuildFlatGroupRows"/>). A hierarchy PARENT's own id is
/// NEVER added to the set, even when 100% of its descendants are — its
/// checkbox is a pure DERIVED view (<see cref="ComputeStates"/>), never itself
/// a member, so "is task X selected" has exactly one unambiguous answer
/// regardless of whether X is a leaf or a parent. This is the literal reading
/// of the REUI-parity matrix's own item name ("Leaf row selection
/// (checkboxes)").
///
/// ── Ignoring collapse (deliberate) ──
///
/// Both <see cref="ComputeStates"/> and <see cref="ResolveLeafIds"/> walk the
/// FULL hierarchy/group membership, never <see cref="GanttState.Collapsed"/> —
/// a parent's tri-state must reflect ALL its descendants, including ones
/// currently hidden behind a collapsed ancestor, and "select all descendants"
/// must actually select the hidden ones too (the common tree-checkbox
/// convention — collapsing a branch must never make its members
/// unselectable). <see cref="GanttRowModel.BuildVisibleRows"/>'s own collapse
/// filtering is therefore irrelevant to this class by design.
/// </summary>
internal static class GanttSelectionModel
{
    /// <summary>
    /// Computes every eligible parent/group row's <see cref="GanttRowSelectionState"/>
    /// for <paramref name="tasks"/> (already <see
    /// cref="GanttRowModel.FilterValidDurationTasks"/>-filtered, the same set
    /// <see cref="GanttRowModel.BuildVisibleRows"/> renders against), keyed by
    /// the SAME string a <see cref="GanttVisibleRow.ToggleKey"/> carries for
    /// that row — mirrors <see cref="GanttRollupModel.ComputeRollups"/>'s own
    /// key convention exactly, so a caller looks this up identically:
    /// <c>row.HasChildren ? states.TryGetValue(row.ToggleKey!, ...) : ...</c>.
    /// A LEAF row never has an entry here — its own state is a direct,
    /// O(1) <paramref name="selectedIds"/> membership check the caller
    /// performs itself (see <see cref="Lumeo.GanttTree"/>'s own
    /// <c>CheckboxState</c>), no dictionary lookup needed.
    /// </summary>
    internal static IReadOnlyDictionary<string, GanttRowSelectionState> ComputeStates(
        IReadOnlyList<GanttTask> tasks, IReadOnlySet<string> selectedIds) =>
        GanttRowModel.UsesHierarchy(tasks) ? ComputeHierarchyStates(tasks, selectedIds) : ComputeGroupStates(tasks, selectedIds);

    /// <summary>
    /// Resolves which LEAF task ids a checkbox click against row identity
    /// <paramref name="key"/> should select/deselect (design spec Phase 3, T6
    /// — "parent/group checkbox = tri-state select-descendants"): <paramref
    /// name="key"/> a plain task id with no children returns just that one id
    /// (a leaf click); a hierarchy parent's own task id returns every
    /// recursive descendant LEAF (never the parent's own id — see the class
    /// remarks on why <see cref="GanttState.SelectedIds"/> never contains a
    /// parent id); a <see cref="GanttRowModel.GroupToggleKey"/>-prefixed group
    /// key (flat mode) returns every member task's id. Returns an empty list
    /// for a <paramref name="key"/> that resolves to nothing in <paramref
    /// name="tasks"/> (a stale key from a race with a task-list update —
    /// same defensive "no-op on a miss" contract every other row-identity
    /// resolver in this campaign uses, e.g. <c>GanttTimeline.CommitCreate</c>'s
    /// own <c>matchedRow is null</c> guard).
    /// </summary>
    internal static IReadOnlyList<string> ResolveLeafIds(IReadOnlyList<GanttTask> tasks, string key)
    {
        if (GanttRowModel.UsesHierarchy(tasks)) return ResolveHierarchyLeafIds(tasks, key);

        // Bug fix (Codex review, P2 #4): a task's own Id is a free-form
        // consumer-supplied string — nothing in GanttTask.Id's own contract
        // reserves GanttRowModel's internal "group::" toggle-key prefix, so a
        // leaf task legitimately named e.g. "group::sprint1" used to be
        // misread as a synthetic group key here (TryGetGroupLabel matching
        // on the prefix BEFORE any task lookup ran), resolving every OTHER
        // member of a same-named group instead of just that one leaf — or
        // an empty result if no group with that label even exists. Resolve
        // an EXACT task id match first (same scan ResolveFlatSingleLeafId
        // already performs); only a key that matches NO real task at all
        // falls through to the group-label interpretation. The normal case
        // (key is a real "group::"-prefixed key with no task sharing that
        // literal id) is unaffected — `direct` comes back empty either way.
        var direct = ResolveFlatSingleLeafId(tasks, key);
        if (direct.Count > 0) return direct;

        return GanttRowModel.TryGetGroupLabel(key, out var label)
            ? ResolveFlatGroupLeafIds(tasks, label)
            : Array.Empty<string>();
    }

    // ── ParentId hierarchy (recursive, bottom-up, memoized — mirrors GanttRollupModel.ComputeHierarchyRollups) ──

    private static IReadOnlyDictionary<string, GanttRowSelectionState> ComputeHierarchyStates(
        IReadOnlyList<GanttTask> tasks, IReadOnlySet<string> selectedIds)
    {
        var childrenByParentId = BuildChildrenMap(tasks);
        var states = new Dictionary<string, GanttRowSelectionState>(childrenByParentId.Count);
        // Cycle guard — same reasoning as GanttRollupModel.ComputeHierarchyRollups'
        // own `inProgress` set: a cyclic ParentId graph must never crash/loop
        // forever. A cyclic node falls back to Unselected (a safe, neutral
        // value — not cached, so a later non-cyclic reference via a different
        // path still gets a real computed state).
        var inProgress = new HashSet<string>();

        GanttRowSelectionState ComputeOne(string parentId)
        {
            if (states.TryGetValue(parentId, out var cached)) return cached;
            if (!inProgress.Add(parentId)) return GanttRowSelectionState.Unselected;

            var children = childrenByParentId[parentId]; // always present — only ever called for a key of this dictionary
            var allSelected = true;
            var anySelected = false;
            foreach (var child in children)
            {
                var childState = childrenByParentId.ContainsKey(child.Id)
                    ? ComputeOne(child.Id)
                    : selectedIds.Contains(child.Id) ? GanttRowSelectionState.Selected : GanttRowSelectionState.Unselected;
                if (childState != GanttRowSelectionState.Selected) allSelected = false;
                if (childState != GanttRowSelectionState.Unselected) anySelected = true;
            }

            var state = allSelected ? GanttRowSelectionState.Selected
                : anySelected ? GanttRowSelectionState.PartiallySelected
                : GanttRowSelectionState.Unselected;
            states[parentId] = state;
            inProgress.Remove(parentId);
            return state;
        }

        foreach (var parentId in childrenByParentId.Keys) ComputeOne(parentId);
        return states;
    }

    private static IReadOnlyList<string> ResolveHierarchyLeafIds(IReadOnlyList<GanttTask> tasks, string taskId)
    {
        var childrenByParentId = BuildChildrenMap(tasks);
        var taskById = new Dictionary<string, GanttTask>(tasks.Count);
        foreach (var t in tasks) taskById[t.Id] = t;

        if (!taskById.ContainsKey(taskId)) return Array.Empty<string>();
        if (!childrenByParentId.TryGetValue(taskId, out var children)) return new[] { taskId }; // a leaf clicked directly

        var result = new List<string>();
        var visited = new HashSet<string> { taskId }; // cycle guard, mirrors GanttRowModel.BuildHierarchyRows' own `visited` set

        void Walk(List<GanttTask> siblings)
        {
            foreach (var t in siblings)
            {
                if (!visited.Add(t.Id)) continue;
                if (childrenByParentId.TryGetValue(t.Id, out var grandchildren)) Walk(grandchildren);
                else result.Add(t.Id);
            }
        }

        Walk(children);
        return result;
    }

    private static Dictionary<string, List<GanttTask>> BuildChildrenMap(IReadOnlyList<GanttTask> tasks)
    {
        var childrenByParentId = new Dictionary<string, List<GanttTask>>();
        foreach (var t in tasks)
        {
            if (t.ParentId is null) continue;
            if (!childrenByParentId.TryGetValue(t.ParentId, out var siblings))
                childrenByParentId[t.ParentId] = siblings = new List<GanttTask>();
            siblings.Add(t);
        }
        return childrenByParentId;
    }

    // ── Flat GroupLabel grouping (no recursion — groups never nest) ─────────

    private static IReadOnlyDictionary<string, GanttRowSelectionState> ComputeGroupStates(
        IReadOnlyList<GanttTask> tasks, IReadOnlySet<string> selectedIds)
    {
        var membersByGroup = BuildGroupMap(tasks);
        var states = new Dictionary<string, GanttRowSelectionState>(membersByGroup.Count);
        foreach (var (label, members) in membersByGroup)
        {
            var allSelected = true;
            var anySelected = false;
            foreach (var m in members)
            {
                if (selectedIds.Contains(m.Id)) anySelected = true;
                else allSelected = false;
            }
            states[GanttRowModel.GroupToggleKey(label)] = allSelected ? GanttRowSelectionState.Selected
                : anySelected ? GanttRowSelectionState.PartiallySelected
                : GanttRowSelectionState.Unselected;
        }
        return states;
    }

    private static IReadOnlyList<string> ResolveFlatGroupLeafIds(IReadOnlyList<GanttTask> tasks, string groupLabel)
    {
        var membersByGroup = BuildGroupMap(tasks);
        if (!membersByGroup.TryGetValue(groupLabel, out var members)) return Array.Empty<string>();
        var ids = new List<string>(members.Count);
        foreach (var m in members) ids.Add(m.Id);
        return ids;
    }

    private static IReadOnlyList<string> ResolveFlatSingleLeafId(IReadOnlyList<GanttTask> tasks, string taskId)
    {
        foreach (var t in tasks)
        {
            if (t.Id == taskId) return new[] { taskId };
        }
        return Array.Empty<string>();
    }

    // Same truthiness normalization as GanttRowModel.BuildFlatGroupRows/
    // GanttRollupModel.ComputeGroupRollups (an empty-string GroupLabel is
    // "ungrouped", identical to null, and therefore never a bucket here).
    private static Dictionary<string, List<GanttTask>> BuildGroupMap(IReadOnlyList<GanttTask> tasks)
    {
        var membersByGroup = new Dictionary<string, List<GanttTask>>();
        foreach (var t in tasks)
        {
            var label = string.IsNullOrEmpty(t.GroupLabel) ? null : t.GroupLabel;
            if (label is null) continue;
            if (!membersByGroup.TryGetValue(label, out var members))
                membersByGroup[label] = members = new List<GanttTask>();
            members.Add(t);
        }
        return membersByGroup;
    }
}
