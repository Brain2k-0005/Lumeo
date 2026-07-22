namespace Lumeo.GanttV3;

/// <summary>
/// Whether a <see cref="GanttVisibleRow"/> represents a real task/milestone or
/// a synthetic flat-grouping section header.
///
/// <c>public</c> rather than <c>internal</c> (design spec asked for the row
/// model to stay internal, same as <c>GanttState</c>): unlike <c>GanttState</c>
/// — which is never exposed as a component parameter's type — <see cref="GanttVisibleRow"/>
/// (and therefore this enum, one of its members) crosses <c>GanttTree</c>/
/// <c>GanttTimeline</c>/<c>GanttArrowLayer</c>'s <c>Rows</c> parameters, and
/// those components are unavoidably <c>public</c> (the Razor compiler always
/// generates a component's class as <c>public partial class</c> — see
/// GanttBar.razor's own NOTE for the dotnet/aspnetcore#5516 /
/// dotnet/razor#8715 background). A <c>public</c> member can't expose a
/// less-accessible type (CS0053), so this — like <see cref="GanttVisibleRow"/> —
/// is <c>public</c> and recorded in PublicAPI.Unshipped.txt; flagged for the
/// reviewer/team lead to confirm, same as the earlier T2 components.
/// </summary>
public enum GanttRowKind
{
    /// <summary>A real <see cref="GanttTask"/> — either a root/leaf task (no hierarchy in play) or a hierarchy node (<see cref="GanttTask.ParentId"/> in play).</summary>
    Task,

    /// <summary>A synthetic placeholder row carrying only a group label — v2's group-header row (gantt-v2.js lines 428-449). Never has a <see cref="GanttVisibleRow.Task"/>.</summary>
    GroupHeader,
}

/// <summary>
/// One row slot in the combined tree-pane / timeline row list — the single,
/// shared source of truth for row ORDER and VISIBILITY that <c>GanttTree</c>,
/// <c>GanttTimeline</c>, and <c>GanttArrowLayer</c> all render against (design
/// spec Phase 2, T3: "row list is computed once in the root/shared code and fed
/// to both [panes]"). Its list POSITION is the row's slot index for
/// <see cref="GanttScale.BarTop"/>/<see cref="GanttScale.BarGeometry"/> purposes —
/// mirrors v2's <c>rowSlot</c> counter, which advances for group-header rows too
/// (gantt-v2.js lines 420-452).
/// </summary>
/// <param name="Kind">Task row or synthetic group-header row.</param>
/// <param name="Task">The underlying task/milestone. Null only for a <see cref="GanttRowKind.GroupHeader"/> row.</param>
/// <param name="Label">Display text — the task's <see cref="GanttTask.Name"/>, or the group's label for a header row.</param>
/// <param name="Depth">Indent depth for the tree pane. 0 for a root task or a group header; a hierarchy child is its parent's depth + 1; a task nested under a flat group header is depth 1 (see <see cref="GanttRowModel.BuildVisibleRows"/> remarks for why hierarchy and flat grouping are mutually exclusive per task list).</param>
/// <param name="HasChildren">True when this row is collapsible — a hierarchy parent (has at least one child task) or any group header (always collapsible: collapsing it hides its member rows).</param>
/// <param name="ToggleKey">The key to pass to <see cref="GanttState.ToggleCollapsed"/>/<see cref="GanttState.SetCollapsed"/> to collapse/expand this row. Null when <see cref="HasChildren"/> is false (nothing to toggle). A task-hierarchy row's key is its own <see cref="GanttTask.Id"/>; a group header's key is <see cref="GanttRowModel.GroupToggleKey"/> of its label (a distinct namespace so a task id can never collide with a group key).</param>
/// <param name="IsCollapsed">Whether this row is CURRENTLY collapsed (its descendants/members are excluded from the row list this row appears in) — resolved once here so callers never re-query <c>GanttState.Collapsed</c> themselves.</param>
/// <remarks>
/// <c>public</c> rather than <c>internal</c> — see <see cref="GanttRowKind"/>'s
/// remarks for why: this type crosses public component parameter boundaries
/// (<c>GanttTree.Rows</c>, <c>GanttTimeline.Rows</c>, <c>GanttArrowLayer.Rows</c>),
/// so it can't stay internal (CS0053). Recorded in PublicAPI.Unshipped.txt;
/// flagged for the reviewer/team lead to confirm, same precedent as GanttBar/
/// GanttNav/Gantt3/GanttTimeline in T2.
/// </remarks>
public readonly record struct GanttVisibleRow(
    GanttRowKind Kind,
    GanttTask? Task,
    string Label,
    int Depth,
    bool HasChildren,
    string? ToggleKey,
    bool IsCollapsed);

/// <summary>
/// Pure, static row-flattening logic for the GanttV3 tree pane + timeline
/// (design spec Phase 2, T3). Turns <see cref="GanttState.Tasks"/> +
/// <see cref="GanttState.Collapsed"/> into the single ordered, already-filtered
/// <see cref="GanttVisibleRow"/> list both panes render — see
/// <see cref="GanttVisibleRow"/>'s own remarks for why that sharing matters.
/// No Blazor/DOM dependency, so it is unit-testable in isolation (mirrors how
/// <see cref="GanttScale"/> and <see cref="GanttState"/> were built in earlier
/// tasks).
/// </summary>
internal static class GanttRowModel
{
    /// <summary>
    /// Prefix applied to a flat group's label to derive its <see cref="GanttState"/>
    /// collapse key — <see cref="GanttState.Collapsed"/> is a single
    /// <c>HashSet&lt;string&gt;</c> shared by both hierarchy task ids and flat group
    /// labels, so this prefix keeps the two key namespaces from ever colliding
    /// (a task whose id happens to equal some other task's group label, however
    /// unlikely, must not accidentally collapse the wrong thing).
    /// </summary>
    private const string GroupKeyPrefix = "group::";

    /// <summary>The <see cref="GanttState.Collapsed"/> key for the flat group named <paramref name="groupLabel"/>.</summary>
    internal static string GroupToggleKey(string groupLabel) => GroupKeyPrefix + groupLabel;

    /// <summary>True when at least one task in the list sets <see cref="GanttTask.ParentId"/> — the signal that hierarchy mode (not flat <see cref="GanttTask.GroupLabel"/> grouping) governs row order.</summary>
    internal static bool UsesHierarchy(IReadOnlyList<GanttTask> tasks)
    {
        for (var i = 0; i < tasks.Count; i++)
            if (tasks[i].ParentId is not null) return true;
        return false;
    }

    /// <summary>
    /// The default for <c>Gantt3.ShowTreePane</c> when the caller doesn't set it
    /// explicitly (design spec: "default: true when GroupBy set or any task has
    /// ParentId, else false"). <paramref name="groupBySet"/> is whether the
    /// caller's <c>GroupBy</c> delegate parameter is non-null — a parameter the
    /// component itself owns, so it's passed in rather than re-derived here.
    /// </summary>
    internal static bool DefaultShowTreePane(IReadOnlyList<GanttTask> tasks, bool groupBySet) =>
        groupBySet || UsesHierarchy(tasks);

    /// <summary>
    /// Flattens <paramref name="tasks"/> into the ordered, collapse-filtered row
    /// list both panes render. Hierarchy (<see cref="GanttTask.ParentId"/>) takes
    /// priority over flat <see cref="GanttTask.GroupLabel"/> grouping whenever any
    /// task in the list sets a <see cref="GanttTask.ParentId"/> — the two are
    /// mutually exclusive per task list (see <see cref="BuildFlatGroupRows"/>'s
    /// remarks for why v2's <c>GroupLabel</c> field, not the <c>GroupBy</c> sort
    /// delegate, is what actually drives header rows).
    /// </summary>
    internal static IReadOnlyList<GanttVisibleRow> BuildVisibleRows(IReadOnlyList<GanttTask> tasks, IReadOnlySet<string> collapsed)
    {
        var validTasks = FilterInvalidDurationTasks(tasks);
        return UsesHierarchy(validTasks) ? BuildHierarchyRows(validTasks, collapsed) : BuildFlatGroupRows(validTasks, collapsed);
    }

    // Bug fix (Codex round 8 review, P2 #5): v2's normalizeTasks (gantt-v2.js)
    // drops any task whose End is before its Start (`.filter(t => t.end >=
    // t.start)`) BEFORE the renderer ever sees it — no bar, no row; v3 had no
    // equivalent, so a genuinely invalid End<Start task rendered an 8px
    // sliver bar (BarGeometry's own Math.Max(8, ...) width clamp) instead of
    // being dropped. Milestones are effectively EXEMPT from v2's rule — not
    // because the rule special-cases them, but because normalizeTasks forces
    // `end = start` for every milestone BEFORE this check runs (gantt-v2.js
    // line 90: `if (isMilestone && start) end = start`), so a milestone's own
    // End/Start relationship can never trip this filter in the first place.
    // Mirrored the same way here: a milestone's End is never compared against
    // its Start at all, only a non-milestone task's is.
    //
    // Filtering here (BuildVisibleRows' single entry point, before either row-
    // building strategy runs) means the dropped task never reaches GanttTree/
    // GanttTimeline/GanttArrowLayer at all — a dependency elsewhere pointing
    // AT it therefore naturally fails GanttArrowLayer's own
    // geometryByTaskId.TryGetValue lookup and is silently skipped, the exact
    // same outcome v2's own arrow loop produces for a filtered-out task
    // (`const source = taskById.get(depId); if (!source) continue;` —
    // gantt-v2.js line 653) — no separate dependency-cleanup step needed.
    private static IReadOnlyList<GanttTask> FilterInvalidDurationTasks(IReadOnlyList<GanttTask> tasks)
    {
        List<GanttTask>? filtered = null;
        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            var isInvalid = !task.IsMilestone && task.End < task.Start;
            if (isInvalid)
            {
                if (filtered is null)
                {
                    filtered = new List<GanttTask>(tasks.Count);
                    for (var j = 0; j < i; j++) filtered.Add(tasks[j]);
                }
                continue;
            }
            filtered?.Add(task);
        }
        return filtered ?? tasks;
    }

    // ── ParentId hierarchy ───────────────────────────────────────────────────

    private static List<GanttVisibleRow> BuildHierarchyRows(IReadOnlyList<GanttTask> tasks, IReadOnlySet<string> collapsed)
    {
        // Bucket every NON-root task by its (non-null) ParentId — Dictionary<TKey,_>
        // requires TKey : notnull, so root tasks (ParentId == null) are collected
        // into their own `roots` list instead of a null-keyed bucket. Both
        // preserve the caller's original relative order — a sibling's position
        // among its siblings is whatever order the caller's Tasks list already
        // has them in (Gantt3 has already applied GroupBy sorting, if any,
        // before this runs).
        var childrenByParentId = new Dictionary<string, List<GanttTask>>();
        var roots = new List<GanttTask>();
        var taskById = new Dictionary<string, GanttTask>(tasks.Count);
        foreach (var t in tasks)
        {
            taskById[t.Id] = t; // last-wins on a duplicate id — out-of-scope invalid input, just don't throw
            if (t.ParentId is null) { roots.Add(t); continue; }
            if (!childrenByParentId.TryGetValue(t.ParentId, out var siblings))
                childrenByParentId[t.ParentId] = siblings = new List<GanttTask>();
            siblings.Add(t);
        }

        var rows = new List<GanttVisibleRow>(tasks.Count);
        var visited = new HashSet<string>();

        void Walk(IReadOnlyList<GanttTask> siblings, int depth)
        {
            foreach (var task in siblings)
            {
                // Defends against a cyclic ParentId graph (A's parent is B, B's
                // parent is A — invalid input, but must not stack-overflow):
                // a task already rendered once is never re-walked as someone
                // else's child.
                if (visited.Contains(task.Id)) continue;
                var hasChildren = childrenByParentId.TryGetValue(task.Id, out var children);
                var isCollapsed = collapsed.Contains(task.Id);
                rows.Add(new GanttVisibleRow(GanttRowKind.Task, task, task.Name, depth, hasChildren, hasChildren ? task.Id : null, isCollapsed));
                visited.Add(task.Id);
                if (hasChildren && !isCollapsed) Walk(children!, depth + 1);
            }
        }

        Walk(roots, 0);

        // Orphans: a task whose ParentId points at an id that never appears as
        // ANY task's Id in this list at all (deleted/typo'd parent, or a partial
        // page of a larger tree) is rendered at root depth rather than silently
        // dropped. Gated on "does this parentId correspond to a real task",
        // NOT on "was it visited" — a real parent that's simply nested under a
        // collapsed ancestor is correctly still unvisited, and its children
        // must stay hidden too, not be promoted to fake roots.
        var allTaskIds = new HashSet<string>(tasks.Count);
        foreach (var t in tasks) allTaskIds.Add(t.Id);

        foreach (var (parentId, orphanSiblings) in childrenByParentId)
        {
            if (allTaskIds.Contains(parentId)) continue; // real parent task — reachable (or intentionally collapsed-hidden) via the walk above
            Walk(orphanSiblings, 0);
        }

        // A task can still be unvisited at this point for TWO different reasons,
        // which must NOT be handled the same way:
        //   1. It's a legitimate descendant of a COLLAPSED ancestor (Walk added
        //      the ancestor's row but deliberately did not recurse into it) —
        //      still correctly hidden, must NOT be promoted.
        //   2. It sits on a cyclic ParentId chain (A's parent is B, B's parent
        //      is A — or a self-loop, A's parent is A) that no root/orphan walk
        //      above could ever reach — genuinely invalid input, but silently
        //      dropping those rows is worse than rendering them somewhere
        //      deterministic (reviewer-verified without any safety net:
        //      [A(parent:B), B(parent:A), D] rendered only D — A/B vanished —
        //      and [A(parent:A)] rendered 0 rows).
        // IsHiddenByCollapsedAncestor distinguishes the two by chasing the
        // ParentId chain upward: if it ever reaches a task already in `visited`,
        // that ancestor WAS rendered (case 1 — collapse suppressed recursion
        // past it) and this task must stay hidden. If instead the chain loops
        // back on itself (a real GanttTask id repeats within this single
        // upward walk) without ever touching `visited`, there is no rendered
        // ancestor to hide behind — it's case 2, a genuine cycle, and safe to
        // promote.
        bool IsHiddenByCollapsedAncestor(GanttTask start)
        {
            var seenInThisChain = new HashSet<string> { start.Id };
            var parentId = start.ParentId;
            while (parentId is not null)
            {
                if (visited.Contains(parentId)) return true;
                if (!seenInThisChain.Add(parentId)) return false; // looped back within this chain -> a cycle, not a collapsed ancestor
                if (!taskById.TryGetValue(parentId, out var parentTask)) return false; // dangling reference already handled by the orphan pass above
                parentId = parentTask.ParentId;
            }
            return false; // chain reached a real root (null ParentId) without ever being visited — the root walk above always covers this first, so this is unreachable in practice
        }

        // Cycle safety net: walk `tasks` in original order and promote the
        // first still-unvisited, not-collapse-hidden member of each remaining
        // cycle to a root (depth 0) — breaks the cycle at a stable, input-order
        // point. Walk's own `visited` guard (above) stops it from re-entering
        // the loop it just broke out of, so any other member of the SAME cycle
        // reachable as that root's "child" renders once, at depth 1+, instead
        // of being re-promoted to its own fake root.
        foreach (var t in tasks)
        {
            if (visited.Contains(t.Id)) continue;
            if (IsHiddenByCollapsedAncestor(t)) continue;
            Walk(new[] { t }, 0);
        }

        return rows;
    }

    // ── Flat GroupLabel grouping (v2 parity) ────────────────────────────────

    /// <summary>
    /// Faithful port of v2's group-header interleaving (gantt-v2.js lines
    /// 420-452: <c>tasks.forEach</c> with the <c>lastGroupLabel</c> transition
    /// check). Note this reads <see cref="GanttTask.GroupLabel"/> — the field a
    /// consumer sets directly on each task — NOT the <c>Gantt3.GroupBy</c>
    /// delegate parameter; v2 never threads <c>GroupBy</c>'s output into the JS
    /// renderer at all, only the sort order it produces (<c>Gantt.razor</c>'s
    /// <c>SortedTasks</c>, and <c>Gantt3</c>'s own copy of the same method) —
    /// the JS side only ever sees <c>task.group_label</c> (<c>ToJsTask</c>'s
    /// <c>t.GroupLabel</c>). This port keeps that exact split: by the time this
    /// method runs, <paramref name="tasks"/> is already GroupBy-sorted (Gantt3's
    /// job), and header rows are driven purely by consecutive-run detection on
    /// <see cref="GanttTask.GroupLabel"/>, same as v2.
    /// </summary>
    private static List<GanttVisibleRow> BuildFlatGroupRows(IReadOnlyList<GanttTask> tasks, IReadOnlySet<string> collapsed)
    {
        var rows = new List<GanttVisibleRow>(tasks.Count);
        string? lastGroupLabel = null;

        foreach (var task in tasks)
        {
            // Bug fix (Codex round 2, P2 #6): v2's JS gates its own group-header
            // check on plain truthiness (`task.groupLabel && ...`, gantt-v2.js:428)
            // — an EMPTY string is falsy in JS, so v2 treats it exactly like "no
            // group" (never starts a header, never indents). GanttTask.GroupLabel
            // is a C# string?, where "" and null are distinct, so a consumer that
            // sets GroupLabel = "" (rather than leaving it null) previously still
            // triggered a (blank-labeled) header row here — normalized to null
            // once, right here, so every check below (including the interleaved-
            // collapse fix's own membership lookup) treats "" and null identically.
            var groupLabel = string.IsNullOrEmpty(task.GroupLabel) ? null : task.GroupLabel;

            if (groupLabel is not null && groupLabel != lastGroupLabel)
            {
                lastGroupLabel = groupLabel;
                var key = GroupToggleKey(groupLabel);
                rows.Add(new GanttVisibleRow(GanttRowKind.GroupHeader, null, groupLabel, 0, true, key, collapsed.Contains(key)));
            }

            // Bug fix (Codex round 2, P2 #5): hiding used to be driven by a
            // running "hidingCurrentGroup" flag set only when a NEW header row
            // was rendered and reset to false by any ungrouped task in between
            // (Codex review wave's earlier fix for THAT leak) — but that flag
            // never accounted for an interleaved run like [Design(collapsed),
            // Ungrouped, Design] with no GroupBy sort clustering same-labeled
            // tasks together (a consumer can set GanttTask.GroupLabel directly
            // without ever supplying GroupBy — v2 has no collapse feature at all,
            // so this exact leak has no v2 equivalent to have caught it there):
            // the ungrouped row's reset left hidingCurrentGroup false for the
            // SECOND "Design" task too, even though its own group IS collapsed,
            // silently un-hiding it. Membership-based hiding — "is THIS task's
            // own group currently collapsed", independent of whatever header was
            // most recently rendered — has no such gap; the header-render check
            // above is now purely about "when does a NEW header row appear",
            // decoupled from "is this task visible".
            var isHidden = groupLabel is not null && collapsed.Contains(GroupToggleKey(groupLabel));
            if (isHidden) continue;

            // A task under a group header is indented one level; an ungrouped
            // task (no GroupLabel at all) sits at depth 0 like a hierarchy root.
            var depth = groupLabel is not null ? 1 : 0;
            rows.Add(new GanttVisibleRow(GanttRowKind.Task, task, task.Name, depth, false, null, false));
        }

        return rows;
    }
}
