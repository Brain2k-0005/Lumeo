using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Statistic;

public class StatisticTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StatisticTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Title_And_Value()
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Title, "Revenue")
            .Add(s => s.Value, "1234"));

        Assert.Contains("Revenue", cut.Markup);
        Assert.Contains("1234", cut.Markup);
    }

    [Fact]
    public void Title_Uses_Muted_Foreground_Class()
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Title, "Users"));

        var cls = cut.Find("p").GetAttribute("class");
        Assert.Contains("text-muted-foreground", cls);
    }

    [Fact]
    public void ShowTrend_True_Renders_Trend_Section()
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Value, "42")
            .Add(s => s.ShowTrend, true)
            .Add(s => s.TrendValue, "+5%"));

        Assert.Contains("+5%", cut.Markup);
    }

    [Fact]
    public void Down_Trend_Has_Red_Class()
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Value, "42")
            .Add(s => s.ShowTrend, true)
            .Add(s => s.TrendDirection, Lumeo.Statistic.TrendType.Down)
            .Add(s => s.TrendValue, "-3%"));

        Assert.Contains("text-destructive", cut.Markup);
    }

    [Fact]
    public void Precision_Formats_Numeric_Value()
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Value, "3.14159")
            .Add(s => s.Precision, 2));

        Assert.Contains("3.14", cut.Markup);
    }
}
