using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Timeline;

public class TimelineAnimatedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TimelineAnimatedTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Animated_False_Is_Default_And_Omits_Enter_Class()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .AddChildContent<Lumeo.TimelineItem>(item => item.Add(i => i.Title, "One"))
            .AddChildContent<Lumeo.TimelineItem>(item => item.Add(i => i.Title, "Two")));

        Assert.DoesNotContain("lumeo-timeline-enter", cut.Markup);
        Assert.DoesNotContain("lumeo-steps-pulse", cut.Markup);
    }

    [Fact]
    public void Animated_True_Vertical_Adds_Enter_Class()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Animated, true)
            .AddChildContent<Lumeo.TimelineItem>(item => item.Add(i => i.Title, "One"))
            .AddChildContent<Lumeo.TimelineItem>(item => item.Add(i => i.Title, "Two")));

        Assert.Contains("lumeo-timeline-enter-vertical", cut.Markup);
    }

    [Fact]
    public void Animated_True_Staggers_Animation_Delay()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Animated, true)
            .AddChildContent<Lumeo.TimelineItem>(item => item.Add(i => i.Title, "One"))
            .AddChildContent<Lumeo.TimelineItem>(item => item.Add(i => i.Title, "Two"))
            .AddChildContent<Lumeo.TimelineItem>(item => item.Add(i => i.Title, "Three")));

        // The first item has 0ms delay; subsequent items have 80ms, 160ms...
        Assert.Contains("animation-delay: 0ms", cut.Markup);
        Assert.Contains("animation-delay: 80ms", cut.Markup);
        Assert.Contains("animation-delay: 160ms", cut.Markup);
    }

    [Fact]
    public void Animated_True_Active_Item_Gets_Pulse_Class()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Animated, true)
            .AddChildContent<Lumeo.TimelineItem>(item =>
            {
                item.Add(i => i.Title, "Active");
                item.Add(i => i.IsActive, true);
            }));

        Assert.Contains("lumeo-steps-pulse", cut.Markup);
    }
}
