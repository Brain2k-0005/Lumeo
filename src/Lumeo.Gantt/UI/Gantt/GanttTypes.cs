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

    /// <summary>
    /// Optional parent task id, enabling GanttV3's tree-pane hierarchy (design spec
    /// "Public API" &gt; Additive: "GanttTask.ParentId (new optional field -&gt;
    /// hierarchy; GroupBy keeps working as flat grouping)"). Null (the default) means
    /// a root-level task — the same as every existing task before this field existed.
    ///
    /// Deliberately added as a non-positional <c>init</c> property in the record body
    /// rather than a new positional-constructor parameter: the positional constructor
    /// and <c>Deconstruct</c> are already-shipped public API (PublicAPI.Shipped.txt),
    /// and CONTRIBUTING.md's API-stability policy forbids changing a shipped member's
    /// signature outside the Obsolete-one-minor-then-major flow. A body property is
    /// purely additive — it does not touch the existing constructor/Deconstruct
    /// signatures, so v2 (which never sets or reads it) keeps compiling and behaving
    /// identically. It IS still picked up by System.Text.Json's property-based
    /// (de)serialization (the parameterless ctor above), by the compiler-generated
    /// record Equals/GetHashCode (so <see cref="GanttTask"/> equality still reflects a
    /// ParentId-only change), and by <c>with</c>-expressions.
    /// </summary>
    public string? ParentId { get; init; }
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
