using Lumeo.GanttV3;
using Xunit;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Regression tests for <see cref="GanttState"/> — the hoistable, Blazor-free
/// Gantt v3 store (design spec "Public API" &gt; Additive &gt; <c>GanttState</c>,
/// the REUI <c>useGanttState</c> analog). Focus: every mutator raises
/// <see cref="GanttState.Changed"/> exactly when the value genuinely changes, and
/// never on a no-op re-application of the current value — the discipline a
/// hoisted, externally-shared store needs so a rendering nav bar and timeline
/// don't re-render on every parent pass just because it re-applied its own
/// current props.
/// </summary>
public class GanttStateTests
{
    private static Lumeo.GanttTask Task(string id, string? parentId = null, string[]? dependencies = null) =>
        new(id, id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 5), Dependencies: dependencies) { ParentId = parentId };

    [Fact]
    public void Defaults_Are_Empty_Tasks_Day_ViewMode_And_No_Collapsed_Rows()
    {
        var state = new GanttState();

        Assert.Empty(state.Tasks);
        Assert.Equal(GanttViewMode.Day, state.ViewMode);
        Assert.Empty(state.Collapsed);
        Assert.False(state.IsCollapsed("anything"));
    }

    [Fact]
    public void SetTasks_Replaces_Tasks_And_Raises_Changed_Once()
    {
        var state = new GanttState();
        var raised = 0;
        state.Changed += () => raised++;

        state.SetTasks(new[] { Task("t1") });

        Assert.Equal(1, raised);
        Assert.Equal("t1", Assert.Single(state.Tasks).Id);
    }

    [Fact]
    public void SetTasks_With_A_Value_Equal_Sequence_Does_Not_Raise_Changed()
    {
        var state = new GanttState();
        state.SetTasks(new[] { Task("t1") });

        var raised = 0;
        state.Changed += () => raised++;

        // A NEW array instance, but value-equal task records — must be a no-op.
        state.SetTasks(new[] { Task("t1") });

        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetTasks_Detects_A_ParentId_Only_Change_As_Genuinely_Different()
    {
        var state = new GanttState();
        state.SetTasks(new[] { Task("t1") });

        var raised = 0;
        state.Changed += () => raised++;

        // Same Id/Name/dates — only ParentId moved. Must still be detected (records
        // fold every declared property, including body-declared ParentId, into
        // Equals/GetHashCode).
        state.SetTasks(new[] { Task("t1", parentId: "p1") });

        Assert.Equal(1, raised);
        Assert.Equal("p1", Assert.Single(state.Tasks).ParentId);
    }

    [Fact]
    public void SetTasks_With_Fresh_But_Content_Equal_Dependencies_Arrays_Does_Not_Raise_Changed()
    {
        // Bug fix (CodeRabbit review): GanttTask.Dependencies is string[]?,
        // which the record's own Equals compares by REFERENCE — a caller that
        // re-materializes its Tasks list every render (a common shape) would
        // pass a brand-new Dependencies array instance each time even when its
        // CONTENT is unchanged, spuriously raising Changed forever.
        var state = new GanttState();
        state.SetTasks(new[] { Task("t1", dependencies: new[] { "a", "b" }) });

        var raised = 0;
        state.Changed += () => raised++;

        // A NEW array instance, same elements in the same order.
        state.SetTasks(new[] { Task("t1", dependencies: new[] { "a", "b" }) });

        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetTasks_With_A_Genuinely_Different_Dependencies_Sequence_Raises_Changed()
    {
        var state = new GanttState();
        state.SetTasks(new[] { Task("t1", dependencies: new[] { "a", "b" }) });

        var raised = 0;
        state.Changed += () => raised++;

        state.SetTasks(new[] { Task("t1", dependencies: new[] { "a", "c" } ) });

        Assert.Equal(1, raised);
        Assert.Equal(new[] { "a", "c" }, Assert.Single(state.Tasks).Dependencies);
    }

    [Fact]
    public void SetViewMode_Raises_Changed_Only_On_A_Genuine_Change()
    {
        var state = new GanttState();
        var raised = 0;
        state.Changed += () => raised++;

        state.SetViewMode(GanttViewMode.Day); // same as default -> no-op
        Assert.Equal(0, raised);

        state.SetViewMode(GanttViewMode.Week);
        Assert.Equal(1, raised);
        Assert.Equal(GanttViewMode.Week, state.ViewMode);

        state.SetViewMode(GanttViewMode.Week); // re-applying current value -> no-op
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetVisibleRange_Raises_Changed_Only_On_A_Genuine_Change()
    {
        var state = new GanttState();
        var raised = 0;
        state.Changed += () => raised++;

        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        state.SetVisibleRange(start, end);
        Assert.Equal(1, raised);
        Assert.Equal(new GanttDateRange(start, end), state.VisibleRange);

        state.SetVisibleRange(start, end); // same window -> no-op
        Assert.Equal(1, raised);

        state.SetVisibleRange(start, end.AddMonths(1)); // genuinely wider -> raises
        Assert.Equal(2, raised);
    }

    [Fact]
    public void SetCollapsed_Raises_Changed_Only_On_A_Genuine_Change()
    {
        var state = new GanttState();
        var raised = 0;
        state.Changed += () => raised++;

        state.SetCollapsed("group-a", collapsed: false); // already expanded -> no-op
        Assert.Equal(0, raised);

        state.SetCollapsed("group-a", collapsed: true);
        Assert.Equal(1, raised);
        Assert.True(state.IsCollapsed("group-a"));
        Assert.Contains("group-a", state.Collapsed);

        state.SetCollapsed("group-a", collapsed: true); // already collapsed -> no-op
        Assert.Equal(1, raised);

        state.SetCollapsed("group-a", collapsed: false);
        Assert.Equal(2, raised);
        Assert.False(state.IsCollapsed("group-a"));
    }

    [Fact]
    public void ToggleCollapsed_Flips_State_And_Always_Raises_Changed()
    {
        var state = new GanttState();
        var raised = 0;
        state.Changed += () => raised++;

        state.ToggleCollapsed("group-a");
        Assert.Equal(1, raised);
        Assert.True(state.IsCollapsed("group-a"));

        state.ToggleCollapsed("group-a");
        Assert.Equal(2, raised);
        Assert.False(state.IsCollapsed("group-a"));
    }
}
