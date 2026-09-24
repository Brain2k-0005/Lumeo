namespace Lumeo;

/// <summary>The axis a <see cref="FlowLayout"/> algorithm grows nodes along.</summary>
public enum FlowLayoutDirection
{
    /// <summary>Roots on the left, children flow rightward.</summary>
    LeftToRight,
    /// <summary>Roots on top, children flow downward.</summary>
    TopToBottom,
}

/// <summary>Options shared by every <see cref="FlowLayout"/> algorithm.</summary>
/// <param name="Direction">The axis nodes grow along.</param>
/// <param name="NodeSpacing">Gap, in flow units, between two nodes in the same rank (perpendicular to <paramref name="Direction"/>).</param>
/// <param name="RankSpacing">Gap, in flow units, between two ranks (parallel to <paramref name="Direction"/>).</param>
/// <param name="GroupPadding">Phase 5 sub-flows: the inset, in flow units, between a group's border and the children laid out inside it.</param>
/// <param name="GroupHeaderHeight">Phase 5 sub-flows: extra room above a group's children for its label row (<see cref="FlowGroupNode"/>'s), in flow units.</param>
public sealed record FlowLayoutOptions(
    FlowLayoutDirection Direction = FlowLayoutDirection.LeftToRight,
    double NodeSpacing = 48,
    double RankSpacing = 96,
    double GroupPadding = 20,
    double GroupHeaderHeight = 28)
{
    /// <summary>The default options: left-to-right, 48px node spacing, 96px rank spacing.</summary>
    public static readonly FlowLayoutOptions Default = new();

    /// <summary>The phase 3 positional deconstruction, kept so <c>var (direction, nodeSpacing, rankSpacing) = options;</c> still compiles after the phase 5 group members were appended.</summary>
    public void Deconstruct(out FlowLayoutDirection Direction, out double NodeSpacing, out double RankSpacing)
    {
        Direction = this.Direction;
        NodeSpacing = this.NodeSpacing;
        RankSpacing = this.RankSpacing;
    }
}

/// <summary>
/// Pure auto-layout algorithms for <see cref="FlowCanvas"/>. Every member takes a node/edge list and
/// returns a NEW node list with updated <see cref="FlowNode.X"/>/<see cref="FlowNode.Y"/> — nothing
/// here touches a live canvas. Apply the result with <c>Nodes = FlowLayout.Tree(...)</c> (or
/// <c>Layered</c>) followed by <c>await canvas.FitViewAsync()</c>.
/// </summary>
/// <remarks>
/// Sizes: a node's box comes from <see cref="FlowNode.Width"/>/<see cref="FlowNode.Height"/> when
/// set, else the matching entry in the <c>measured</c> dictionary each method takes (a snapshot of
/// the engine's live measurements), else <see cref="FlowGeometry.DefaultNodeWidth"/>/<see cref="FlowGeometry.DefaultNodeHeight"/>.
/// </remarks>
public static class FlowLayout
{
    /// <summary>
    /// Lays out <paramref name="nodes"/> as a rooted tree (or forest — every node with no incoming
    /// edge is its own root). A node reachable from more than one root is placed under whichever root
    /// reaches it first (input order); cycles are broken the same way <see cref="Layered"/> breaks
    /// them (DFS back-edge removal) before the tree is built, so a cyclic graph still lays out
    /// deterministically instead of looping.
    /// </summary>
    public static IReadOnlyList<FlowNode> Tree(
        IReadOnlyList<FlowNode> nodes, IReadOnlyList<FlowEdge> edges,
        FlowLayoutOptions? options = null, IReadOnlyDictionary<string, (double Width, double Height)>? measured = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);
        return LayoutPerGroup(nodes, edges, options ?? FlowLayoutOptions.Default, measured, TreeCore);
    }

    private static IReadOnlyList<FlowNode> TreeCore(
        IReadOnlyList<FlowNode> nodes, IReadOnlyList<FlowEdge> edges,
        FlowLayoutOptions opts, IReadOnlyDictionary<string, (double Width, double Height)>? measured)
    {
        if (nodes.Count == 0) return Array.Empty<FlowNode>();

        var ids = nodes.Select(n => n.Id).ToList();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < ids.Count; i++) index[ids[i]] = i;

        var acyclic = RemoveBackEdges(ids, index, edges);
        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var hasIncoming = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids) children[id] = new List<string>();
        foreach (var (s, t) in acyclic)
        {
            children[s].Add(t);
            hasIncoming.Add(t);
        }

        var sizes = SizesOf(nodes, measured);
        var roots = ids.Where(id => !hasIncoming.Contains(id)).ToList();
        if (roots.Count == 0) roots.Add(ids[0]); // a pure cycle: pick a deterministic root

        // Depth = shallowest distance from any root that reaches the node (BFS-style, first writer
        // wins) so "reachable from more than one root" resolves to whichever root reaches it first,
        // in root/child input order.
        var depthOf = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var root in roots) AssignDepth(root, 0, children, depthOf);
        foreach (var id in ids) if (!depthOf.ContainsKey(id)) depthOf[id] = 0; // unreachable node: its own root at depth 0
        var rankStart = ComputeRankStarts(ids, depthOf, sizes, opts);

        var placed = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        var cursor = 0.0; // next free perpendicular offset, shared across every root/orphan

        // Post-order: a leaf takes the next free slot; an internal node centres over the span its
        // (already-placed) children occupy, never earlier than the cursor left by its own subtree.
        double PlaceSubtree(string id)
        {
            if (placed.TryGetValue(id, out var already)) return Perp(already, opts.Direction);
            var kids = children[id].Where(c => !placed.ContainsKey(c)).ToList();
            double perp;
            if (kids.Count == 0)
            {
                perp = cursor;
                cursor += PerpSize(sizes[id], opts.Direction) + opts.NodeSpacing;
            }
            else
            {
                var kidPerps = kids.Select(PlaceSubtree).ToList();
                var min = kidPerps.Min();
                var max = kids.Zip(kidPerps, (k, p) => p + PerpSize(sizes[k], opts.Direction)).Max();
                perp = (min + max) / 2 - PerpSize(sizes[id], opts.Direction) / 2;
                perp = Math.Max(perp, min); // never push left of its leftmost/topmost child
                // Guard against a parent taller/wider than the gap its children's boxes leave (a
                // shallow, wide subtree next to it): reserve at least its own tail edge too, so no
                // later sibling/root subtree can be placed under it.
                cursor = Math.Max(cursor, perp + PerpSize(sizes[id], opts.Direction) + opts.NodeSpacing);
            }
            placed[id] = opts.Direction == FlowLayoutDirection.LeftToRight ? (rankStart[depthOf[id]], perp) : (perp, rankStart[depthOf[id]]);
            return perp;
        }

        foreach (var root in roots) PlaceSubtree(root);
        // Any node not reachable from a declared root (isolated / only reachable via a dropped back
        // edge) gets appended after everything already placed, so the result stays total.
        foreach (var id in ids) if (!placed.ContainsKey(id)) PlaceSubtree(id);

        return nodes.Select(n => n with { X = placed[n.Id].X, Y = placed[n.Id].Y }).ToList();
    }

    private static void AssignDepth(string id, int depth, Dictionary<string, List<string>> children, Dictionary<string, int> depthOf)
    {
        if (depthOf.TryGetValue(id, out var existing) && existing >= depth) return;
        depthOf[id] = depth;
        foreach (var c in children[id]) AssignDepth(c, depth + 1, children, depthOf);
    }

    private static Dictionary<int, double> ComputeRankStarts(
        List<string> ids, Dictionary<string, int> depthOf,
        Dictionary<string, (double Width, double Height)> sizes, FlowLayoutOptions opts)
    {
        var maxDepth = depthOf.Values.DefaultIfEmpty(0).Max();
        var maxAlong = new double[maxDepth + 1];
        foreach (var id in ids)
        {
            var d = depthOf[id];
            maxAlong[d] = Math.Max(maxAlong[d], Along(sizes[id], opts.Direction));
        }
        var starts = new Dictionary<int, double>();
        var running = 0.0;
        for (var d = 0; d <= maxDepth; d++)
        {
            starts[d] = running;
            running += maxAlong[d] + opts.RankSpacing;
        }
        return starts;
    }

    private static double Perp((double X, double Y) p, FlowLayoutDirection dir) => dir == FlowLayoutDirection.LeftToRight ? p.Y : p.X;
    private static double PerpSize((double Width, double Height) s, FlowLayoutDirection dir) => dir == FlowLayoutDirection.LeftToRight ? s.Height : s.Width;
    private static double Along((double Width, double Height) s, FlowLayoutDirection dir) => dir == FlowLayoutDirection.LeftToRight ? s.Width : s.Height;

    /// <summary>
    /// Layered layout: nodes are assigned to ranks by longest path from a source (DFS back-edge
    /// removal breaks cycles first, so every graph terminates), then ordered within each rank by
    /// barycenter of their neighbours in the adjacent rank (a few sweeps, alternating up/down) to
    /// reduce edge crossings. Deterministic for a fixed input order.
    /// </summary>
    public static IReadOnlyList<FlowNode> Layered(
        IReadOnlyList<FlowNode> nodes, IReadOnlyList<FlowEdge> edges,
        FlowLayoutOptions? options = null, IReadOnlyDictionary<string, (double Width, double Height)>? measured = null,
        int crossingReductionSweeps = 4)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);
        return LayoutPerGroup(nodes, edges, options ?? FlowLayoutOptions.Default, measured,
            (n, e, o, m) => LayeredCore(n, e, o, m, crossingReductionSweeps));
    }

    // ── Phase 5: per-group layout ────────────────────────────────────────

    private delegate IReadOnlyList<FlowNode> LayoutCore(
        IReadOnlyList<FlowNode> nodes, IReadOnlyList<FlowEdge> edges,
        FlowLayoutOptions opts, IReadOnlyDictionary<string, (double Width, double Height)>? measured);

    /// <summary>
    /// Sub-flows: every sibling set (the children of one group, and the top-level nodes) is laid out
    /// on its own with the same algorithm — deepest groups first — using only the edges between two
    /// members of that set (an edge from inside a group to outside it counts as an edge from the group
    /// at the level where both ends meet). Children land inside their group at
    /// (<see cref="FlowLayoutOptions.GroupPadding"/>, <see cref="FlowLayoutOptions.GroupPadding"/> +
    /// <see cref="FlowLayoutOptions.GroupHeaderHeight"/>) — their X/Y stay RELATIVE to it — and a group
    /// grows (never shrinks) its <see cref="FlowNode.Width"/>/<see cref="FlowNode.Height"/> to fit
    /// them before its own level is laid out. Without any <see cref="FlowNode.ParentId"/> this is
    /// exactly the flat algorithm.
    /// </summary>
    private static IReadOnlyList<FlowNode> LayoutPerGroup(
        IReadOnlyList<FlowNode> nodes, IReadOnlyList<FlowEdge> edges, FlowLayoutOptions opts,
        IReadOnlyDictionary<string, (double Width, double Height)>? measured, LayoutCore core)
    {
        if (nodes.Count == 0) return Array.Empty<FlowNode>();
        var hierarchy = new FlowHierarchy(nodes);
        if (!hierarchy.HasGroups) return core(nodes, edges, opts, measured);

        var current = new Dictionary<string, FlowNode>(StringComparer.Ordinal);
        foreach (var n in nodes) if (n is not null && !string.IsNullOrEmpty(n.Id)) current.TryAdd(n.Id, n);
        var sizes = new Dictionary<string, (double Width, double Height)>(StringComparer.Ordinal);
        if (measured is not null) foreach (var (k, v) in measured) sizes[k] = v;

        // Groups deepest first, so a group's final size is known when its own level is laid out.
        var groups = current.Keys.Where(hierarchy.HasChildren)
            .OrderByDescending(hierarchy.DepthOf)
            .ToList();
        foreach (var groupId in groups)
        {
            var kids = hierarchy.ChildrenOf(groupId).Select(id => current[id]).ToList();
            var kidSet = new HashSet<string>(kids.Select(k => k.Id), StringComparer.Ordinal);
            var laid = core(kids, LevelEdges(edges, kidSet, hierarchy), opts, sizes);
            var offsetX = opts.GroupPadding;
            var offsetY = opts.GroupPadding + opts.GroupHeaderHeight;
            double needW = 0, needH = 0;
            var laidSizes = SizesOf(laid, sizes);
            foreach (var k in laid)
            {
                var placed = k with { X = k.X + offsetX, Y = k.Y + offsetY };
                current[k.Id] = placed;
                var (w, h) = laidSizes[k.Id];
                needW = Math.Max(needW, placed.X + w + opts.GroupPadding);
                needH = Math.Max(needH, placed.Y + h + opts.GroupPadding);
            }
            var group = current[groupId];
            var groupSize = SizesOf(new[] { group }, sizes)[groupId];
            var grown = group with { Width = Math.Max(groupSize.Width, needW), Height = Math.Max(groupSize.Height, needH) };
            current[groupId] = grown;
            sizes[groupId] = (grown.Width!.Value, grown.Height!.Value);
        }

        var roots = current.Values.Where(n => hierarchy.ParentOf(n.Id) is null).ToList();
        var rootSet = new HashSet<string>(roots.Select(r => r.Id), StringComparer.Ordinal);
        foreach (var r in core(roots, LevelEdges(edges, rootSet, hierarchy), opts, sizes)) current[r.Id] = r;

        return nodes.Select(n => current.TryGetValue(n.Id, out var laidOut) ? laidOut : n).ToList();
    }

    // The edges between two DIFFERENT members of one sibling set, each end mapped to the member that
    // contains it (itself, or its ancestor in the set) — deduplicated, in input order.
    private static List<FlowEdge> LevelEdges(IReadOnlyList<FlowEdge> edges, HashSet<string> members, FlowHierarchy hierarchy)
    {
        string? MemberOf(string id)
        {
            var cursor = hierarchy.Contains(id) ? id : null;
            while (cursor is not null && !members.Contains(cursor)) cursor = hierarchy.ParentOf(cursor);
            return cursor;
        }

        var result = new List<FlowEdge>();
        var seen = new HashSet<(string, string)>();
        foreach (var e in edges)
        {
            if (e is null) continue;
            var s = MemberOf(e.Source);
            var t = MemberOf(e.Target);
            if (s is null || t is null || s == t || !seen.Add((s, t))) continue;
            result.Add(s == e.Source && t == e.Target ? e : new FlowEdge(e.Id, s, t));
        }
        return result;
    }

    private static IReadOnlyList<FlowNode> LayeredCore(
        IReadOnlyList<FlowNode> nodes, IReadOnlyList<FlowEdge> edges,
        FlowLayoutOptions opts, IReadOnlyDictionary<string, (double Width, double Height)>? measured,
        int crossingReductionSweeps)
    {
        if (nodes.Count == 0) return Array.Empty<FlowNode>();

        var ids = nodes.Select(n => n.Id).ToList();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < ids.Count; i++) index[ids[i]] = i;
        var acyclic = RemoveBackEdges(ids, index, edges);

        var outAdj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var inAdj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var id in ids) { outAdj[id] = new List<string>(); inAdj[id] = new List<string>(); }
        foreach (var (s, t) in acyclic) { outAdj[s].Add(t); inAdj[t].Add(s); }

        // Rank = longest path from any source (in-degree 0 in the acyclic graph). Processed in a
        // topological order (Kahn) so every predecessor's rank is final before a node is ranked.
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        var indeg = ids.ToDictionary(id => id, id => inAdj[id].Count, StringComparer.Ordinal);
        var queue = new Queue<string>(ids.Where(id => indeg[id] == 0));
        foreach (var id in ids.Where(id => indeg[id] == 0)) rank[id] = 0;
        var processed = new HashSet<string>(StringComparer.Ordinal);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!processed.Add(id)) continue;
            var r = rank.GetValueOrDefault(id, 0);
            foreach (var t in outAdj[id])
            {
                rank[t] = Math.Max(rank.GetValueOrDefault(t, 0), r + 1);
                if (--indeg[t] == 0) queue.Enqueue(t);
            }
        }
        foreach (var id in ids) if (!rank.ContainsKey(id)) rank[id] = 0; // isolated / unreached: rank 0

        var maxRank = rank.Values.DefaultIfEmpty(0).Max();
        var layers = new List<List<string>>();
        for (var r = 0; r <= maxRank; r++) layers.Add(new List<string>());
        foreach (var id in ids) layers[rank[id]].Add(id); // stable: input order within a rank initially

        // Barycenter ordering: a few alternating sweeps, each re-sorting a layer by the mean position
        // of its neighbours in the adjacent (already re-ordered) layer.
        var posInLayer = new Dictionary<string, int>(StringComparer.Ordinal);
        void ReindexAll() { foreach (var layer in layers) for (var i = 0; i < layer.Count; i++) posInLayer[layer[i]] = i; }
        ReindexAll();

        for (var sweep = 0; sweep < crossingReductionSweeps; sweep++)
        {
            var downward = sweep % 2 == 0;
            if (downward)
            {
                for (var r = 1; r <= maxRank; r++) BarycenterSort(layers[r], id => inAdj[id], posInLayer);
            }
            else
            {
                for (var r = maxRank - 1; r >= 0; r--) BarycenterSort(layers[r], id => outAdj[id], posInLayer);
            }
            ReindexAll();
        }

        var sizes = SizesOf(nodes, measured);
        var rankAlong = new double[maxRank + 1];
        var running = 0.0;
        for (var r = 0; r <= maxRank; r++)
        {
            rankAlong[r] = running;
            var maxSize = layers[r].Count == 0 ? 0 : layers[r].Max(id => Along(sizes[id], opts.Direction));
            running += maxSize + opts.RankSpacing;
        }

        var placed = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        for (var r = 0; r <= maxRank; r++)
        {
            var perp = 0.0;
            foreach (var id in layers[r])
            {
                var along = rankAlong[r];
                placed[id] = opts.Direction == FlowLayoutDirection.LeftToRight ? (along, perp) : (perp, along);
                perp += PerpSize(sizes[id], opts.Direction) + opts.NodeSpacing;
            }
        }

        return nodes.Select(n => n with { X = placed[n.Id].X, Y = placed[n.Id].Y }).ToList();
    }

    private static void BarycenterSort(List<string> layer, Func<string, List<string>> neighborsOf, Dictionary<string, int> posInLayer)
    {
        var withKey = layer.Select((id, originalIndex) =>
        {
            var neighbors = neighborsOf(id);
            double key = neighbors.Count == 0
                ? posInLayer.GetValueOrDefault(id, originalIndex) // no neighbours: keep its current slot (stable)
                : neighbors.Average(n => posInLayer.GetValueOrDefault(n, 0));
            return (Id: id, Key: key, OriginalIndex: originalIndex);
        }).ToList();
        // Stable sort by barycenter; ties keep the original relative order (deterministic).
        var ordered = withKey.OrderBy(x => x.Key).ThenBy(x => x.OriginalIndex).Select(x => x.Id).ToList();
        layer.Clear();
        layer.AddRange(ordered);
    }

    /// <summary>
    /// A deterministic feedback-arc set: one DFS per unvisited node (in input order); an edge to a
    /// node currently on the DFS stack is a back edge and is dropped. Returns the remaining edges as
    /// (source, target) id pairs. Self-loops are always dropped.
    /// </summary>
    private static List<(string Source, string Target)> RemoveBackEdges(
        List<string> ids, Dictionary<string, int> index, IReadOnlyList<FlowEdge> edges)
    {
        var byId = new HashSet<string>(ids, StringComparer.Ordinal);
        var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var id in ids) adj[id] = new List<string>();
        foreach (var e in edges)
        {
            if (e is null || e.Source == e.Target) continue;
            if (!byId.Contains(e.Source) || !byId.Contains(e.Target)) continue;
            adj[e.Source].Add(e.Target);
        }

        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 0=unvisited,1=on-stack,2=done
        var kept = new List<(string, string)>();
        foreach (var id in ids) state[id] = 0;

        void Dfs(string id)
        {
            state[id] = 1;
            foreach (var next in adj[id])
            {
                var s = state[next];
                if (s == 1) continue; // back edge: drop
                kept.Add((id, next));
                if (s == 0) Dfs(next);
            }
            state[id] = 2;
        }

        foreach (var id in ids) if (state[id] == 0) Dfs(id);
        return kept;
    }

    private static Dictionary<string, (double Width, double Height)> SizesOf(
        IReadOnlyList<FlowNode> nodes, IReadOnlyDictionary<string, (double Width, double Height)>? measured)
    {
        var sizes = new Dictionary<string, (double Width, double Height)>(StringComparer.Ordinal);
        foreach (var n in nodes)
        {
            double w = n.Width ?? (measured is not null && measured.TryGetValue(n.Id, out var m) ? m.Width : FlowGeometry.DefaultNodeWidth);
            double h = n.Height ?? (measured is not null && measured.TryGetValue(n.Id, out var m2) ? m2.Height : FlowGeometry.DefaultNodeHeight);
            sizes[n.Id] = (w > 0 ? w : FlowGeometry.DefaultNodeWidth, h > 0 ? h : FlowGeometry.DefaultNodeHeight);
        }
        return sizes;
    }
}
