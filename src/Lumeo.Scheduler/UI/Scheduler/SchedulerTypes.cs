namespace Lumeo;

/// <summary>
/// A calendar/scheduler event. Used as the data contract between Blazor and the
/// FullCalendar JS wrapper.
/// </summary>
/// <param name="Id">Stable identifier used to reconcile edits back into the caller's collection.</param>
/// <param name="Title">The label rendered on the event chip.</param>
/// <param name="Start">Start timestamp (inclusive).</param>
/// <param name="End">End timestamp (exclusive, per FullCalendar convention).</param>
/// <param name="AllDay">When true the event is rendered in the all-day lane.</param>
/// <param name="Color">CSS color or variable reference, e.g. "var(--color-primary)".</param>
/// <param name="Url">Optional link opened on click instead of firing OnEventClick.</param>
/// <param name="ExtendedProps">Arbitrary app-level metadata round-tripped through the JS layer.</param>
/// <param name="DaysOfWeek">
/// For recurring events: the days of the week on which the event repeats.
/// Uses FullCalendar's free simple recurrence model (no premium rrule plugin required).
/// e.g. [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday].
/// When set, <see cref="Start"/> and <see cref="End"/> provide the start/end times
/// (time-of-day part only) and <see cref="RecurrenceEnd"/> bounds the recurrence.
/// </param>
/// <param name="RecurrenceEnd">
/// Optional end date for a recurring event. The event will not appear after this date.
/// Only meaningful when <see cref="DaysOfWeek"/> is set.
/// </param>
/// <param name="ExceptionDates">
/// Dates on which a recurring event should be skipped (one-off exclusions).
/// Only meaningful when <see cref="DaysOfWeek"/> is set.
/// </param>
/// <param name="ResourceId">
/// Optional resource identifier. When a <c>Resources</c> list is provided on the
/// <see cref="Scheduler"/> component, events are color-coded by their resource and
/// a resource legend is rendered above the calendar.
/// </param>
/// <param name="ClassNames">
/// Extra CSS class names to apply to the event chip, as produced by the
/// <c>EventClassNames</c> callback on the Scheduler component (or set directly here).
/// </param>
public record SchedulerEvent(
    string Id,
    string Title,
    DateTime Start,
    DateTime End,
    bool AllDay = false,
    string? Color = null,
    string? Url = null,
    Dictionary<string, object>? ExtendedProps = null,
    IReadOnlyList<DayOfWeek>? DaysOfWeek = null,
    DateTime? RecurrenceEnd = null,
    IReadOnlyList<DateTime>? ExceptionDates = null,
    string? ResourceId = null,
    string? ClassNames = null
);

/// <summary>
/// A named resource (person, room, equipment) used by the Scheduler for
/// color-coding events. Does not require the FullCalendar Premium resource plugin.
/// </summary>
/// <param name="Id">Identifier matched against <see cref="SchedulerEvent.ResourceId"/>.</param>
/// <param name="Title">Display name shown in the resource legend.</param>
/// <param name="Color">
/// CSS color applied to events belonging to this resource when the event itself
/// does not supply its own <see cref="SchedulerEvent.Color"/>.
/// </param>
public record SchedulerResource(string Id, string Title, string? Color = null);

/// <summary>
/// A date range produced when the user drag-selects in the calendar.
/// </summary>
public record SchedulerDateRange(DateTime Start, DateTime End, bool AllDay);

/// <summary>
/// Built-in views exposed by the Lumeo scheduler. Maps onto FullCalendar's
/// dayGridMonth / timeGridWeek / timeGridDay / listWeek view names.
/// </summary>
public enum SchedulerView
{
    Month,
    Week,
    Day,
    List
}
