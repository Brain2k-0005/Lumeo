namespace Lumeo;

/// <summary>
/// A task bar rendered on the Gantt chart. Used as the data contract between
/// Blazor and the Frappe Gantt JS wrapper.
/// </summary>
public record GanttTask(
    string Id,
    string Name,
    DateTime Start,
    DateTime End,
    int Progress = 0,                // 0-100
    string[]? Dependencies = null,   // array of Task Ids
    string? CustomClass = null
);

/// <summary>
/// Gantt timeline zoom level. Maps onto Frappe Gantt's view_mode strings
/// ("Quarter Day", "Half Day", "Day", "Week", "Month", "Year").
/// </summary>
public enum GanttViewMode
{
    QuarterDay,
    HalfDay,
    Day,
    Week,
    Month,
    Year
}
