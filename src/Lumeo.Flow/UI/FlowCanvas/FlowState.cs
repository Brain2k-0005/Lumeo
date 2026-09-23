namespace Lumeo;

/// <summary>
/// A FlowCanvas' non-render state: the latest known viewport, the viewport .NET last asked the
/// engine to apply (the stamp), measured node sizes and handle offsets, the pane size, the current
/// selection and the node-list generation. No render-loop coupling — the canvas decides when to
/// render; this class only answers questions.
/// </summary>
internal sealed class FlowState
{
    private readonly Dictionary<string, Measured> _measured = new(StringComparer.Ordinal);
    private readonly Queue<FlowViewport> _recentReports = new();

    private sealed record Measured(double Width, double Height, IReadOnlyList<HandleInfo> Handles);

    /// <summary>A measured handle: centre offset from the node's top-left, in flow units.</summary>
    internal sealed record HandleInfo(string? Id, FlowHandleType Type, FlowPosition Position, double X, double Y);

    /// <summary>The latest viewport known to .NET — engine reports or .NET-originated sets.</summary>
    public FlowViewport Viewport { get; private set; } = new(0, 0, 1);

    /// <summary>The viewport of the latest .NET-originated set (what the stamp carries).</summary>
    public FlowViewport StampedViewport { get; private set; } = new(0, 0, 1);

    /// <summary>Monotonic id of the latest .NET-originated viewport set; 0 = the initial viewport.</summary>
    public long StampId { get; private set; }

    /// <summary>
    /// Bumped whenever the node list is replaced from OUTSIDE (not an echo of the canvas' own
    /// commit). A drag that started under an older generation is stale and never committed.
    /// </summary>
    public int Generation { get; private set; }

    public double PaneWidth { get; private set; }
    public double PaneHeight { get; private set; }

    public HashSet<string> SelectedNodeIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> DraggingNodeIds { get; } = new(StringComparer.Ordinal);

    public void Initialize(FlowViewport viewport)
    {
        Viewport = viewport;
        StampedViewport = viewport;
    }

    /// <summary>A .NET-originated viewport change: recorded and given a fresh stamp id.</summary>
    public void Stamp(FlowViewport viewport)
    {
        Viewport = viewport;
        StampedViewport = viewport;
        StampId++;
    }

    /// <summary>
    /// The engine reported <paramref name="viewport"/>. Remembered for echo detection: a bound
    /// <c>Viewport</c> parameter that comes back with a value the engine itself reported is not a
    /// new instruction.
    /// </summary>
    public void Report(FlowViewport viewport)
    {
        Viewport = viewport;
        _recentReports.Enqueue(viewport);
        while (_recentReports.Count > 16) _recentReports.Dequeue();
    }

    public bool IsEcho(FlowViewport viewport) => viewport == Viewport || _recentReports.Contains(viewport);

    public void BumpGeneration() => Generation++;

    public void SetPaneSize(double width, double height)
    {
        PaneWidth = width > 0 ? width : 0;
        PaneHeight = height > 0 ? height : 0;
    }

    /// <summary>Applies an engine measurement batch; returns true when anything changed.</summary>
    public bool ApplyMeasurements(IEnumerable<FlowNodeMeasurement> measurements)
    {
        var changed = false;
        foreach (var m in measurements)
        {
            if (m is null || string.IsNullOrEmpty(m.Id)) continue;
            var handles = new List<HandleInfo>();
            if (m.Handles is not null)
            {
                foreach (var h in m.Handles)
                {
                    if (h is null) continue;
                    var type = string.Equals(h.Type, "target", StringComparison.OrdinalIgnoreCase) ? FlowHandleType.Target : FlowHandleType.Source;
                    var pos = FlowGeometry.ParsePosition(h.Position?.ToLowerInvariant())
                              ?? (type == FlowHandleType.Source ? FlowPosition.Right : FlowPosition.Left);
                    handles.Add(new HandleInfo(string.IsNullOrEmpty(h.Id) ? null : h.Id, type, pos, h.X, h.Y));
                }
            }
            var next = new Measured(m.Width, m.Height, handles);
            if (_measured.TryGetValue(m.Id, out var prev)
                && prev.Width == next.Width && prev.Height == next.Height
                && prev.Handles.SequenceEqual(next.Handles))
            {
                continue;
            }
            _measured[m.Id] = next;
            changed = true;
        }
        return changed;
    }

    public void ForgetMissing(IEnumerable<FlowNode> nodes)
    {
        if (_measured.Count == 0) return;
        var live = new HashSet<string>(nodes.Select(n => n.Id), StringComparer.Ordinal);
        foreach (var id in _measured.Keys.Where(id => !live.Contains(id)).ToList()) _measured.Remove(id);
    }

    public bool IsMeasured(string id) => _measured.ContainsKey(id);

    /// <summary>The node's rect: fixed size &gt; measured size &gt; <see cref="FlowGeometry.DefaultNodeWidth"/>.</summary>
    public FlowRect GetNodeRect(FlowNode node)
    {
        _measured.TryGetValue(node.Id, out var m);
        var w = node.Width ?? m?.Width ?? FlowGeometry.DefaultNodeWidth;
        var h = node.Height ?? m?.Height ?? FlowGeometry.DefaultNodeHeight;
        return new FlowRect(node.X, node.Y, w, h);
    }

    /// <summary>
    /// Where an edge attaches: the handle with <paramref name="handleId"/> (or the node's first
    /// handle of <paramref name="type"/> when the id is null), else the default side — right for a
    /// source, left for a target. Mirrors <c>anchorFor</c> in flow.js.
    /// </summary>
    public (FlowPoint Point, FlowPosition Position) GetAnchor(FlowNode node, string? handleId, FlowHandleType type)
    {
        var rect = GetNodeRect(node);
        if (_measured.TryGetValue(node.Id, out var m) && m.Handles.Count > 0)
        {
            HandleInfo? match = null;
            foreach (var h in m.Handles)
            {
                if (h.Type != type) continue;
                if (handleId is null || string.Equals(h.Id, handleId, StringComparison.Ordinal))
                {
                    match = h;
                    break;
                }
            }
            if (match is not null)
            {
                return (new FlowPoint(node.X + match.X, node.Y + match.Y), match.Position);
            }
        }
        var side = type == FlowHandleType.Source ? FlowPosition.Right : FlowPosition.Left;
        return (FlowGeometry.GetHandleAnchor(rect, side), side);
    }

    /// <summary>The viewport that fits <paramref name="nodes"/> into the pane, or null when not computable.</summary>
    public FlowViewport? ComputeFit(IReadOnlyList<FlowNode> nodes, double padding, double minZoom, double maxZoom)
        => FlowGeometry.FitView(nodes.Select(GetNodeRect), PaneWidth, PaneHeight, padding, minZoom, maxZoom);

    /// <summary>True when every node has a fixed size or a measurement — a .NET-side fit is exact.</summary>
    public bool AllSized(IReadOnlyList<FlowNode> nodes)
        => nodes.All(n => (n.Width is not null && n.Height is not null) || _measured.ContainsKey(n.Id));
}
