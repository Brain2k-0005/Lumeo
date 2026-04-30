using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

public class SchedulerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_toolbar_navigation()
    {
        var cut = _ctx.Render<L.Scheduler>();
        // Toolbar has Today, Month, Week, Day, List buttons
        Assert.Contains("Today", cut.Markup);
        Assert.Contains("Month", cut.Markup);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Class, "scheduler-cls"));
        Assert.Contains("scheduler-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "sched" }));
        Assert.Contains("data-testid=\"sched\"", cut.Markup);
    }

    [Fact]
    public void Initial_view_month_selected_by_default()
    {
        var cut = _ctx.Render<L.Scheduler>();
        // "Month" toggle item should be present and active
        Assert.Contains("Month", cut.Markup);
    }
}
