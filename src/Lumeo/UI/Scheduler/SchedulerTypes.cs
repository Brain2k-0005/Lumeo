namespace Lumeo;

/// <summary>
/// A calendar/scheduler event. Used as the data contract between Blazor and the
/// FullCalendar JS wrapper.
/// </summary>
public record SchedulerEvent(
    string Id,
    string Title,
    DateTime Start,
    DateTime End,
    bool AllDay = false,
    string? Color = null,
    string? Url = null,
    Dictionary<string, object>? ExtendedProps = null
);

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
