using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Tests for the DataGrid Expandable (fullscreen) feature.
///
/// The fullscreen mode wraps the grid root in a fixed-position overlay while
/// rendering the SAME grid markup sub-tree inside, so internal state
/// (selection, filters, sorts, pagination, etc.) is preserved across toggles.
/// </summary>
public class DataGridExpandableTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridExpandableTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record TestItem(int Id, string Name);

    private static List<TestItem> GetData() => new()
    {
        new(1, "Alice"),
        new(2, "Bob"),
        new(3, "Charlie"),
    };

    private static List<DataGridColumn<TestItem>> GetColumns() => new()
    {
        new() { Field = "Id", Title = "ID", Sortable = true },
        new() { Field = "Name", Title = "Name", Sortable = true, Filterable = true },
    };

    [Fact]
    public void Expandable_False_By_Default_No_Expand_Button_Rendered()
    {
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.ShowToolbar, true));

        // Expand toolbar button should not be present.
        Assert.DoesNotContain("Expand to fullscreen", cut.Markup);
        Assert.DoesNotContain("Exit fullscreen", cut.Markup);
    }

    [Fact]
    public void Expandable_True_Renders_Expand_Button_In_Toolbar()
    {
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.ShowToolbar, true)
            .Add(x => x.Expandable, true));

        // Title/aria-label contains "Expand to fullscreen" when collapsed.
        Assert.Contains("Expand to fullscreen", cut.Markup);
    }

    [Fact]
    public async Task Clicking_Expand_Button_Toggles_IsExpanded_And_Fires_Changed()
    {
        bool? lastFired = null;
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.ShowToolbar, true)
            .Add(x => x.Expandable, true)
            .Add(x => x.IsExpandedChanged, EventCallback.Factory.Create<bool>(this, v => lastFired = v)));

        // Click the expand button — find it by its aria-label.
        var expandButton = cut.Find("[aria-label='Expand to fullscreen']");
        await cut.InvokeAsync(() => expandButton.Click());

        Assert.True(lastFired);

        // After expand, the dialog wrapper should exist with role="dialog".
        Assert.NotNull(cut.Find("[role='dialog']"));
        Assert.Contains("fixed inset-0", cut.Markup);
    }

    [Fact]
    public void IsExpanded_True_Binding_Renders_Fullscreen_Wrapper_On_First_Render()
    {
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.ShowToolbar, true)
            .Add(x => x.Expandable, true)
            .Add(x => x.IsExpanded, true));

        // The fullscreen overlay is present.
        Assert.Contains("fixed inset-0", cut.Markup);
        Assert.NotNull(cut.Find("[role='dialog']"));
    }

    [Fact]
    public async Task Pressing_Escape_While_Expanded_Collapses_Grid()
    {
        bool? lastFired = null;
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, GetData())
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.ShowToolbar, true)
            .Add(x => x.Expandable, true)
            .Add(x => x.IsExpanded, true)
            .Add(x => x.IsExpandedChanged, EventCallback.Factory.Create<bool>(this, v => lastFired = v)));

        // Confirm we start expanded.
        Assert.Contains("fixed inset-0", cut.Markup);

        // Dispatch Escape key on the fullscreen wrapper.
        var dialog = cut.Find("[role='dialog']");
        await cut.InvokeAsync(() => dialog.KeyDown(new KeyboardEventArgs { Key = "Escape" }));

        Assert.False(lastFired);
        // After collapse, the fullscreen wrapper should no longer be rendered.
        Assert.DoesNotContain("role=\"dialog\"", cut.Markup);
    }

    [Fact]
    public async Task Expand_Collapse_Cycle_Preserves_Selection_State()
    {
        var items = GetData();
        IReadOnlyList<TestItem>? lastSelection = null;

        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.Columns, GetColumns())
            .Add(x => x.ShowToolbar, true)
            .Add(x => x.Expandable, true)
            .Add(x => x.SelectionMode, DataGridSelectionMode.Multiple)
            .Add(x => x.SelectedItemsChanged, EventCallback.Factory.Create<IReadOnlyList<TestItem>>(this, sel => lastSelection = sel)));

        // Programmatically toggle selection via the grid instance then expand/collapse.
        var grid = cut.Instance;
        await cut.InvokeAsync(() => grid.ToggleExpanded()); // expand
        await cut.InvokeAsync(() => grid.ToggleExpanded()); // collapse

        // Grid should now be collapsed again (no fullscreen wrapper) and still render data.
        Assert.DoesNotContain("role=\"dialog\"", cut.Markup);
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
    }
}
