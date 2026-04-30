using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tour;

public class TourTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TourTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_nothing_when_closed()
    {
        var steps = new List<L.Tour.TourStepConfig>
        {
            new(null, "Welcome", "This is step 1")
        };
        var cut = _ctx.Render<L.Tour>(p => p
            .Add(c => c.Steps, steps)
            .Add(c => c.Open, false));
        // No overlay or tooltip when closed
        Assert.Empty(cut.FindAll("[id^='tour-tooltip-']"));
    }

    [Fact]
    public void Renders_tooltip_when_open()
    {
        var steps = new List<L.Tour.TourStepConfig>
        {
            new(null, "Welcome", "This is step 1")
        };
        var cut = _ctx.Render<L.Tour>(p => p
            .Add(c => c.Steps, steps)
            .Add(c => c.Open, true));
        Assert.Contains("Welcome", cut.Markup);
    }

    [Fact]
    public void Merges_class_parameter_on_tooltip()
    {
        var steps = new List<L.Tour.TourStepConfig>
        {
            new(null, "Step 1", "Description")
        };
        var cut = _ctx.Render<L.Tour>(p => p
            .Add(c => c.Steps, steps)
            .Add(c => c.Open, true)
            .Add(c => c.Class, "tour-cls"));
        Assert.Contains("tour-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes_on_tooltip()
    {
        var steps = new List<L.Tour.TourStepConfig>
        {
            new(null, "Step 1", "Description")
        };
        var cut = _ctx.Render<L.Tour>(p => p
            .Add(c => c.Steps, steps)
            .Add(c => c.Open, true)
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "tour" }));
        Assert.Contains("data-testid=\"tour\"", cut.Markup);
    }

    [Fact]
    public void Shows_step_counter()
    {
        var steps = new List<L.Tour.TourStepConfig>
        {
            new(null, "Step One", "First"),
            new(null, "Step Two", "Second")
        };
        var cut = _ctx.Render<L.Tour>(p => p
            .Add(c => c.Steps, steps)
            .Add(c => c.Open, true)
            .Add(c => c.CurrentStep, 0));
        // Should show "1 / 2"
        Assert.Contains("1 / 2", cut.Markup);
    }
}
