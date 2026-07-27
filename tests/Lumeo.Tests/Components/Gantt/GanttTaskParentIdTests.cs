using System.Text.Json;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Gantt;

/// <summary>
/// Regression coverage for GanttV3's additive <see cref="L.GanttTask.ParentId"/>
/// field (Gantt v3 phase-1 plan, T1): it must default to null (every pre-existing
/// task/fixture keeps working unchanged), round-trip through both `with`-expressions
/// and System.Text.Json exactly like every other property, and — critically — must
/// NOT have disturbed the trim-safe JSON contract the JS<->.NET interop boundary
/// relies on (see <see cref="Lumeo.Tests.Serialization.InteropPayloadTrimSafetyTests"/>,
/// which already asserts <c>GanttTask</c> keeps its public parameterless constructor).
/// </summary>
public class GanttTaskParentIdTests
{
    private static L.GanttTask Task1 =>
        new("t1", "Design", new DateTime(2026, 1, 1), new DateTime(2026, 1, 5), 20);

    [Fact]
    public void ParentId_Defaults_To_Null()
    {
        Assert.Null(Task1.ParentId);
    }

    [Fact]
    public void ParentId_Is_Settable_Via_With_Expression()
    {
        var child = Task1 with { ParentId = "parent-1" };

        Assert.Equal("parent-1", child.ParentId);
        // Everything else is untouched by the `with` — additive, not a new shape.
        Assert.Equal(Task1.Id, child.Id);
        Assert.Equal(Task1.Start, child.Start);
    }

    [Fact]
    public void Equality_Reflects_A_ParentId_Only_Difference()
    {
        var a = Task1 with { ParentId = "p1" };
        var b = Task1 with { ParentId = "p2" };
        var c = Task1 with { ParentId = "p1" };

        Assert.NotEqual(a, b);
        Assert.Equal(a, c);
    }

    [Fact]
    public void ParentId_Round_Trips_Through_System_Text_Json_Property_Based_Deserialization()
    {
        // Mirrors how JSRuntime deserializes GanttTask across the JS interop boundary
        // (JsOnTaskClick/JsOnDateChange/JsOnProgressChange [JSInvokable] parameters):
        // reflection-based STJ, no [JsonConstructor] — it must use the parameterless
        // ctor + property setters, which is exactly what makes this trim-safe.
        const string json = """
            {"Id":"t1","Name":"Design","Start":"2026-01-01T00:00:00","End":"2026-01-05T00:00:00","Progress":20,"ParentId":"g1"}
            """;

        var task = JsonSerializer.Deserialize<L.GanttTask>(json);

        Assert.NotNull(task);
        Assert.Equal("t1", task!.Id);
        Assert.Equal("g1", task.ParentId);

        // And serializing back out includes it under its own property name — no
        // custom converter or [JsonPropertyName] needed for an additive field.
        var roundTripped = JsonSerializer.Serialize(task);
        Assert.Contains("\"ParentId\":\"g1\"", roundTripped);
    }

    [Fact]
    public void ParentId_Absent_From_Json_Deserializes_To_Null_Not_A_Missing_Member_Error()
    {
        // Every pre-existing JS payload (v2's taskToJson never sends ParentId) must
        // keep deserializing without error — this is the "additive" guarantee.
        const string json = """{"Id":"t1","Name":"Design","Start":"2026-01-01T00:00:00","End":"2026-01-05T00:00:00"}""";

        var task = JsonSerializer.Deserialize<L.GanttTask>(json);

        Assert.NotNull(task);
        Assert.Null(task!.ParentId);
    }
}
