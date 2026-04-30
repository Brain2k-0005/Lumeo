using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Gantt;

public class GanttTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public GanttTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default()
    {
        var cut = _ctx.Render<L.Gantt>();
        // Root element contains a div with border classes
        Assert.Contains("lumeo-gantt-host", cut.Markup);
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.Gantt>(p => p.Add(c => c.Class, "my-gantt"));
        Assert.Contains("my-gantt", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "gantt" }));
        Assert.Contains("data-testid=\"gantt\"", cut.Markup);
    }

    [Fact]
    public void Renders_toolbar_with_view_buttons()
    {
        var cut = _ctx.Render<L.Gantt>();
        // Toolbar ToggleGroupItems render "Day", "Week", "Month", "Year" text
        Assert.Contains("Day", cut.Markup);
        Assert.Contains("Month", cut.Markup);
    }
}
