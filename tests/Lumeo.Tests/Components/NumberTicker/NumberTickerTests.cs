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
}
