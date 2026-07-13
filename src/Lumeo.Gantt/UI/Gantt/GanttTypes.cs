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
)
{
    // Trim safety: this record is deserialized from JS (JsOnTaskClick/JsOnDateChange/
    // JsOnProgressChange [JSInvokable] parameters). JSRuntime's reflection-based
    // serializer must never bind the positional ctor — the trimmer strips its parameter
    // names ("ConstructorContainsNullParameterNames", crashes the component under a
    // trimmed publish). With this parameterless ctor STJ uses property-based
    // (de)serialization instead. Do not remove.
    public GanttTask() : this("", "", default, default) { }
}

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
