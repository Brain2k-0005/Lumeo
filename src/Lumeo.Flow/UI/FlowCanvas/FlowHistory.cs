namespace Lumeo;

/// <summary>
/// An undo/redo stack of <see cref="FlowCanvas"/> snapshots. Framework-free — plumb it into a canvas
/// with the <c>History</c> parameter (the canvas pushes on every committed change and wires
/// <c>Ctrl+Z</c>/<c>Ctrl+Y</c>/<c>Ctrl+Shift+Z</c>), or drive it yourself and apply
/// <see cref="Undo"/>/<see cref="Redo"/>'s result to <c>@bind-Nodes</c>/<c>@bind-Edges</c>.
/// </summary>
public sealed class FlowHistory
{
    /// <summary>An immutable node+edge pair — one point in the undo stack.</summary>
    /// <param name="Nodes">The node list at this point.</param>
    /// <param name="Edges">The edge list at this point.</param>
    public sealed record Snapshot(IReadOnlyList<FlowNode> Nodes, IReadOnlyList<FlowEdge> Edges);

    private readonly List<Snapshot> _stack = new();
    private int _cursor = -1; // index of the current snapshot in _stack; -1 = nothing pushed yet

    /// <summary>Creates a history capped at <paramref name="capacity"/> snapshots (default 50; the oldest is dropped once full).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 1.</exception>
    public FlowHistory(int capacity = 50)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");
        Capacity = capacity;
    }

    /// <summary>The maximum number of snapshots kept.</summary>
    public int Capacity { get; }

    /// <summary>The number of snapshots currently kept (past + present + future).</summary>
    public int Count => _stack.Count;

    /// <summary>Whether <see cref="Undo"/> would return a snapshot.</summary>
    public bool CanUndo => _cursor > 0;

    /// <summary>Whether <see cref="Redo"/> would return a snapshot.</summary>
    public bool CanRedo => _cursor >= 0 && _cursor < _stack.Count - 1;

    /// <summary>The snapshot <see cref="Undo"/>/<see cref="Redo"/> currently sit on, or <c>null</c> before the first <see cref="Push"/>.</summary>
    public Snapshot? Current => _cursor >= 0 ? _stack[_cursor] : null;

    /// <summary>Raised after <see cref="Push"/>, <see cref="Undo"/>, <see cref="Redo"/> or <see cref="Clear"/> change what <see cref="CanUndo"/>/<see cref="CanRedo"/> report.</summary>
    public event Action? Changed;

    /// <summary>
    /// Records a new current state, discarding any redo history beyond it (a push after an undo — the
    /// normal "you made a new edit instead of redoing" rule). The very first call seeds the baseline
    /// nothing can undo past; push the canvas' starting <c>Nodes</c>/<c>Edges</c> once before any edit
    /// so the first real edit can still be undone back to it.
    /// </summary>
    public void Push(IReadOnlyList<FlowNode> nodes, IReadOnlyList<FlowEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);
        if (_cursor < _stack.Count - 1) _stack.RemoveRange(_cursor + 1, _stack.Count - _cursor - 1);
        _stack.Add(new Snapshot(nodes, edges));
        _cursor = _stack.Count - 1;
        if (_stack.Count > Capacity)
        {
            var drop = _stack.Count - Capacity;
            _stack.RemoveRange(0, drop);
            _cursor -= drop;
        }
        Changed?.Invoke();
    }

    /// <summary>Moves one step back and returns the snapshot to apply, or <c>null</c> when <see cref="CanUndo"/> is <c>false</c>.</summary>
    public Snapshot? Undo()
    {
        if (!CanUndo) return null;
        _cursor--;
        Changed?.Invoke();
        return _stack[_cursor];
    }

    /// <summary>Moves one step forward and returns the snapshot to apply, or <c>null</c> when <see cref="CanRedo"/> is <c>false</c>.</summary>
    public Snapshot? Redo()
    {
        if (!CanRedo) return null;
        _cursor++;
        Changed?.Invoke();
        return _stack[_cursor];
    }

    /// <summary>Discards every snapshot. <see cref="Current"/> becomes <c>null</c>; the next <see cref="Push"/> re-seeds the baseline.</summary>
    public void Clear()
    {
        if (_stack.Count == 0) return;
        _stack.Clear();
        _cursor = -1;
        Changed?.Invoke();
    }
}
