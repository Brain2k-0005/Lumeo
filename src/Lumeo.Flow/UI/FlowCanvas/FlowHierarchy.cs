namespace Lumeo;

/// <summary>
/// Phase 5 sub-flows: the parent/child structure of one node list — resolved parents (a missing
/// parent id or a parent cycle is treated as top-level), nesting depth, direct children, ABSOLUTE
/// flow positions (a child's <see cref="FlowNode.X"/>/<see cref="FlowNode.Y"/> are relative to its
/// parent) and the render order (parents before their children, otherwise the list order). Built
/// once per node list (<see cref="FlowState.SetNodes"/>), immutable afterwards. Pure — no canvas, no
/// measurement: sizes come from the caller where needed.
/// </summary>
internal sealed class FlowHierarchy
{
    private readonly Dictionary<string, FlowNode> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _parentOf = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _children = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _depth = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FlowPoint> _absolute = new(StringComparer.Ordinal);

    public static readonly FlowHierarchy Empty = new(Array.Empty<FlowNode>());

    /// <summary>True when at least one node has a (valid) parent — lets hot paths skip all group work otherwise.</summary>
    public bool HasGroups => _parentOf.Count > 0;

    /// <summary>Every node, parents before their children (stable: list order within the same depth).</summary>
    public IReadOnlyList<FlowNode> RenderOrder { get; }

    /// <summary>The deepest nesting level (0 = no groups).</summary>
    public int MaxDepth { get; }

    public FlowHierarchy(IReadOnlyList<FlowNode> nodes)
    {
        foreach (var n in nodes)
        {
            if (n is null || string.IsNullOrEmpty(n.Id)) continue;
            _byId.TryAdd(n.Id, n); // a duplicate id keeps its first occurrence (edges resolve the same way)
        }

        // A parent is valid when it exists, is not the node itself, and its own chain never leads back
        // to the node — every member of a cycle therefore becomes top-level, deterministically.
        foreach (var n in _byId.Values)
        {
            var pid = n.ParentId;
            if (string.IsNullOrEmpty(pid) || pid == n.Id || !_byId.ContainsKey(pid)) continue;
            var cursor = pid;
            var steps = 0;
            var cyclic = false;
            while (cursor is not null && steps++ <= _byId.Count)
            {
                if (cursor == n.Id) { cyclic = true; break; }
                var next = _byId.TryGetValue(cursor, out var c) ? c.ParentId : null;
                cursor = string.IsNullOrEmpty(next) || !_byId.ContainsKey(next) || next == cursor ? null : next;
            }
            // Only a chain that returns to THIS node makes it a cycle member. A chain that loops further
            // up (n -> A -> B -> A) leaves n a valid child of A — A and B themselves become top-level.
            if (cyclic) continue;
            _parentOf[n.Id] = pid;
            if (!_children.TryGetValue(pid, out var list)) _children[pid] = list = new List<string>();
            list.Add(n.Id);
        }

        var maxDepth = 0;
        foreach (var n in _byId.Values)
        {
            var d = DepthCore(n.Id);
            if (d > maxDepth) maxDepth = d;
        }
        MaxDepth = maxDepth;

        if (_parentOf.Count == 0)
        {
            RenderOrder = nodes;
        }
        else
        {
            var ordered = new List<FlowNode>(nodes.Count);
            for (var d = 0; d <= maxDepth; d++)
            {
                foreach (var n in nodes)
                {
                    if (n is null || string.IsNullOrEmpty(n.Id) || !ReferenceEquals(_byId.GetValueOrDefault(n.Id), n)) continue;
                    if (_depth[n.Id] == d) ordered.Add(n);
                }
            }
            RenderOrder = ordered;
        }
    }

    private int DepthCore(string id)
    {
        if (_depth.TryGetValue(id, out var cached)) return cached;
        // Iterative: walk up to the first node with a known depth (or a root), then fill in downwards.
        var chain = new List<string>();
        var cursor = id;
        var baseDepth = -1;
        var baseAbs = new FlowPoint(0, 0);
        while (true)
        {
            if (_depth.TryGetValue(cursor, out var known))
            {
                baseDepth = known;
                baseAbs = _absolute[cursor];
                break;
            }
            chain.Add(cursor);
            if (!_parentOf.TryGetValue(cursor, out var parent)) break;
            cursor = parent;
        }
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var nodeId = chain[i];
            var node = _byId[nodeId];
            baseDepth++;
            baseAbs = new FlowPoint(baseAbs.X + node.X, baseAbs.Y + node.Y);
            _depth[nodeId] = baseDepth;
            _absolute[nodeId] = baseAbs;
        }
        return _depth[id];
    }

    public bool Contains(string id) => _byId.ContainsKey(id);

    public FlowNode? Get(string id) => _byId.GetValueOrDefault(id);

    /// <summary>The resolved parent id (null for a top-level node, a missing parent, or a cycle member).</summary>
    public string? ParentOf(string id) => _parentOf.GetValueOrDefault(id);

    public int DepthOf(string id) => _depth.GetValueOrDefault(id);

    /// <summary>The node's top-left corner in absolute flow coordinates (its own X/Y for a top-level node).</summary>
    public FlowPoint AbsoluteOf(FlowNode node)
        => ReferenceEquals(_byId.GetValueOrDefault(node.Id), node) && _absolute.TryGetValue(node.Id, out var p)
            ? p
            : AbsoluteOfDetached(node);

    // A node instance that is not the one this hierarchy was built from (a stale record, or one that
    // was never in the list): resolve through its parent chain as it stands now.
    private FlowPoint AbsoluteOfDetached(FlowNode node)
    {
        if (!string.IsNullOrEmpty(node.ParentId) && node.ParentId != node.Id && _absolute.TryGetValue(node.ParentId, out var parentAbs)
            && _byId.ContainsKey(node.ParentId))
        {
            return new FlowPoint(parentAbs.X + node.X, parentAbs.Y + node.Y);
        }
        return new FlowPoint(node.X, node.Y);
    }

    public IReadOnlyList<string> ChildrenOf(string id)
        => _children.TryGetValue(id, out var list) ? list : Array.Empty<string>();

    public bool HasChildren(string id) => _children.ContainsKey(id);

    /// <summary>Every descendant id of <paramref name="id"/> (children, grandchildren, ...), parents before children.</summary>
    public List<string> DescendantsOf(string id)
    {
        var result = new List<string>();
        if (!_children.ContainsKey(id)) return result;
        var queue = new Queue<string>();
        queue.Enqueue(id);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!_children.TryGetValue(current, out var kids)) continue;
            foreach (var k in kids)
            {
                result.Add(k);
                queue.Enqueue(k);
            }
        }
        return result;
    }

    /// <summary>True when <paramref name="ancestorId"/> is a strict ancestor of <paramref name="id"/>.</summary>
    public bool IsAncestor(string ancestorId, string id)
    {
        var cursor = ParentOf(id);
        while (cursor is not null)
        {
            if (cursor == ancestorId) return true;
            cursor = ParentOf(cursor);
        }
        return false;
    }

    /// <summary>Walks up from <paramref name="id"/> (exclusive) and returns every ancestor id, nearest first.</summary>
    public List<string> AncestorsOf(string id)
    {
        var result = new List<string>();
        var cursor = ParentOf(id);
        while (cursor is not null)
        {
            result.Add(cursor);
            cursor = ParentOf(cursor);
        }
        return result;
    }
}
