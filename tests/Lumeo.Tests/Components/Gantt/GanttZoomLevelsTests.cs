using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Gantt;

/// <summary>
/// Regression tests for the Gantt zoom toolbar API. The Day/Week/Month/Year buttons
/// used to be hardcoded with no way to render a subset or pick a non-default initial
/// level. <c>ZoomLevels</c> now restricts (and orders) the rendered buttons and
/// <c>DefaultZoom</c> seeds the initial selection.
/// </summary>
public class GanttZoomLevelsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public GanttZoomLevelsTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IReadOnlyList<string> ToggleLabels(IRenderedComponent<L.Gantt> cut) =>
        cut.FindAll("button[data-toggle-item='true']")
           .Select(b => b.TextContent.Trim())
           .ToList();

    [Fact]
    public void Default_Toolbar_Renders_All_Four_Levels()
    {
        var cut = _ctx.Render<L.Gantt>();
        Assert.Equal(new[] { "Day", "Week", "Month", "Year" }, ToggleLabels(cut));
    }

    [Fact]
    public void ZoomLevels_Renders_Only_Configured_Subset_In_Order()
    {
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.ZoomLevels, new[] { L.GanttViewMode.Month, L.GanttViewMode.Day, L.GanttViewMode.Week }));

        // Order preserved, Year absent.
        Assert.Equal(new[] { "Month", "Day", "Week" }, ToggleLabels(cut));
    }

    [Fact]
    public void ZoomLevels_Skips_NonToolbar_Levels()
    {
        // QuarterDay / HalfDay have no toolbar label; they are filtered out silently.
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.ZoomLevels, new[]
            {
                L.GanttViewMode.QuarterDay, L.GanttViewMode.Day, L.GanttViewMode.HalfDay, L.GanttViewMode.Week,
            }));

        Assert.Equal(new[] { "Day", "Week" }, ToggleLabels(cut));
    }

    [Fact]
    public void DefaultZoom_Seeds_Initial_Selection()
    {
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.ZoomLevels, new[] { L.GanttViewMode.Day, L.GanttViewMode.Week, L.GanttViewMode.Month })
            .Add(c => c.DefaultZoom, L.GanttViewMode.Week));

        // The Week toggle is the pressed one on first render (not Day, the ViewMode default).
        var pressed = cut.FindAll("button[data-toggle-item='true']")
            .Where(b => b.GetAttribute("aria-pressed") == "true")
            .Select(b => b.TextContent.Trim())
            .ToList();
        Assert.Equal(new[] { "Week" }, pressed);
    }

    [Fact]
    public void Empty_ZoomLevels_Falls_Back_To_Default_Set()
    {
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.ZoomLevels, Array.Empty<L.GanttViewMode>()));
        Assert.Equal(new[] { "Day", "Week", "Month", "Year" }, ToggleLabels(cut));
    }
}
