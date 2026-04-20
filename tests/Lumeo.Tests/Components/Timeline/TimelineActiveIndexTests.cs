using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Timeline;

/// <summary>
/// Tests for the <see cref="Timeline.ActiveIndex"/> "active dot travels"
/// pattern: items before the index render as completed (bg-primary), the
/// item at the index pulses, items after are pending (border-muted).
/// </summary>
public class TimelineActiveIndexTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TimelineActiveIndexTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void ActiveIndex_Active_Item_Gets_Pulse_Class()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Animated, true)
            .Add(t => t.ActiveIndex, 1)
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "A"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "B"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "C")));

        Assert.Contains("lumeo-steps-pulse", cut.Markup);
    }

    [Fact]
    public void ActiveIndex_Earlier_Items_Render_As_Completed()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Animated, true)
            .Add(t => t.ActiveIndex, 2)
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "A"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "B"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "C")));

        // Completed state paints the dot with bg-primary.
        Assert.Contains("bg-primary text-primary-foreground", cut.Markup);
    }

    [Fact]
    public void ActiveIndex_Pending_Items_Render_Muted()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Animated, true)
            .Add(t => t.ActiveIndex, 0)
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "A"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "B"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "C")));

        // Pending items use the muted border.
        Assert.Contains("border-muted", cut.Markup);
    }

    [Fact]
    public void ActiveIndex_Completed_Connectors_Use_Primary_Color()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Animated, true)
            .Add(t => t.ActiveIndex, 2)
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "A"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "B"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "C")));

        // At least one connector must be coloured primary (the ones up to index 2).
        Assert.Contains("bg-primary lumeo-steps-line-draw", cut.Markup);
    }

    [Fact]
    public void ActiveIndex_Advances_Replays_Connector_Animation()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Animated, true)
            .Add(t => t.ActiveIndex, 0)
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "A"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "B"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "C")));

        cut.Render(p => p.Add(t => t.ActiveIndex, 1));

        // Forward draw on the connector leading into index 1.
        Assert.Contains("lumeo-steps-line-draw", cut.Markup);
    }

    [Fact]
    public void Legacy_IsActive_Still_Works_When_ActiveIndex_Is_Null()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Animated, true)
            .AddChildContent<Lumeo.TimelineItem>(i =>
            {
                i.Add(x => x.Title, "A");
                i.Add(x => x.IsActive, true);
            }));

        Assert.Contains("lumeo-steps-pulse", cut.Markup);
    }
}
