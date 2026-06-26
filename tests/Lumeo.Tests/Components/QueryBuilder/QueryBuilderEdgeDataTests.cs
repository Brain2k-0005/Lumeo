using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.QueryBuilder;

/// <summary>
/// Edge-data regression coverage for two battle-test (Wave 2) findings against
/// <see cref="Lumeo.QueryBuilder"/>:
///
/// #51 — a "between" rule whose upper bound (<see cref="QueryRule.Value2"/>) was never
/// seeded compiled to a silent `member &lt;= default(T)` (e.g. `&lt;= 0`), excluding almost
/// every row. The fix drops a missing bound so the range stays open-ended.
///
/// #53 — a rule whose <see cref="QueryRule.Field"/> is not in the supplied Fields list
/// silently lost its operator/value editors AND its dropdown snapped to the first field,
/// hiding the real stored value. The fix surfaces the unknown field (a selected, disabled
/// option) plus an inline warning, and only collapses once Fields is actually populated.
/// </summary>
public class QueryBuilderEdgeDataTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public QueryBuilderEdgeDataTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<QueryField> Fields() =>
    [
        new() { Name = "name", Label = "Name", Type = QueryFieldType.Text },
        new() { Name = "age", Label = "Age", Type = QueryFieldType.Number }
    ];

    // --- #51: "between" with a missing upper bound is open-ended, not <= default(T) ---

    [Fact]
    public void Between_With_Null_UpperBound_Compiles_To_OpenEnded_LowerBound_Only()
    {
        // Value2 (the upper bound) is left null — the user picked "between" but never typed
        // a second value. Pre-fix this compiled to `Age >= 18 && Age <= 0`, which is false
        // for every realistic row. The fix drops the absent upper term.
        var query = new QueryGroup
        {
            Rules = { new QueryRule { Field = nameof(Person.Age), Operator = "between", Value = 18, Value2 = null } }
        };

        var predicate = Lumeo.QueryBuilder.ToExpression<Person>(query);
        Assert.NotNull(predicate);
        var fn = predicate!.Compile();

        Assert.True(fn(new Person { Age = 30 }));   // >= 18, no upper cap — pre-fix this was FALSE (30 <= 0)
        Assert.True(fn(new Person { Age = 18 }));    // boundary still included
        Assert.False(fn(new Person { Age = 10 }));   // below the lower bound
    }

    [Fact]
    public void Between_With_Null_LowerBound_Compiles_To_OpenEnded_UpperBound_Only()
    {
        // Symmetric: only the upper bound is supplied.
        var query = new QueryGroup
        {
            Rules = { new QueryRule { Field = nameof(Person.Age), Operator = "between", Value = null, Value2 = 65 } }
        };

        var predicate = Lumeo.QueryBuilder.ToExpression<Person>(query);
        Assert.NotNull(predicate);
        var fn = predicate!.Compile();

        Assert.True(fn(new Person { Age = 30 }));    // <= 65, no lower cap
        Assert.False(fn(new Person { Age = 70 }));   // above the upper bound
    }

    [Fact]
    public void Between_With_Both_Bounds_Set_Still_Filters_Both_Sides()
    {
        // Normal-path behaviour is preserved exactly when both bounds are present.
        var query = new QueryGroup
        {
            Rules = { new QueryRule { Field = nameof(Person.Age), Operator = "between", Value = 18, Value2 = 65 } }
        };

        var fn = Lumeo.QueryBuilder.ToExpression<Person>(query)!.Compile();

        Assert.True(fn(new Person { Age = 30 }));
        Assert.False(fn(new Person { Age = 10 }));   // below lo
        Assert.False(fn(new Person { Age = 70 }));   // above hi
    }

    // --- #53: a rule pointing at a field absent from Fields is surfaced, not hidden ---

    [Fact]
    public void UnknownField_Rule_Surfaces_The_Stored_Field_And_A_Warning_Instead_Of_Hiding_Everything()
    {
        // The rule targets "removed" — a field NOT in the supplied list. Pre-fix the field
        // <select> snapped to the first known option and the operator/value editors silently
        // vanished, giving the user no indication their field is gone.
        var query = new QueryGroup
        {
            Rules = { new QueryRule { Field = "removed", Operator = "=", Value = "x" } }
        };

        var cut = _ctx.Render<Lumeo.QueryBuilder>(p => p
            .Add(q => q.Fields, Fields())
            .Add(q => q.Query, query));

        // The field <select> shows the stored-but-unknown value as a selected, disabled option.
        var fieldSelect = cut.FindAll("select").First(s => s.GetAttribute("aria-label") == "Field");
        var unknownOption = fieldSelect.QuerySelectorAll("option")
            .FirstOrDefault(o => o.GetAttribute("value") == "removed");
        Assert.NotNull(unknownOption);
        Assert.True(unknownOption!.HasAttribute("disabled"));
        Assert.Equal("removed", unknownOption.TextContent.Trim());

        // An inline warning is surfaced (role=alert), not a silent collapse.
        Assert.Contains(cut.FindAll("[role='alert']"), _ => true);
        Assert.Contains("Unknown field", cut.Markup);

        // And the operator picker stays absent (no valid operator set for an unknown field),
        // but the row is not just an empty field box — the warning is the affordance.
        Assert.DoesNotContain(cut.FindAll("select"), s => s.GetAttribute("aria-label") == "Operator");
    }

    [Fact]
    public void Empty_Fields_Does_Not_Treat_A_Rule_As_Unknown_Field()
    {
        // "Fields not yet loaded" (empty list) must NOT be mistaken for "field removed":
        // no warning should appear while Fields is empty.
        var query = new QueryGroup
        {
            Rules = { new QueryRule { Field = "age", Operator = "=", Value = "10" } }
        };

        var cut = _ctx.Render<Lumeo.QueryBuilder>(p => p
            .Add(q => q.Fields, new List<QueryField>())   // not yet loaded
            .Add(q => q.Query, query));

        Assert.DoesNotContain("Unknown field", cut.Markup);
        Assert.Empty(cut.FindAll("[role='alert']"));
    }

    [Fact]
    public void Known_Field_Rule_Renders_Operator_Editor_And_No_Warning()
    {
        // Normal-path: a rule whose field IS in the list renders its operator picker and shows
        // no unknown-field warning.
        var query = new QueryGroup
        {
            Rules = { new QueryRule { Field = "age", Operator = "=", Value = "10" } }
        };

        var cut = _ctx.Render<Lumeo.QueryBuilder>(p => p
            .Add(q => q.Fields, Fields())
            .Add(q => q.Query, query));

        Assert.Contains(cut.FindAll("select"), s => s.GetAttribute("aria-label") == "Operator");
        Assert.DoesNotContain("Unknown field", cut.Markup);
    }

    private sealed class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }
}
