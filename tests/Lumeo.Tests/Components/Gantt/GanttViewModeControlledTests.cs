using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Gantt;

/// <summary>
/// Regression for the toolbar-zoom revert bug: the view mode the user picks from
/// the toolbar must survive a parent re-render that carries an *unchanged*
/// <c>ViewMode</c> parameter. Before the fix, <c>OnParametersSetAsync</c> compared
/// the incoming <c>ViewMode</c> against the live <c>_currentViewMode</c>, so any
/// parent re-render silently reverted the toolbar selection back to the parameter.
///
/// The toolbar selection is reflected in the markup via the ToggleGroup: the
/// selected zoom button carries <c>aria-pressed="true"</c>, which is what these
/// tests assert against (the SVG renderer itself never runs in bUnit's headless
/// DOM — see <see cref="GanttInteropTests"/>).
/// </summary>
public class GanttViewModeControlledTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public GanttViewModeControlledTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static L.GanttTask Task1 =>
        new("t1", "Design", new DateTime(2026, 1, 1), new DateTime(2026, 1, 5), 20);

    /// <summary>The toggle button currently carrying aria-pressed="true", or null.</summary>
    private static string? PressedZoomLabel(IRenderedComponent<L.Gantt> cut) =>
        cut.FindAll("button[aria-pressed='true']")
           .Select(b => b.TextContent.Trim())
           .FirstOrDefault();

    [Fact]
    public void Toolbar_view_mode_survives_a_parent_rerender_with_unchanged_ViewMode()
    {
        // Uncontrolled usage: parent passes a constant ViewMode and never binds
        // ViewModeChanged — exactly how the docs examples use the component.
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Tasks, new[] { Task1 }));

        Assert.Equal("Day", PressedZoomLabel(cut));

        // User picks "Week" from the toolbar (the second zoom button). The
        // ToggleGroup raises ValueChanged -> Gantt.OnViewModeChangedAsync.
        var weekButton = cut.FindAll("button[aria-pressed]")
            .First(b => b.TextContent.Trim() == "Week");
        weekButton.Click();
        Assert.Equal("Week", PressedZoomLabel(cut));

        // The parent re-renders with the SAME ViewMode (still Day) but a changed
        // Tasks list — a perfectly ordinary re-render. Before the fix this reverted
        // the toolbar back to "Day"; the selection must now survive.
        var moved = Task1 with { Start = new DateTime(2026, 1, 2), End = new DateTime(2026, 1, 6) };
        cut.Render(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Tasks, new[] { moved }));

        Assert.Equal("Week", PressedZoomLabel(cut));
    }

    [Fact]
    public void Toolbar_selection_raises_ViewModeChanged_for_controlled_usage()
    {
        // Controlled usage: a bound parent must be notified so its ViewMode field
        // tracks the toolbar (i.e. @bind-ViewMode works).
        L.GanttViewMode? raised = null;
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ViewModeChanged, (L.GanttViewMode m) => { raised = m; })
            .Add(c => c.Tasks, new[] { Task1 }));

        var monthButton = cut.FindAll("button[aria-pressed]")
            .First(b => b.TextContent.Trim() == "Month");
        monthButton.Click();

        Assert.Equal(L.GanttViewMode.Month, raised);
        Assert.Equal("Month", PressedZoomLabel(cut));
    }

    [Fact]
    public void Genuine_ViewMode_parameter_change_still_takes_effect()
    {
        // The fix must not break the controlled path: when the parent really
        // changes ViewMode, the toolbar must follow.
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Tasks, new[] { Task1 }));

        Assert.Equal("Day", PressedZoomLabel(cut));

        cut.Render(p => p.Add(c => c.ViewMode, L.GanttViewMode.Year));

        Assert.Equal("Year", PressedZoomLabel(cut));
    }
}
