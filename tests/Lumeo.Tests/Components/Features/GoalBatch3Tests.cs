using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Features;

/// <summary>
/// Goal batch 3 — composable value/label slots so animated counts (e.g. a
/// NumberTicker from Lumeo.Motion) drop in without duplicating animation in core:
///   #273 Statistic.ValueContent · #277 Gauge.LabelContent.
/// </summary>
public class GoalBatch3Tests
{
    private static BunitContext NewCtx()
    {
        var ctx = new BunitContext();
        ctx.AddLumeoServices();
        return ctx;
    }

    [Fact]
    public void Statistic_value_content_overrides_value()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Statistic>(p => p
            .Add(x => x.Value, "999")
            .Add(x => x.ValueContent, b => b.AddContent(0, "CUSTOM")));

        Assert.Contains("CUSTOM", cut.Markup);
        Assert.DoesNotContain("999", cut.Markup);
    }

    [Fact]
    public void Statistic_falls_back_to_value()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Statistic>(p => p.Add(x => x.Value, "42"));
        Assert.Contains("42", cut.Markup);
    }

    [Fact]
    public void Gauge_label_content_overrides_default()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Gauge>(p => p
            .Add(x => x.Value, 50)
            .Add(x => x.LabelContent, b => b.AddContent(0, "TICK")));

        Assert.Contains("TICK", cut.Markup);
    }

    [Fact]
    public void Gauge_default_shows_percentage_label()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Gauge>(p => p.Add(x => x.Value, 50));
        Assert.Contains("50%", cut.Markup);
    }
}
