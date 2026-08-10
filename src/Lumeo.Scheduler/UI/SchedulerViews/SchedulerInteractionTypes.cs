namespace Lumeo;

/// <summary>
/// Which gesture produced a candidate (Start, End) window during a Scheduler drag/resize/create
/// gesture. Mirrors <c>GanttTaskUpdateSource</c>
/// (<c>src/Lumeo.Gantt/UI/GanttV3/GanttV3Types.cs</c>) — the Scheduler's interaction engine
/// deliberately reuses the same shape (spec §3.1/§3.2).
/// </summary>
public enum SchedulerEventUpdateSource
{
    /// <summary>The whole event was dragged to a new day/slot; its duration is unchanged.</summary>
    Move,

    /// <summary>The event's start edge was dragged (Week/Day time-grid only — month has no edge-resize).</summary>
    ResizeStart,

    /// <summary>The event's end edge was dragged (Week/Day time-grid only — month has no edge-resize).</summary>
    ResizeEnd,

    /// <summary>A brand-new event window was drawn by dragging on empty grid background.</summary>
    Create
}

/// <summary>
/// The candidate drop position offered to <c>CanDrop</c> before a move/resize/create commits.
/// Mirrors <c>GanttScheduleDropContext</c>'s shape and role — see
/// <see cref="SchedulerEventUpdateSource"/>'s remarks. Spec §3.2.
/// </summary>
/// <param name="ProposedStart">The candidate Start the gesture would commit if <c>CanDrop</c> returns <c>true</c>.</param>
/// <param name="ProposedEnd">The candidate End the gesture would commit if <c>CanDrop</c> returns <c>true</c>.</param>
/// <param name="Source">Which gesture produced this candidate.</param>
public sealed record SchedulerScheduleDropContext(DateTime ProposedStart, DateTime ProposedEnd, SchedulerEventUpdateSource Source);
