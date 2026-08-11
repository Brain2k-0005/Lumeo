namespace Lumeo.GanttV3;

/// <summary>
/// Pure, static rollup computation for the GanttV3 summary-envelope feature
/// (design spec Phase 3, T3 — "Duration-weighted progress rollup per parent/
/// group row" + "Overridable rollup math"). Mirrors <see cref="GanttRowModel"/>'s
/// own shape: no Blazor/DOM dependency, unit-testable in isolation, computed
/// fresh from the same task set the row model builds <see cref="GanttVisibleRow"/>s
/// from.
///
/// ── Which set of children a row's <see cref="GanttRollup"/> is computed over ──
///
/// <see cref="GanttRowModel"/>'s own pinned decision (T3 controller note
/// "Hierarchie schlaegt GroupBy" — hierarchy and flat <c>GroupLabel</c>
/// grouping are mutually exclusive per task list, never combined) carries
/// over here unchanged: <see cref="ComputeRollups"/> branches on the EXACT
/// same <see cref="GanttRowModel.UsesHierarchy"/> test <c>BuildVisibleRows</c>
/// uses, so a rollup is never computed for a row kind that couldn't exist in
/// the first place for this task set.
///
/// ── Recursion (a parent whose children are themselves parents) ──
///
/// A hierarchy parent's rollup is computed over its DIRECT children only —
/// but when a direct child is ITSELF a hierarchy parent (has children of its
/// own), the value fed into the parent's own math is that child's ALREADY-
/// COMPUTED <see cref="GanttRollup"/> (its Start/End/WeightedProgress),
/// substituted onto a copy of the child's own <see cref="GanttTask"/> — see
/// <c>EffectiveChild</c>'s remarks (a local function inside <c>ComputeHierarchyRollups</c>).
/// Computed bottom-up (post-order,
/// memoized per parent id), so a grandparent's rollup transitively reflects
/// its whole subtree without ever materializing a flattened leaf list (which
/// would risk O(rows²) for a deep/linear chain of parents — see this
/// method's own remarks on complexity).
///
/// ── Duration weighting + the zero-duration-milestone guard ──
///
/// Weight for a child = calendar-day duration (<c>(End.Date - Start.Date).TotalDays</c>),
/// FLOORED to <see cref="MinimumWeightDays"/> (1.0) — never literally 0. A
/// milestone child (Start == End) still contributes its Start/End to the
/// envelope's min/max exactly like any other child (unaffected by
/// weighting), and the floor means it always contributes SOME weight to the
/// progress average (as if it were a 1-day task) rather than either being
/// silently excluded (0 weight -> a plain-sum average would just skip it)
/// or driving a division-by-zero when every single child of a row happens to
/// be a milestone (sum of weights would otherwise be exactly 0).
///
/// ── Complexity ──
///
/// O(n) total per <see cref="ComputeRollups"/> call, where n is the filtered
/// task count: <c>ComputeHierarchyRollups</c> (private) builds the child-id
/// lookup in one O(n) pass, then its local <c>ComputeOne</c> function is
/// memoized per parent id (each parent's own body runs exactly once across
/// the whole call, regardless of how many times it's reached as someone
/// else's child), so the total work across every <c>ComputeOne</c> call is
/// bounded by the number of (parent, child) EDGES in the task forest — at
/// most n-1 for n tasks — not by the number of PARENT ROWS times their
/// (possibly re-walked) subtree sizes. <see cref="ComputeGroupRollups"/> is
/// a single O(n) pass bucketing tasks by <see cref="GanttTask.GroupLabel"/>.
/// Neither path ever flattens a subtree into a list.
/// </summary>
internal static class GanttRollupModel
{
    /// <summary>
    /// The minimum weight (in day-equivalents) any single child contributes to
    /// a <see cref="GanttRollup.WeightedProgress"/> average — see this class's
    /// own remarks on the zero-duration-milestone guard.
    /// </summary>
    internal const double MinimumWeightDays = 1.0;

    /// <summary>
    /// Computes every eligible parent/group row's <see cref="GanttRollup"/> for
    /// <paramref name="tasks"/> (already <see cref="GanttRowModel.FilterValidDurationTasks"/>-filtered,
    /// the same set <see cref="GanttRowModel.BuildVisibleRows"/> renders against),
    /// keyed by the SAME string a <see cref="GanttVisibleRow.ToggleKey"/> carries
    /// for that row — a hierarchy parent's own <see cref="GanttTask.Id"/>, or a
    /// flat group's <see cref="GanttRowModel.GroupToggleKey"/>. A caller can
    /// therefore look up any row's rollup (when one exists) via
    /// <c>row.HasChildren ? rollups.TryGetValue(row.ToggleKey!, ...) : false</c>
    /// with no separate id-derivation logic of its own.
    /// </summary>
    internal static IReadOnlyDictionary<string, GanttRollup> ComputeRollups(
        IReadOnlyList<GanttTask> tasks, Func<IReadOnlyList<GanttTask>, GanttRollup>? rollupMath) =>
        GanttRowModel.UsesHierarchy(tasks) ? ComputeHierarchyRollups(tasks, rollupMath) : ComputeGroupRollups(tasks, rollupMath);

    // ── ParentId hierarchy (recursive, bottom-up, memoized) ─────────────────

    private static IReadOnlyDictionary<string, GanttRollup> ComputeHierarchyRollups(
        IReadOnlyList<GanttTask> tasks, Func<IReadOnlyList<GanttTask>, GanttRollup>? rollupMath)
    {
        var childrenByParentId = new Dictionary<string, List<GanttTask>>();
        var taskById = new Dictionary<string, GanttTask>(tasks.Count);
        foreach (var t in tasks)
        {
            taskById[t.Id] = t; // last-wins on a duplicate id — mirrors GanttRowModel's own out-of-scope-input tolerance
            if (t.ParentId is null) continue;
            if (!childrenByParentId.TryGetValue(t.ParentId, out var siblings))
                childrenByParentId[t.ParentId] = siblings = new List<GanttTask>();
            siblings.Add(t);
        }

        var rollups = new Dictionary<string, GanttRollup>(childrenByParentId.Count);
        // Cycle guard (defensive — a cyclic ParentId graph is invalid input,
        // same class GanttRowModel.BuildHierarchyRows already defends rendering
        // against, but rollup computation walks a DIFFERENT direction — down
        // into children, not up through parents — so it needs its own guard):
        // a parent id currently being computed that's reached AGAIN before its
        // own computation finished means a cycle, not a diamond (memoization
        // via `rollups` already handles a genuine diamond/shared-descendant
        // shape without re-entering this branch at all).
        var inProgress = new HashSet<string>();

        GanttRollup ComputeOne(string parentId)
        {
            if (rollups.TryGetValue(parentId, out var cached)) return cached;
            if (!inProgress.Add(parentId))
            {
                // Cyclic ParentId chain — never crash/loop forever. Falls back to
                // the task's own raw Start/End/Progress as a sane, bounded value
                // (not cached into `rollups`, so a later, non-cyclic reference to
                // the SAME id — reached via a different path — still gets a real
                // computed rollup if one is possible).
                var self = taskById[parentId];
                return new GanttRollup(self.Start.Date, self.End.Date, self.Progress);
            }

            var children = childrenByParentId[parentId]; // always present — only ever called for a key of this dictionary
            var effectiveChildren = new List<GanttTask>(children.Count);
            foreach (var c in children) effectiveChildren.Add(EffectiveChild(c));

            var rollup = SafeInvokeRollupMath(rollupMath, effectiveChildren);
            rollups[parentId] = rollup;
            inProgress.Remove(parentId);
            return rollup;

            // The task fed into a PARENT's own rollup math for one of its direct
            // children: the child's raw GanttTask when it's a leaf, or — when the
            // child is itself a hierarchy parent — a copy of it with Start/End/
            // Progress REPLACED by that child's own (already bottom-up-computed)
            // rollup. This substitution is the entire recursion mechanism: it
            // lets RollupMath's fixed `IReadOnlyList<GanttTask> -> GanttRollup`
            // signature compose transitively across any depth of nesting without
            // the delegate itself ever needing to know it's looking at a
            // "synthetic" representative rather than a real leaf task — from its
            // perspective every entry is just a GanttTask. Only Start/End/Progress
            // are overwritten; Id/Name/IsMilestone/Dependencies/CustomClass/
            // GroupLabel/ParentId all stay the child's own real values.
            GanttTask EffectiveChild(GanttTask child)
            {
                if (!childrenByParentId.ContainsKey(child.Id)) return child;
                var childRollup = ComputeOne(child.Id);
                var roundedProgress = (int)Math.Round(childRollup.WeightedProgress, MidpointRounding.AwayFromZero);
                return child with { Start = childRollup.Start, End = childRollup.End, Progress = roundedProgress };
            }
        }

        foreach (var parentId in childrenByParentId.Keys) ComputeOne(parentId);
        return rollups;
    }

    // ── Flat GroupLabel grouping (no recursion — groups never nest) ─────────

    private static IReadOnlyDictionary<string, GanttRollup> ComputeGroupRollups(
        IReadOnlyList<GanttTask> tasks, Func<IReadOnlyList<GanttTask>, GanttRollup>? rollupMath)
    {
        // Same truthiness normalization as GanttRowModel.BuildFlatGroupRows
        // (an empty-string GroupLabel is "ungrouped", identical to null) — kept
        // in lockstep so a rollup's dictionary key always matches the SAME
        // group header row BuildFlatGroupRows would (or wouldn't) render.
        var membersByGroup = new Dictionary<string, List<GanttTask>>();
        foreach (var t in tasks)
        {
            var label = string.IsNullOrEmpty(t.GroupLabel) ? null : t.GroupLabel;
            if (label is null) continue;
            if (!membersByGroup.TryGetValue(label, out var members))
                membersByGroup[label] = members = new List<GanttTask>();
            members.Add(t);
        }

        var rollups = new Dictionary<string, GanttRollup>(membersByGroup.Count);
        foreach (var (label, members) in membersByGroup)
            rollups[GanttRowModel.GroupToggleKey(label)] = SafeInvokeRollupMath(rollupMath, members);
        return rollups;
    }

    // ── Default math + the defensive contract around a consumer override ────

    /// <summary>
    /// The built-in duration-weighted rollup math (design spec Phase 3, T3 —
    /// "Default math duration-weighted"). See this class's own remarks for the
    /// weighting formula and the zero-duration-milestone guard.
    /// </summary>
    internal static GanttRollup DefaultRollupMath(IReadOnlyList<GanttTask> children)
    {
        if (children.Count == 0) return default;

        var start = DateTime.MaxValue;
        var end = DateTime.MinValue;
        double sumWeight = 0, sumWeightedProgress = 0;
        foreach (var c in children)
        {
            var cs = c.Start.Date;
            var ce = c.End.Date;
            if (cs < start) start = cs;
            if (ce > end) end = ce;
            var weight = Math.Max((ce - cs).TotalDays, MinimumWeightDays);
            sumWeight += weight;
            sumWeightedProgress += weight * c.Progress;
        }
        // sumWeight is always > 0 here (children.Count >= 1, every weight is
        // floored to at least MinimumWeightDays) — the guard is defensive only.
        var progress = sumWeight > 0 ? sumWeightedProgress / sumWeight : 0;
        return new GanttRollup(start, end, progress);
    }

    /// <summary>
    /// Invokes <paramref name="rollupMath"/> (or <see cref="DefaultRollupMath"/>
    /// when null) and sanitizes whatever comes back — the defensive contract a
    /// consumer-supplied <c>RollupMath</c> must satisfy WITHOUT being trusted to
    /// (design spec Phase 3, T3: "A consumer-supplied RollupMath can throw or
    /// return nonsense ... enforce it defensively"):
    ///   1. A THROWING override never takes the chart down — caught here, falls
    ///      back to <see cref="DefaultRollupMath"/> over the SAME children for
    ///      that one row only (every other row's rollup is computed completely
    ///      independently, so one broken override never affects the rest of the
    ///      chart).
    ///   2. A returned <see cref="GanttRollup.End"/> before <see cref="GanttRollup.Start"/>
    ///      is clamped to <c>End = Start</c> (a zero-width envelope, never a
    ///      negative-width one — <see cref="Sanitize"/>).
    ///   3. A returned <see cref="GanttRollup.WeightedProgress"/> outside
    ///      0..100 (including NaN/Infinity, e.g. from a 0/0 a naive custom
    ///      average might produce) is clamped into range.
    /// Applied uniformly to BOTH the default math's own output and a custom
    /// override's — <see cref="GanttTask.Progress"/> itself is a plain,
    /// consumer-supplied <c>int</c> with no enforced 0-100 range (see
    /// <c>GanttBar.ClampedProgress</c>'s own precedent for the identical
    /// concern), so even the default math's result isn't provably in range
    /// without this.
    /// </summary>
    private static GanttRollup SafeInvokeRollupMath(Func<IReadOnlyList<GanttTask>, GanttRollup>? rollupMath, IReadOnlyList<GanttTask> children)
    {
        if (rollupMath is null) return Sanitize(DefaultRollupMath(children));

        GanttRollup raw;
        try
        {
            raw = rollupMath(children);
        }
        catch
        {
            return Sanitize(DefaultRollupMath(children));
        }
        return Sanitize(raw);
    }

    private static GanttRollup Sanitize(GanttRollup raw)
    {
        var end = raw.End < raw.Start ? raw.Start : raw.End;
        var progress = double.IsNaN(raw.WeightedProgress) || double.IsInfinity(raw.WeightedProgress)
            ? 0.0
            : Math.Clamp(raw.WeightedProgress, 0.0, 100.0);
        return new GanttRollup(raw.Start, end, progress);
    }
}
