using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Steps;

/// <summary>
/// Motion-polish tests: when <see cref="Steps.Animated"/> is true and the
/// user goes Next → Previous, the connector into the newly-current step
/// should emit the reverse "unwind" animation class.
/// </summary>
public class StepsDirectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StepsDirectionTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Forward_Change_Uses_Draw_Class_Not_Unwind()
    {
        var cut = _ctx.Render<Lumeo.Steps>(p => p
            .Add(s => s.Animated, true)
            .Add(s => s.CurrentStep, 0)
            .AddChildContent<Lumeo.StepsItem>(i => i.Add(x => x.Title, "A"))
            .AddChildContent<Lumeo.StepsItem>(i => i.Add(x => x.Title, "B"))
            .AddChildContent<Lumeo.StepsItem>(i => i.Add(x => x.Title, "C")));

        cut.Render(p => p.Add(s => s.CurrentStep, 2));

        Assert.Contains("lumeo-steps-line-draw-x", cut.Markup);
        Assert.DoesNotContain("lumeo-steps-line-unwind-x", cut.Markup);
    }

    [Fact]
    public void Backward_Change_Emits_Unwind_Class_On_Current_Connector()
    {
        var cut = _ctx.Render<Lumeo.Steps>(p => p
            .Add(s => s.Animated, true)
            .Add(s => s.CurrentStep, 2)
            .AddChildContent<Lumeo.StepsItem>(i => i.Add(x => x.Title, "A"))
            .AddChildContent<Lumeo.StepsItem>(i => i.Add(x => x.Title, "B"))
            .AddChildContent<Lumeo.StepsItem>(i => i.Add(x => x.Title, "C")));

        // Going Previous from step 2 → step 1 should retract the connector
        // that leads OUT of the newly-current step.
        cut.Render(p => p.Add(s => s.CurrentStep, 1));

        Assert.Contains("lumeo-steps-line-unwind-x", cut.Markup);
    }

    [Fact]
    public void Backward_Vertical_Emits_Unwind_Y()
    {
        var cut = _ctx.Render<Lumeo.Steps>(p => p
            .Add(s => s.Animated, true)
            .Add(s => s.Orientation, Lumeo.Steps.StepsOrientation.Vertical)
            .Add(s => s.CurrentStep, 2)
            .AddChildContent<Lumeo.StepsItem>(i => i.Add(x => x.Title, "A"))
            .AddChildContent<Lumeo.StepsItem>(i => i.Add(x => x.Title, "B"))
            .AddChildContent<Lumeo.StepsItem>(i => i.Add(x => x.Title, "C")));

        cut.Render(p => p.Add(s => s.CurrentStep, 1));

        Assert.Contains("lumeo-steps-line-unwind-y", cut.Markup);
        Assert.DoesNotContain("lumeo-steps-line-unwind-x", cut.Markup);
    }

    [Fact]
    public void Backward_Change_Does_Not_Emit_Unwind_When_Not_Animated()
    {
        var cut = _ctx.Render<Lumeo.Steps>(p => p
            .Add(s => s.CurrentStep, 2)
            .AddChildContent<Lumeo.StepsItem>(i => i.Add(x => x.Title, "A"))
            .AddChildContent<Lumeo.StepsItem>(i => i.Add(x => x.Title, "B"))
            .AddChildContent<Lumeo.StepsItem>(i => i.Add(x => x.Title, "C")));

        cut.Render(p => p.Add(s => s.CurrentStep, 1));

        Assert.DoesNotContain("lumeo-steps-line-unwind", cut.Markup);
    }
}
