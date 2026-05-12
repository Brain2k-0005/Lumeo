namespace Lumeo;

/// <summary>
/// A task bar rendered on the Gantt chart. Used as the data contract between
/// Blazor and the Lumeo SVG Gantt renderer.
/// </summary>
public record GanttTask(
    string Id,
    string Name,
    DateTime Start,
    DateTime End,
    int Progress = 0,                // 0-100
    string[]? Dependencies = null,   // array of Task Ids
    string? CustomClass = null,
    bool IsMilestone = false,        // renders as a diamond; zero-duration point event
    string? GroupLabel = null        // optional swim-lane / group header label
);

/// <summary>
/// Gantt timeline zoom level. Supported by the Lumeo SVG engine:
/// QuarterDay = 6-hour columns, HalfDay = 12-hour columns,
/// Day = 1 column/day, Week = 1 column/week,
/// Month = 1 column/month, Year = 1 column/year.
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
