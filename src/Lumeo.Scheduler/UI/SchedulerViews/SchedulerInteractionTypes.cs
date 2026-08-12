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

/// <summary>
/// A candidate override for a drop's committed Start/End/AllDay, offered by <c>CanDrop</c> when
/// it accepts a drop but wants to snap it to different values than the raw proposal (e.g. round
/// to the nearest business-hour boundary instead of merely refusing an out-of-hours drop).
/// Mirrors ReUI's <c>onEventUpdate</c> return shape (<c>{ start?, end?, allDay? }</c>) — see
/// <see cref="SchedulerDropResult"/>. Any member left <c>null</c> keeps the corresponding
/// <see cref="SchedulerScheduleDropContext"/> proposal (or, for <c>AllDay</c>, the source
/// event's current value) unchanged.
/// </summary>
/// <param name="Start">Overrides <see cref="SchedulerScheduleDropContext.ProposedStart"/> when set.</param>
/// <param name="End">Overrides <see cref="SchedulerScheduleDropContext.ProposedEnd"/> when set.</param>
/// <param name="AllDay">Overrides the committed event's <c>AllDay</c> flag when set.</param>
public sealed record SchedulerDropAdjustment(DateTime? Start = null, DateTime? End = null, bool? AllDay = null);

/// <summary>
/// The three-way verdict a <c>CanDrop</c> predicate returns for a candidate drop: reject it
/// outright, accept it exactly as proposed, or accept it WITH an <see cref="SchedulerDropAdjustment"/>
/// that snaps the eventually-committed Start/End/AllDay to different values than what was
/// proposed. Widens the wave-1 binary allow/reject contract (spec §3.2) while <c>CanDrop</c> is
/// still <c>PublicAPI.Unshipped</c> — done now, deliberately, before it ships and this would
/// become a breaking change (mirrors why Gantt v3's own <c>OnTaskUpdate</c> is scheduled for a
/// PublicAPI-promotion release specifically because ITS binary contract already shipped).
///
/// An implicit <c>bool</c> conversion keeps the common "just allow/reject it" case exactly as
/// simple as returning a raw <c>bool</c> was before this type existed — e.g.
/// <c>CanDrop="(ev, ctx) => true"</c> still compiles unchanged against the widened
/// <c>Func&lt;SchedulerEvent, SchedulerScheduleDropContext, SchedulerDropResult&gt;</c> parameter
/// type.
/// </summary>
public readonly struct SchedulerDropResult : IEquatable<SchedulerDropResult>
{
    /// <summary>Whether the drop is permitted at all (with or without an <see cref="Adjustment"/>).</summary>
    public bool Accepted { get; }

    /// <summary>
    /// When set, the Start/End/AllDay a consumer wants committed instead of the raw proposal.
    /// Always <c>null</c> when <see cref="Accepted"/> is <c>false</c> (a rejected drop has
    /// nothing to adjust).
    /// </summary>
    public SchedulerDropAdjustment? Adjustment { get; }

    private SchedulerDropResult(bool accepted, SchedulerDropAdjustment? adjustment)
    {
        Accepted = accepted;
        Adjustment = accepted ? adjustment : null;
    }

    /// <summary>Rejects the drop outright.</summary>
    public static readonly SchedulerDropResult Reject = new(false, null);

    /// <summary>Accepts the drop exactly as proposed — no adjustment.</summary>
    public static readonly SchedulerDropResult Accept = new(true, null);

    /// <summary>Accepts the drop, but commits <paramref name="adjustment"/>'s values instead of the raw proposal.</summary>
    public static SchedulerDropResult AcceptWith(SchedulerDropAdjustment adjustment) => new(true, adjustment);

    /// <summary>Keeps <c>CanDrop="(ev, ctx) => true"</c>/<c>false</c> lambdas source-compatible with the widened delegate type.</summary>
    public static implicit operator SchedulerDropResult(bool accepted) => accepted ? Accept : Reject;

    public bool Equals(SchedulerDropResult other) => Accepted == other.Accepted && Equals(Adjustment, other.Adjustment);
    public override bool Equals(object? obj) => obj is SchedulerDropResult other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Accepted, Adjustment);
    public static bool operator ==(SchedulerDropResult left, SchedulerDropResult right) => left.Equals(right);
    public static bool operator !=(SchedulerDropResult left, SchedulerDropResult right) => !left.Equals(right);
}
