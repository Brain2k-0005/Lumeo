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

    [Fact]
    public void Accepts_task_with_dependencies_without_exception()
    {
        // frappe-gantt / the custom SVG renderer won't run in bUnit's headless DOM,
        // but the component should render its host div without throwing.
        var tasks = new List<L.GanttTask>
        {
            new("t1", "Task 1", DateTime.Today, DateTime.Today.AddDays(3)),
            new("t2", "Task 2", DateTime.Today.AddDays(3), DateTime.Today.AddDays(6),
                Dependencies: new[] { "t1" }),
        };
        var exception = Record.Exception(() => _ctx.Render<L.Gantt>(p =>
            p.Add(c => c.Tasks, tasks)));
        Assert.Null(exception);
    }

    [Fact]
    public void Accepts_milestone_task_without_exception()
    {
        // IsMilestone=true should not throw; end is clamped to start in ToJsTask.
        var tasks = new List<L.GanttTask>
        {
            new("m1", "Kickoff", DateTime.Today, DateTime.Today, IsMilestone: true),
            new("t1", "Follow-up", DateTime.Today.AddDays(1), DateTime.Today.AddDays(5)),
        };
        var exception = Record.Exception(() => _ctx.Render<L.Gantt>(p =>
            p.Add(c => c.Tasks, tasks)));
        Assert.Null(exception);
    }

    [Fact]
    public void All_view_mode_enum_values_accepted_without_exception()
    {
        // Verify every GanttViewMode value can be passed without a parse/cast error.
        foreach (var mode in Enum.GetValues<L.GanttViewMode>())
        {
            var exception = Record.Exception(() => _ctx.Render<L.Gantt>(p =>
                p.Add(c => c.ViewMode, mode)));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void GroupBy_parameter_accepted_without_exception()
    {
        var tasks = new List<L.GanttTask>
        {
            new("t1", "Design", DateTime.Today, DateTime.Today.AddDays(5), GroupLabel: "Phase 1"),
            new("t2", "Dev",    DateTime.Today.AddDays(5), DateTime.Today.AddDays(15), GroupLabel: "Phase 2"),
        };
        var exception = Record.Exception(() => _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.GroupBy, (L.GanttTask t) => t.GroupLabel ?? "")));
        Assert.Null(exception);
    }
}
