namespace Lumeo.GanttV3;

/// <summary>
/// A half-open date window describing which portion of the timeline is currently
/// materialized/rendered (design spec "Virtualization" — horizontal windowed time
/// range that extends on scroll). Pure data: no timezone conversion is ever applied
/// to <see cref="Start"/>/<see cref="End"/> anywhere in GanttV3 (see the TZ/DST-safety
/// note on <see cref="GanttScale"/>), so they keep whatever <see cref="DateTimeKind"/>
/// the caller gave them.
/// </summary>
internal readonly record struct GanttDateRange(DateTime Start, DateTime End);

/// <summary>
/// Hoistable Gantt store — the REUI <c>useGanttState</c> analog called out in the
/// design spec ("Public API" &gt; Additive &gt; <c>GanttState</c>: "nav and view can be
/// rendered separately against one shared state instance"). Deliberately a plain C#
/// class with zero Blazor/DI/CascadingValue dependency, so it can be constructed and
/// unit-tested in isolation and so a single instance can later be shared between a
/// separately-rendered <c>GanttNav</c> and <c>GanttTimeline</c> (Phase 2/3) without
/// either owning the other's lifetime.
///
/// <c>internal</c> for now: the phase-1 plan promotes only <see cref="GanttTask.ParentId"/>
/// to the public surface (see PublicAPI.Unshipped.txt) — this type becomes public API
/// once a component actually exposes a <c>State</c> parameter wired to it (Phase 3).
///
/// Every mutator is idempotent with respect to <see cref="Changed"/>: setting a value
/// that is already current is a silent no-op and does not raise the event. This mirrors
/// the Gantt (v2) component's own change-detection discipline (hash-gated re-pushes —
/// see <c>Gantt.razor</c>'s <c>ComputeTasksHash</c>/<c>_lastOptionsHash</c> pattern) so a
/// caller that re-applies the same value on every render (a common Blazor pattern)
/// doesn't spuriously notify subscribers.
/// </summary>
internal sealed class GanttState
{
    private List<GanttTask> _tasks = new();
    private GanttViewMode _viewMode = GanttViewMode.Day;
    private GanttDateRange _visibleRange;
    private readonly HashSet<string> _collapsed = new();

    /// <summary>Raised after any mutator below actually changes state. Not raised for no-op sets (same value re-applied).</summary>
    public event Action? Changed;

    /// <summary>The current task set. Replace via <see cref="SetTasks"/> — this list is never mutated in place.</summary>
    public IReadOnlyList<GanttTask> Tasks => _tasks;

    /// <summary>The active view mode. Change via <see cref="SetViewMode"/>.</summary>
    public GanttViewMode ViewMode => _viewMode;

    /// <summary>The currently materialized date window. Change via <see cref="SetVisibleRange(DateTime,DateTime)"/>.</summary>
    public GanttDateRange VisibleRange => _visibleRange;

    /// <summary>Ids of collapsed (children-hidden) rows. Mutate via <see cref="SetCollapsed"/>/<see cref="ToggleCollapsed"/>.</summary>
    public IReadOnlySet<string> Collapsed => _collapsed;

    /// <summary>
    /// Replaces the task set. No-ops (and does not raise <see cref="Changed"/>) when
    /// the new sequence is value-equal, element-for-element, to the current one —
    /// <see cref="GanttTask"/> is a record, so this is a cheap structural comparison
    /// that also picks up a <see cref="GanttTask.ParentId"/>-only change.
    /// </summary>
    public void SetTasks(IEnumerable<GanttTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var next = tasks as IReadOnlyList<GanttTask> ?? tasks.ToList();
        if (TasksEqual(_tasks, next)) return;
        _tasks = next as List<GanttTask> ?? next.ToList();
        RaiseChanged();
    }

    /// <summary>Sets the active view mode. No-op when unchanged.</summary>
    public void SetViewMode(GanttViewMode mode)
    {
        if (_viewMode == mode) return;
        _viewMode = mode;
        RaiseChanged();
    }

    /// <summary>Sets the materialized date window. No-op when unchanged.</summary>
    public void SetVisibleRange(DateTime start, DateTime end) => SetVisibleRange(new GanttDateRange(start, end));

    /// <summary>Sets the materialized date window. No-op when unchanged.</summary>
    public void SetVisibleRange(GanttDateRange range)
    {
        if (_visibleRange == range) return;
        _visibleRange = range;
        RaiseChanged();
    }

    /// <summary>True when the given task/row id is currently collapsed.</summary>
    public bool IsCollapsed(string taskId) => _collapsed.Contains(taskId);

    /// <summary>Sets or clears the collapsed state for a task/row id. No-op when unchanged.</summary>
    public void SetCollapsed(string taskId, bool collapsed)
    {
        var changed = collapsed ? _collapsed.Add(taskId) : _collapsed.Remove(taskId);
        if (changed) RaiseChanged();
    }

    /// <summary>Flips the collapsed state for a task/row id. Always raises <see cref="Changed"/> (a toggle is never a no-op).</summary>
    public void ToggleCollapsed(string taskId) => SetCollapsed(taskId, !IsCollapsed(taskId));

    private void RaiseChanged() => Changed?.Invoke();

    private static bool TasksEqual(IReadOnlyList<GanttTask> a, IReadOnlyList<GanttTask> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
