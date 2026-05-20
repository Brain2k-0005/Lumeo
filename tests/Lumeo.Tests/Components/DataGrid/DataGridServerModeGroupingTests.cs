using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// ServerMode used to support only single-level grouping via the static GroupBy
/// parameter. Multi-level (GroupByFields) and runtime UI grouping (drag-to-panel,
/// add/remove chip, clear-all) were silently ignored — every UI handler short-
/// circuited with `if (!ServerMode) ProcessClientData()`. These tests cover the
/// fix: a shared RegroupServerItems() path that runs after each Items refresh and
/// after each UI mutation, delegating to the same ProcessSingleLevelGrouping /
/// ProcessMultiLevelGrouping methods client-mode uses.
///
/// Per-page caveat: grouping runs over what the server returned. Cross-page
/// grouping is a consumer concern (deliver all rows or pre-aggregate) — these
/// tests use a single Items batch so the per-page constraint is irrelevant.
/// </summary>
public class DataGridServerModeGroupingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridServerModeGroupingTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Employee(int Id, string Name, string Department, string Status);

    private static List<Employee> GetEmployees() => new()
    {
        new(1, "Alice", "Engineering", "Active"),
        new(2, "Bob",   "Engineering", "Active"),
        new(3, "Carol", "Marketing",   "Active"),
        new(4, "Dan",   "Marketing",   "Inactive"),
        new(5, "Eve",   "HR",          "Active"),
    };

    private static List<DataGridColumn<Employee>> GetColumns() => new()
    {
        new() { Field = "Id",         Title = "ID" },
        new() { Field = "Name",       Title = "Name" },
        new() { Field = "Department", Title = "Department", Groupable = true },
        new() { Field = "Status",     Title = "Status",     Groupable = true },
    };

    private IRenderedComponent<DataGrid<Employee>> RenderServer(
        IReadOnlyList<string>? groupByFields = null,
        bool showGroupPanel = false)
    {
        var items = GetEmployees();
        return _ctx.Render<DataGrid<Employee>>(p =>
        {
            p.Add(x => x.ServerMode, true);
            p.Add(x => x.TotalCount, items.Count);
            p.Add(x => x.Items, items);
            p.Add(x => x.Columns, GetColumns());
            p.Add(x => x.ShowGroupPanel, showGroupPanel);
            if (groupByFields is not null)
                p.Add(x => x.GroupByFields, groupByFields);
        });
    }

    // ===========================================================================
    // Multi-level GroupByFields in ServerMode renders nested group rows
    // ===========================================================================

    [Fact]
    public void ServerMode_MultiLevelGroupByFields_RendersNestedGroupRows()
    {
        var cut = RenderServer(groupByFields: new[] { "Department", "Status" });

        var groupRows = cut.FindAll("[data-slot=\"datagrid-group-row\"]");
        // 3 departments (Engineering, HR, Marketing) + their status sub-groups.
        // Engineering: 1 Status group (Active)
        // HR:          1 Status group (Active)
        // Marketing:   2 Status groups (Active, Inactive)
        // Total = 3 + 4 = 7 group rows.
        Assert.Equal(7, groupRows.Count);
    }

    // ===========================================================================
    // Single-level GroupByFields in ServerMode (regression — already worked via
    // the GroupBy parameter, now exercises the unified path)
    // ===========================================================================

    [Fact]
    public void ServerMode_SingleLevelGroupByFields_RendersGroupRows()
    {
        var cut = RenderServer(groupByFields: new[] { "Department" });

        var groupRows = cut.FindAll("[data-slot=\"datagrid-group-row\"]");
        // Engineering, HR, Marketing = 3 groups.
        Assert.Equal(3, groupRows.Count);
    }

    // ===========================================================================
    // UI: AddGroupField via the panel select fires RegroupServerItems
    // ===========================================================================

    [Fact]
    public void ServerMode_AddGroupFieldViaPanel_RegroupsLive()
    {
        var cut = RenderServer(showGroupPanel: true);

        // No grouping yet → no group rows.
        Assert.Empty(cut.FindAll("[data-slot=\"datagrid-group-row\"]"));

        // Add Department via the panel <select>.
        var select = cut.Find("[data-slot=\"datagrid-group-panel\"] select");
        select.Change("Department");

        // Group rows must now appear (without re-fetching from the server).
        var groupRows = cut.FindAll("[data-slot=\"datagrid-group-row\"]");
        Assert.Equal(3, groupRows.Count);
    }

    // ===========================================================================
    // UI: AddGroupField twice in ServerMode builds a multi-level tree
    // ===========================================================================

    [Fact]
    public void ServerMode_AddSecondGroupFieldViaPanel_BuildsMultiLevelTree()
    {
        var cut = RenderServer(showGroupPanel: true);

        // Add Department, then Status — second add must promote to multi-level
        // (it would silently no-op on the old code path).
        cut.Find("[data-slot=\"datagrid-group-panel\"] select").Change("Department");
        cut.Find("[data-slot=\"datagrid-group-panel\"] select").Change("Status");

        var groupRows = cut.FindAll("[data-slot=\"datagrid-group-row\"]");
        Assert.Equal(7, groupRows.Count);
    }

    // ===========================================================================
    // UI: RemoveGroupField via chip button regroups live
    // ===========================================================================

    [Fact]
    public void ServerMode_RemoveGroupFieldViaChip_RegroupsLive()
    {
        var cut = RenderServer(
            showGroupPanel: true,
            groupByFields: new[] { "Department", "Status" });

        // Sanity: multi-level tree active.
        Assert.Equal(7, cut.FindAll("[data-slot=\"datagrid-group-row\"]").Count);

        // Click the first chip's Remove button — drops Department, leaves Status.
        var removeButtons = cut.FindAll("button[title=\"Remove grouping\"]");
        Assert.True(removeButtons.Count >= 1);
        removeButtons[0].Click();

        // Status alone = 2 sections (Active, Inactive). On the old code path this
        // would have stayed at 7 because the handler was a no-op in ServerMode.
        var groupRows = cut.FindAll("[data-slot=\"datagrid-group-row\"]");
        Assert.Equal(2, groupRows.Count);
    }

    // ===========================================================================
    // UI: Clear-all wipes grouping in ServerMode
    // ===========================================================================

    [Fact]
    public void ServerMode_ClearGroupFieldsViaButton_ClearsGrouping()
    {
        var cut = RenderServer(
            showGroupPanel: true,
            groupByFields: new[] { "Department", "Status" });

        Assert.NotEmpty(cut.FindAll("[data-slot=\"datagrid-group-row\"]"));

        // The clear-all button is title="Clear all grouping" — fires
        // ClearGroupFields, which now hits RegroupServerItems in ServerMode.
        var clearButton = cut.Find("button[title=\"Clear all grouping\"]");
        clearButton.Click();

        Assert.Empty(cut.FindAll("[data-slot=\"datagrid-group-row\"]"));
    }
}
