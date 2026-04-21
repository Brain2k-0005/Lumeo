using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Steps;

public class StepsAnimatedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StepsAnimatedTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Animated_False_Is_Default_And_Omits_Pulse_Class()
    {
        var cut = _ctx.Render<Lumeo.Steps>(p => p
            .Add(s => s.CurrentStep, 1)
            .AddChildContent<Lumeo.StepsItem>(item => item.Add(i => i.Title, "One"))
            .AddChildContent<Lumeo.StepsItem>(item => item.Add(i => i.Title, "Two"))
            .AddChildContent<Lumeo.StepsItem>(item => item.Add(i => i.Title, "Three")));

        Assert.DoesNotContain("lumeo-steps-pulse", cut.Markup);
        Assert.DoesNotContain("lumeo-steps-line-draw", cut.Markup);
    }

    [Fact]
    public void Animated_True_Adds_Pulse_Class_To_Current_Indicator()
    {
        var cut = _ctx.Render<Lumeo.Steps>(p => p
            .Add(s => s.Animated, true)
            .Add(s => s.CurrentStep, 1)
            .AddChildContent<Lumeo.StepsItem>(item => item.Add(i => i.Title, "One"))
            .AddChildContent<Lumeo.StepsItem>(item => item.Add(i => i.Title, "Two"))
            .AddChildContent<Lumeo.StepsItem>(item => item.Add(i => i.Title, "Three")));

        // One pulse belongs to the Current step only
        Assert.Contains("lumeo-steps-pulse", cut.Markup);
    }

    [Fact]
    public void Animated_True_Adds_LineDraw_Class_To_Completed_Connectors()
    {
        var cut = _ctx.Render<Lumeo.Steps>(p => p
            .Add(s => s.Animated, true)
            .Add(s => s.CurrentStep, 2)
            .AddChildContent<Lumeo.StepsItem>(item => item.Add(i => i.Title, "A"))
            .AddChildContent<Lumeo.StepsItem>(item => item.Add(i => i.Title, "B"))
            .AddChildContent<Lumeo.StepsItem>(item => item.Add(i => i.Title, "C")));

        Assert.Contains("lumeo-steps-line-draw-x", cut.Markup);
    }

    [Fact]
    public void Animated_Vertical_Uses_Y_Line_Draw_Class()
    {
        var cut = _ctx.Render<Lumeo.Steps>(p => p
            .Add(s => s.Animated, true)
            .Add(s => s.Orientation, Lumeo.Steps.StepsOrientation.Vertical)
            .Add(s => s.CurrentStep, 1)
            .AddChildContent<Lumeo.StepsItem>(item => item.Add(i => i.Title, "A"))
            .AddChildContent<Lumeo.StepsItem>(item => item.Add(i => i.Title, "B")));

        Assert.Contains("lumeo-steps-line-draw-y", cut.Markup);
        Assert.DoesNotContain("lumeo-steps-line-draw-x", cut.Markup);
    }
}
