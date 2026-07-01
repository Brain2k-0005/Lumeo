using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Statistic;

/// <summary>
/// Bug 59 (keyboard-a11y): the directional trend icon must be aria-hidden (it is
/// decorative, mirroring Delta's arrows) and, when no visible TrendValue text is
/// present, the trend container must carry an accessible name so AT can announce
/// the direction. Without the fix the &lt;svg&gt; has no aria-hidden and the
/// trend container has no accessible name at all.
/// </summary>
public class StatisticTrendA11yTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StatisticTrendA11yTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement TrendContainer(IRenderedComponent<Lumeo.Statistic> cut) =>
        cut.Find("div.mt-1");

    [Fact]
    public void Trend_Up_Icon_Is_AriaHidden()
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Value, "100")
            .Add(s => s.ShowTrend, true)
            .Add(s => s.TrendDirection, Lumeo.Statistic.TrendType.Up));

        Assert.Equal("true", cut.Find("svg").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Trend_Down_Icon_Is_AriaHidden()
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Value, "100")
            .Add(s => s.ShowTrend, true)
            .Add(s => s.TrendDirection, Lumeo.Statistic.TrendType.Down));

        Assert.Equal("true", cut.Find("svg").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Trend_Without_Value_Gets_Direction_Label_Up()
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Value, "100")
            .Add(s => s.ShowTrend, true)
            .Add(s => s.TrendDirection, Lumeo.Statistic.TrendType.Up));

        Assert.Equal("Trending up", TrendContainer(cut).GetAttribute("aria-label"));
    }

    [Fact]
    public void Trend_Without_Value_Gets_Direction_Label_Down()
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Value, "100")
            .Add(s => s.ShowTrend, true)
            .Add(s => s.TrendDirection, Lumeo.Statistic.TrendType.Down));

        Assert.Equal("Trending down", TrendContainer(cut).GetAttribute("aria-label"));
    }

    [Fact]
    public void Trend_With_Value_Omits_AriaLabel_And_Keeps_Visible_Text()
    {
        // A visible TrendValue already names the container, so no aria-label is
        // emitted (it would otherwise mask the actual value text from AT).
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Value, "100")
            .Add(s => s.ShowTrend, true)
            .Add(s => s.TrendValue, "+12%")
            .Add(s => s.TrendDirection, Lumeo.Statistic.TrendType.Up));

        var container = TrendContainer(cut);
        Assert.False(container.HasAttribute("aria-label"));
        Assert.Contains("+12%", container.TextContent);
    }
}
