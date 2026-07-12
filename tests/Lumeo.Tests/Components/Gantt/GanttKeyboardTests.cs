using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Gantt;

/// <summary>
/// Wave 4 composition audit — Gantt's own DOM is a toolbar of already-tested
/// Lumeo &lt;ToggleGroup&gt;/&lt;ToggleGroupItem&gt; zoom buttons; the task-bar
/// canvas is opaque JS (frappe-gantt), out of bUnit's reach. ToggleGroup's own
/// arrow-key/Enter/Space handling is covered by its own suite (Radix roving
/// tabindex). GanttViewModeControlledTests/GanttZoomLevelsTests already cover
/// the toolbar's rendered aria-pressed state; this file fills the one
/// remaining neededTests gap — that activating a zoom button both updates
/// Gantt's own ViewMode AND fires ViewModeChanged with that value (the
/// Blazor-level wiring behind the ToggleGroup selection, not new key-handling
/// code).
/// </summary>
public class GanttKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public GanttKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Selecting_A_Zoom_Button_Fires_ViewModeChanged_With_The_Picked_Value()
    {
        L.GanttViewMode? pushed = null;
        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ViewModeChanged, (L.GanttViewMode v) => pushed = v));

        cut.FindAll("button[data-toggle-item='true']")
            .First(b => b.TextContent.Trim() == "Month")
            .Click();

        Assert.Equal(L.GanttViewMode.Month, pushed);
    }

    [Fact]
    public void Uncontrolled_Zoom_Button_Selection_Updates_The_Toolbar_Without_A_Binding()
    {
        // No ViewModeChanged bound — Gantt still tracks the toolbar selection
        // internally (uncontrolled usage), reflected via aria-pressed.
        var cut = _ctx.Render<L.Gantt>(p => p.Add(c => c.ViewMode, L.GanttViewMode.Day));

        cut.FindAll("button[data-toggle-item='true']")
            .First(b => b.TextContent.Trim() == "Year")
            .Click();

        var pressed = cut.FindAll("button[data-toggle-item='true']")
            .Single(b => b.GetAttribute("aria-pressed") == "true");
        Assert.Equal("Year", pressed.TextContent.Trim());
    }
}
