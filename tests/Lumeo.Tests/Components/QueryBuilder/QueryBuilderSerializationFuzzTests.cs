using System.Text.Json;
using Xunit;
using Lumeo;

namespace Lumeo.Tests.Components.QueryBuilder;

/// <summary>
/// Root-cause consolidation test matrix for <see cref="Lumeo.QueryBuilderSerialization"/>'s
/// <c>QueryRule.Value</c>/<c>Value2</c> and <c>QueryGroup.Combinator</c> converters (#366
/// review, round 3). Three consecutive review rounds each found one more boxed scalar type the
/// hand-rolled <c>QueryValueJsonConverter</c> didn't handle (discriminator shape, then oversized
/// numbers/custom values, then unsigned integrals/enum/Guid/corrupted-combinator). The Write
/// side now dispatches numerics via <see cref="TypeCode"/> — a CLOSED set — instead of an ad hoc
/// per-type case list, specifically to make this matrix exhaustive rather than reactive.
///
/// Covers: every numeric TypeCode at its boundary values, enum, Guid, NaN/Infinity rejection,
/// nested groups, empty groups, unicode, and the numeric-string combinator corruption from the
/// CodeRabbit/Codex round-3 findings.
/// </summary>
public class QueryBuilderSerializationFuzzTests
{
    private enum Priority { Low, Medium, High }

    // --- Every numeric TypeCode round-trips exactly, including its type-specific extremes ---

    public static IEnumerable<object[]> NumericBoundaryValues()
    {
        yield return new object[] { byte.MinValue };
        yield return new object[] { byte.MaxValue };
        yield return new object[] { sbyte.MinValue };
        yield return new object[] { sbyte.MaxValue };
        yield return new object[] { short.MinValue };
        yield return new object[] { short.MaxValue };
        yield return new object[] { ushort.MinValue };
        yield return new object[] { ushort.MaxValue };
        yield return new object[] { int.MinValue };
        yield return new object[] { int.MaxValue };
        yield return new object[] { uint.MinValue };
        yield return new object[] { uint.MaxValue };
        yield return new object[] { long.MinValue };
        yield return new object[] { long.MaxValue };
        yield return new object[] { ulong.MinValue };
        yield return new object[] { ulong.MaxValue };
        yield return new object[] { decimal.MinValue };
        yield return new object[] { decimal.MaxValue };
        yield return new object[] { 0.1f };
        yield return new object[] { float.MinValue };
        yield return new object[] { float.MaxValue };
        yield return new object[] { 0.1d };
        yield return new object[] { double.MinValue };
        yield return new object[] { double.MaxValue };
    }

    [Theory]
    [MemberData(nameof(NumericBoundaryValues))]
    public void Numeric_Value_Round_Trips_At_Its_Type_Boundary(object value)
    {
        var query = Lumeo.QueryGroup.CreateEmpty();
        query.Rules.Add(new Lumeo.QueryRule { Field = "x", Operator = "=", Value = value });

        var json = Lumeo.QueryBuilder.ToJson(query);
        var parsed = Lumeo.QueryBuilder.FromJson(json);

        Assert.NotNull(parsed);
        var rule = Assert.IsType<Lumeo.QueryRule>(Assert.Single(parsed!.Rules));
        Assert.NotNull(rule.Value);

        // ReadNumber only ever materializes Int64/Decimal/Double (the three exact-precision
        // shapes), so a byte/short/uint/etc. Value comes back widened rather than as its
        // original narrow type. What must hold is that the WRITTEN literal is numerically exact.
        // decimal.MaxValue (~7.9e28) is smaller than float/double.MaxValue (~3.4e38/~1.8e308), so
        // decimal can't be the common comparison type for every case here — use double for
        // Single/Double (their natural range) and decimal (exact, no binary-fraction error) for
        // every integral/Decimal source.
        var expectedDouble = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        var actualDouble = Convert.ToDouble(rule.Value, System.Globalization.CultureInfo.InvariantCulture);
        if (value is float or double)
        {
            // float is widened to double before writing (see WriteNumeric), so the round trip
            // is bit-exact once compared as double — no tolerance needed.
            Assert.Equal(expectedDouble, actualDouble);
        }
        else
        {
            var expectedText = ((IFormattable)value).ToString(null, System.Globalization.CultureInfo.InvariantCulture);
            var actualText = ((IFormattable)rule.Value!).ToString(null, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(decimal.Parse(expectedText!, System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(actualText!, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    // --- Enum values (#366 round-3 finding: "Serialize enum and Guid query values") ---

    [Fact]
    public void Enum_Value_Round_Trips_As_A_String_And_Reloads_Via_ConvertValue()
    {
        var query = Lumeo.QueryGroup.CreateEmpty();
        query.Rules.Add(new Lumeo.QueryRule { Field = nameof(Widget.Priority), Operator = "=", Value = Priority.High });

        var json = Lumeo.QueryBuilder.ToJson(query);
        Assert.Contains("\"High\"", json);

        var parsed = Lumeo.QueryBuilder.FromJson(json);
        var predicate = Lumeo.QueryBuilder.ToExpression<Widget>(parsed!)!.Compile();

        Assert.True(predicate(new Widget { Priority = Priority.High }));
        Assert.False(predicate(new Widget { Priority = Priority.Low }));
    }

    // --- Guid values (#366 round-3 finding) ---

    [Fact]
    public void Guid_Value_Round_Trips_As_A_String_And_Reloads_Via_ConvertValue()
    {
        var id = Guid.NewGuid();
        var query = Lumeo.QueryGroup.CreateEmpty();
        query.Rules.Add(new Lumeo.QueryRule { Field = nameof(Widget.Id), Operator = "=", Value = id });

        var json = Lumeo.QueryBuilder.ToJson(query);
        Assert.Contains(id.ToString(), json);

        var parsed = Lumeo.QueryBuilder.FromJson(json);
        var predicate = Lumeo.QueryBuilder.ToExpression<Widget>(parsed!)!.Compile();

        Assert.True(predicate(new Widget { Id = id }));
        Assert.False(predicate(new Widget { Id = Guid.NewGuid() }));
    }

    // --- Unsigned integrals (#366 round-3 finding: "Add cases for unsigned numeric query values") ---

    [Theory]
    [InlineData((byte)200)]
    [InlineData((ushort)60000)]
    [InlineData(3000000000u)]
    [InlineData(18000000000000000000ul)]
    public void Unsigned_Integral_Value_Serializes_Instead_Of_Throwing(object value)
    {
        var query = Lumeo.QueryGroup.CreateEmpty();
        query.Rules.Add(new Lumeo.QueryRule { Field = "x", Operator = "=", Value = value });

        var json = Lumeo.QueryBuilder.ToJson(query);
        var parsed = Lumeo.QueryBuilder.FromJson(json);

        Assert.NotNull(parsed);
        Assert.NotNull(Assert.IsType<Lumeo.QueryRule>(Assert.Single(parsed!.Rules)).Value);
    }

    // --- NaN/Infinity: WriteNumberValue's ArgumentException must become the documented JsonException ---

    [Theory]
    [MemberData(nameof(NonFiniteValues))]
    public void NonFinite_Float_Or_Double_Value_Fails_The_Write_Instead_Of_Throwing_ArgumentException(object value)
    {
        var query = Lumeo.QueryGroup.CreateEmpty();
        query.Rules.Add(new Lumeo.QueryRule { Field = "x", Operator = "=", Value = value });

        Assert.Throws<JsonException>(() => Lumeo.QueryBuilder.ToJson(query));
    }

    public static IEnumerable<object[]> NonFiniteValues()
    {
        yield return new object[] { double.NaN };
        yield return new object[] { double.PositiveInfinity };
        yield return new object[] { double.NegativeInfinity };
        yield return new object[] { float.NaN };
        yield return new object[] { float.PositiveInfinity };
        yield return new object[] { float.NegativeInfinity };
    }

    // --- Numeric-string combinator corruption (#366 round-3 finding: "Reject numeric combinator strings") ---

    [Theory]
    [InlineData("2")]
    [InlineData("-1")]
    [InlineData("99")]
    public void Numeric_String_Combinator_Fails_The_Parse_Instead_Of_Accepting_An_Undefined_Value(string numeric)
    {
        var json = $$"""{"combinator":"{{numeric}}","rules":[]}""";

        var parsed = Lumeo.QueryBuilder.FromJson(json);

        Assert.Null(parsed);
    }

    // --- Nested groups round-trip structure, discriminators, and combinators ---

    [Fact]
    public void Deeply_Nested_Groups_Round_Trip_Structure_And_Combinators()
    {
        var query = new Lumeo.QueryGroup
        {
            Combinator = QueryCombinator.Or,
            Rules =
            {
                new Lumeo.QueryRule { Field = "a", Operator = "=", Value = "x" },
                new Lumeo.QueryGroup
                {
                    Combinator = QueryCombinator.And,
                    Rules =
                    {
                        new Lumeo.QueryRule { Field = "b", Operator = ">", Value = 1 },
                        new Lumeo.QueryGroup
                        {
                            Combinator = QueryCombinator.Or,
                            Rules = { new Lumeo.QueryRule { Field = "c", Operator = "contains", Value = "z" } }
                        }
                    }
                }
            }
        };

        var json = Lumeo.QueryBuilder.ToJson(query);
        var parsed = Lumeo.QueryBuilder.FromJson(json);

        Assert.NotNull(parsed);
        Assert.Equal(QueryCombinator.Or, parsed!.Combinator);
        Assert.Equal(2, parsed.Rules.Count);
        var inner = Assert.IsType<Lumeo.QueryGroup>(parsed.Rules[1]);
        Assert.Equal(QueryCombinator.And, inner.Combinator);
        Assert.Equal(2, inner.Rules.Count);
        var innermost = Assert.IsType<Lumeo.QueryGroup>(inner.Rules[1]);
        Assert.Equal(QueryCombinator.Or, innermost.Combinator);
        Assert.Equal("z", Assert.IsType<Lumeo.QueryRule>(Assert.Single(innermost.Rules)).Value);
    }

    // --- Empty groups round-trip and compile to no predicate ---

    [Fact]
    public void Empty_Root_Group_Round_Trips_And_ToExpression_Returns_Null()
    {
        var query = Lumeo.QueryGroup.CreateEmpty();

        var json = Lumeo.QueryBuilder.ToJson(query);
        var parsed = Lumeo.QueryBuilder.FromJson(json);

        Assert.NotNull(parsed);
        Assert.Empty(parsed!.Rules);
        Assert.Null(Lumeo.QueryBuilder.ToExpression<Widget>(parsed));
    }

    [Fact]
    public void Nested_Empty_Group_Contributes_Nothing_To_The_Compiled_Predicate()
    {
        var query = new Lumeo.QueryGroup
        {
            Rules =
            {
                new Lumeo.QueryRule { Field = "Name", Operator = "=", Value = "ok" },
                Lumeo.QueryGroup.CreateEmpty()
            }
        };

        var json = Lumeo.QueryBuilder.ToJson(query);
        var parsed = Lumeo.QueryBuilder.FromJson(json);
        var predicate = Lumeo.QueryBuilder.ToExpression<Widget>(parsed!)!.Compile();

        Assert.True(predicate(new Widget { Name = "ok" }));
        Assert.False(predicate(new Widget { Name = "nope" }));
    }

    // --- Unicode field/value text survives the round trip untouched ---

    [Theory]
    [InlineData("héllo wörld")]
    [InlineData("日本語のテスト")]
    [InlineData("emoji 🎉🚀 mix")]
    [InlineData("naïvé combining")]
    public void Unicode_String_Value_Round_Trips_Exactly(string text)
    {
        var query = Lumeo.QueryGroup.CreateEmpty();
        query.Rules.Add(new Lumeo.QueryRule { Field = "Name", Operator = "=", Value = text });

        var json = Lumeo.QueryBuilder.ToJson(query);
        var parsed = Lumeo.QueryBuilder.FromJson(json);
        var rule = Assert.IsType<Lumeo.QueryRule>(Assert.Single(parsed!.Rules));

        Assert.Equal(text, rule.Value);
    }

    private sealed class Widget
    {
        public string Name { get; set; } = "";
        public Priority Priority { get; set; }
        public Guid Id { get; set; }
    }
}
