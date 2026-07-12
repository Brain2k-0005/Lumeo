using System.Globalization;
using System.Text.Json;
using Xunit;
using Lumeo;

namespace Lumeo.Tests.Components.QueryBuilder;

/// <summary>
/// Regression coverage for three PR #364 review findings against
/// <see cref="Lumeo.QueryBuilderSerialization"/>'s <c>QueryRule.Value</c>/<c>Value2</c> and
/// <c>QueryGroup.Combinator</c> JSON converters:
///
/// P2 — a saved <c>long</c>/<c>decimal</c> query value was rehydrated as <c>double</c>,
/// rounding IDs above 2^53 or high-precision decimals before <c>ToExpression</c> ever
/// converted them to the target property type.
///
/// P2 — an invalid/corrupted <c>combinator</c> string silently became <c>And</c> instead of
/// failing the parse, changing the meaning of a corrupted query rather than surfacing it.
///
/// P2 — a <see cref="DateOnly"/>/<see cref="DateTime"/> query value was serialized with
/// culture-sensitive <c>ToString()</c>, which round-trips a different date than the one saved
/// once the JSON is re-parsed under <see cref="CultureInfo.InvariantCulture"/>
/// (<c>ToExpression</c>'s <c>ConvertValue</c>).
/// </summary>
public class QueryBuilderSerializationRoundTripTests
{
    private sealed class Widget
    {
        public string Name { get; set; } = "";
        public long BigId { get; set; }
        public decimal Amount { get; set; }
        public DateOnly Released { get; set; }
        public double Score { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    [Fact]
    public void Long_Value_Above_2_Pow_53_Round_Trips_Exactly()
    {
        // 2^53 + 1 == 9007199254740993 cannot be represented exactly as a double: rounding
        // it before ToExpression converts it to `long` would make the predicate match the
        // wrong BigId.
        const long id = 9007199254740993L;
        var query = Lumeo.QueryGroup.CreateEmpty();
        query.Rules.Add(new Lumeo.QueryRule { Field = "BigId", Operator = "=", Value = id });

        var json = Lumeo.QueryBuilder.ToJson(query);
        var parsed = Lumeo.QueryBuilder.FromJson(json);
        var rule = Assert.IsType<Lumeo.QueryRule>(Assert.Single(parsed!.Rules));

        Assert.Equal(id, Assert.IsType<long>(rule.Value));

        var predicate = Lumeo.QueryBuilder.ToExpression<Widget>(parsed)!.Compile();
        Assert.True(predicate(new Widget { BigId = id }));
        Assert.False(predicate(new Widget { BigId = id + 1 }));
    }

    [Fact]
    public void Decimal_Value_Round_Trips_Without_Precision_Loss()
    {
        // A double can't exactly represent every decimal fraction (e.g. money amounts);
        // rehydrating as double before ToExpression's Convert.ChangeType(..., decimal) risks
        // an off-by-a-fraction-of-a-cent comparison.
        const decimal amount = 19999999.87m;
        var query = Lumeo.QueryGroup.CreateEmpty();
        query.Rules.Add(new Lumeo.QueryRule { Field = "Amount", Operator = "=", Value = amount });

        var json = Lumeo.QueryBuilder.ToJson(query);
        var parsed = Lumeo.QueryBuilder.FromJson(json);
        var rule = Assert.IsType<Lumeo.QueryRule>(Assert.Single(parsed!.Rules));

        Assert.Equal(amount, Assert.IsType<decimal>(rule.Value));

        var predicate = Lumeo.QueryBuilder.ToExpression<Widget>(parsed)!.Compile();
        Assert.True(predicate(new Widget { Amount = amount }));
    }

    [Fact]
    public void Invalid_Combinator_Fails_The_Parse_Instead_Of_Defaulting_To_And()
    {
        // A corrupted "combinator":"bogus" must not silently become And — FromJson is
        // documented to return null on parse failure so callers don't run a different
        // predicate than the one that was saved.
        const string json = """{"combinator":"bogus","rules":[]}""";

        var parsed = Lumeo.QueryBuilder.FromJson(json);

        Assert.Null(parsed);
    }

    [Fact]
    public void DateOnly_Value_Round_Trips_Under_A_Non_Invariant_Culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var released = new DateOnly(2026, 7, 12);
            var query = Lumeo.QueryGroup.CreateEmpty();
            query.Rules.Add(new Lumeo.QueryRule { Field = "Released", Operator = "=", Value = released });

            var json = Lumeo.QueryBuilder.ToJson(query);

            // The JSON itself must be culture-invariant (ISO "yyyy-MM-dd"), not "12.07.2026".
            Assert.Contains("2026-07-12", json);

            var parsed = Lumeo.QueryBuilder.FromJson(json);
            var predicate = Lumeo.QueryBuilder.ToExpression<Widget>(parsed!)!.Compile();

            Assert.True(predicate(new Widget { Released = released }));
            Assert.False(predicate(new Widget { Released = released.AddDays(1) }));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Fractional_Number_Value_Round_Trips_Under_A_Non_Invariant_Culture()
    {
        // ReadNumber returns `decimal` for a fractional JSON number (to preserve precision for
        // decimal-typed fields, see Decimal_Value_Round_Trips_Without_Precision_Loss above).
        // When the TARGET property is `double`, ConvertValue used to format that decimal via
        // value.ToString() (culture-sensitive) before an InvariantCulture parse — under de-DE,
        // 1.5m.ToString() renders "1,5", which InvariantCulture then misreads as 15.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var query = Lumeo.QueryGroup.CreateEmpty();
            query.Rules.Add(new Lumeo.QueryRule { Field = "Score", Operator = "=", Value = 1.5 });

            var json = Lumeo.QueryBuilder.ToJson(query);
            var parsed = Lumeo.QueryBuilder.FromJson(json);
            var predicate = Lumeo.QueryBuilder.ToExpression<Widget>(parsed!)!.Compile();

            Assert.True(predicate(new Widget { Score = 1.5 }));
            Assert.False(predicate(new Widget { Score = 15 }));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Non_String_Combinator_Fails_The_Parse_Instead_Of_Throwing_Uncaught()
    {
        // reader.GetString() throws InvalidOperationException (not JsonException) for a
        // non-string token. FromJson only catches JsonException, so {"combinator":0,...} used
        // to bypass the documented return-null-on-parse-failure contract and let the exception
        // escape instead of treating the query as invalid.
        const string json = """{"combinator":0,"rules":[]}""";

        var parsed = Lumeo.QueryBuilder.FromJson(json);

        Assert.Null(parsed);
    }

    [Fact]
    public void Utc_DateTime_Value_Round_Trips_Without_A_Timezone_Shift()
    {
        // A programmatic QueryRule.Value of Kind.Utc serializes with an "O"-format string
        // carrying a "Z" suffix. Without DateTimeStyles.RoundtripKind, DateTime.Parse silently
        // converts that back to local time on reload, so the rebuilt predicate compares against
        // a shifted instant in any non-UTC timezone.
        var createdUtc = new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);
        var query = Lumeo.QueryGroup.CreateEmpty();
        query.Rules.Add(new Lumeo.QueryRule { Field = "CreatedUtc", Operator = "=", Value = createdUtc });

        var json = Lumeo.QueryBuilder.ToJson(query);
        var parsed = Lumeo.QueryBuilder.FromJson(json);
        var predicate = Lumeo.QueryBuilder.ToExpression<Widget>(parsed!)!.Compile();

        Assert.True(predicate(new Widget { CreatedUtc = createdUtc }));
        Assert.False(predicate(new Widget { CreatedUtc = createdUtc.AddHours(1) }));
    }

    [Fact]
    public void Custom_Non_Scalar_Value_Fails_The_Write_Instead_Of_Being_Silently_Stringified()
    {
        // A custom ValueEditorTemplate can box a non-scalar shape (e.g. string[]) into
        // QueryRule.Value. A ToString() fallback would corrupt the saved query — a string[]
        // becomes the literal text "System.String[]" with no way to recover the original data.
        // ToJson must fail loudly instead of persisting that unrecoverable value (#366 review).
        var query = Lumeo.QueryGroup.CreateEmpty();
        query.Rules.Add(new Lumeo.QueryRule { Field = "Name", Operator = "in", Value = new[] { "a", "b" } });

        var ex = Assert.Throws<JsonException>(() => Lumeo.QueryBuilder.ToJson(query));
        Assert.Contains("String[]", ex.Message);
    }

    [Fact]
    public void Oversized_Numeric_Literal_Fails_The_Parse_Instead_Of_Throwing_Uncaught()
    {
        // QueryNode is a polymorphic base ([JsonDerivedType] on QueryRule/QueryGroup), so any
        // JSON with an entry in "rules" needs the "$type" discriminator the real serializer
        // writes — hand-crafting the rule object without one fails on "must specify a type
        // discriminator" before FromJson ever reaches the numeric value. Serialize a genuine
        // QueryGroup first (which writes a correct, discriminated shape) and corrupt only the
        // numeric literal, so the test exercises the oversized-value path specifically rather
        // than an unrelated discriminator error.
        //
        // A numeric literal outside even Double's range (e.g. a hand-corrupted persisted
        // filter, "1e400") makes ReadNumber's fallback GetDouble() clamp to +Infinity instead
        // of throwing — Infinity isn't valid JSON and can never be written back out, so
        // ReadNumber now rejects it explicitly with a JsonException. FromJson only catches
        // JsonException and is documented to return null on parse failure (#366 review).
        var query = Lumeo.QueryGroup.CreateEmpty();
        query.Rules.Add(new Lumeo.QueryRule { Field = "Score", Operator = "=", Value = 42.0 });
        var validJson = Lumeo.QueryBuilder.ToJson(query);
        Assert.Contains("\"value\": 42", validJson);
        var json = validJson.Replace("\"value\": 42", "\"value\": 1e400");

        var parsed = Lumeo.QueryBuilder.FromJson(json);

        Assert.Null(parsed);
    }
}
