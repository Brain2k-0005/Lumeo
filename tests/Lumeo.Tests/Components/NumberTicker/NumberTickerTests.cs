using System.Globalization;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.NumberTicker;

public class NumberTickerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NumberTickerTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_As_Span_With_Tabular_Nums()
    {
        var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
            .Add(n => n.Value, 100));

        var span = cut.Find("span");
        Assert.Contains("tabular-nums", span.GetAttribute("class"));
    }

    [Fact]
    public void Initial_Renders_Start_Value_Not_Target()
    {
        var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
            .Add(n => n.Value, 100)
            .Add(n => n.StartValue, 0));

        // The inner ticker span (with id) starts at StartValue formatted
        Assert.Contains(">0<", cut.Markup);
    }

    [Fact]
    public void Prefix_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
            .Add(n => n.Value, 100)
            .Add(n => n.Prefix, "$"));

        Assert.Contains("$", cut.Markup);
    }

    [Fact]
    public void Suffix_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
            .Add(n => n.Value, 100)
            .Add(n => n.Suffix, "%"));

        Assert.Contains("%", cut.Markup);
    }

    [Fact]
    public void Prefix_Not_Rendered_When_Null()
    {
        var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
            .Add(n => n.Value, 100));

        // Only 2 spans: outer + the ticker value span. Prefix/suffix add extra spans.
        var spans = cut.FindAll("span");
        Assert.Equal(2, spans.Count);
    }

    [Fact]
    public void Decimals_Zero_Formats_Integer()
    {
        var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
            .Add(n => n.Value, 100)
            .Add(n => n.StartValue, 42)
            .Add(n => n.Decimals, 0));

        Assert.Contains(">42<", cut.Markup);
    }

    [Fact]
    public void Decimals_Two_Formats_With_Precision()
    {
        var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
            .Add(n => n.Value, 100)
            .Add(n => n.StartValue, 3.14)
            .Add(n => n.Decimals, 2));

        Assert.Contains("3.14", cut.Markup);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
            .Add(n => n.Value, 0)
            .Add(n => n.Class, "nt-x"));

        Assert.Contains("nt-x", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
            .Add(n => n.Value, 0)
            .Add(n => n.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "ticker"
            }));

        Assert.Equal("ticker", cut.Find("span").GetAttribute("data-testid"));
    }

    // --- Culture-aware formatting (#330) ---
    // The initial/final value must use CurrentCulture's grouping + decimal
    // separators rather than a hardcoded "," / "." pair, so the rendered number
    // matches the user's locale (and matches the JS count-up which is fed the
    // same separators).

    private static void WithCulture(string name, Action body)
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = prev;
        }
    }

    [Fact]
    public void Initial_Value_Uses_CurrentCulture_Group_Separator()
    {
        // de-DE groups thousands with "." → 1.234.567
        WithCulture("de-DE", () =>
        {
            var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
                .Add(n => n.Value, 2_000_000)
                .Add(n => n.StartValue, 1_234_567));

            Assert.Contains(">1.234.567<", cut.Markup);
        });
    }

    [Fact]
    public void Initial_Value_Uses_CurrentCulture_Decimal_Separator()
    {
        // de-DE uses "," as the decimal separator → 3,14
        WithCulture("de-DE", () =>
        {
            var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
                .Add(n => n.Value, 10)
                .Add(n => n.StartValue, 3.14)
                .Add(n => n.Decimals, 2));

            Assert.Contains(">3,14<", cut.Markup);
        });
    }

    [Fact]
    public void EnUs_Culture_Formats_With_Comma_Group_And_Dot_Decimal()
    {
        WithCulture("en-US", () =>
        {
            var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
                .Add(n => n.Value, 5_000)
                .Add(n => n.StartValue, 1_234.5)
                .Add(n => n.Decimals, 1));

            Assert.Contains(">1,234.5<", cut.Markup);
        });
    }

    [Fact]
    public void Explicit_ThousandsSeparator_Overrides_Culture_Grouping()
    {
        // Even in de-DE, an explicit ThousandsSeparator="," wins for grouping,
        // while the decimal separator still follows the culture (",").
        WithCulture("de-DE", () =>
        {
            var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
                .Add(n => n.Value, 9_999)
                .Add(n => n.StartValue, 1_234)
                .Add(n => n.ThousandsSeparator, ","));

            Assert.Contains(">1,234<", cut.Markup);
        });
    }

    [Fact]
    public void Empty_ThousandsSeparator_Suppresses_Grouping()
    {
        WithCulture("en-US", () =>
        {
            var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
                .Add(n => n.Value, 9_999)
                .Add(n => n.StartValue, 1_234)
                .Add(n => n.ThousandsSeparator, ""));

            Assert.Contains(">1234<", cut.Markup);
        });
    }

    // --- Wave 3 battle-test regressions ---

    // #50 (edge-data): a negative Decimals built a bogus "N-1" standard-format
    // string, so C# rendered the literal text "N-1" for ANY value (while the JS
    // count-up clamped to 0 decimals). Decimals must clamp to a non-negative count.
    [Fact]
    public void Negative_Decimals_Are_Clamped_To_Zero_Not_Rendered_As_Garbage()
    {
        WithCulture("en-US", () =>
        {
            var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
                .Add(n => n.Value, 9_999)
                .Add(n => n.StartValue, 1_234)
                .Add(n => n.Decimals, -1));

            // Before the fix the inner span rendered the literal "N-1".
            Assert.DoesNotContain("N-1", cut.Markup);
            Assert.Contains(">1,234<", cut.Markup);
        });
    }

    // #14 (edge-data): the JS formatter hardcodes uniform groups-of-3, but C#
    // FormatValue inherited the culture's NumberGroupSizes. Under an Indian-style
    // [3,2] grouping the initial/final C# render (12,34,567) diverged from the
    // animated JS values (1,234,567). C# must force uniform groups-of-3 so both
    // sides agree, honouring the documented "mirrors the JS formatter" invariant.
    [Fact]
    public void Indian_Group_Sizes_Render_As_Uniform_Groups_Of_Three()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // Explicit [3,2] grouping so the test is deterministic regardless of
            // the ICU data backing a named culture like en-IN.
            var indian = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            indian.NumberFormat.NumberGroupSizes = new[] { 3, 2 };
            indian.NumberFormat.NumberGroupSeparator = ",";
            indian.NumberFormat.NumberDecimalSeparator = ".";
            CultureInfo.CurrentCulture = indian;

            var cut = _ctx.Render<Lumeo.NumberTicker>(p => p
                .Add(n => n.Value, 2_000_000)
                .Add(n => n.StartValue, 1_234_567));

            // Before the fix this rendered the Indian grouping ">12,34,567<".
            Assert.Contains(">1,234,567<", cut.Markup);
            Assert.DoesNotContain("12,34,567", cut.Markup);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
