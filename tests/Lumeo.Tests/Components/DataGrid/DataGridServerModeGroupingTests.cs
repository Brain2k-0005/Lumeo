using Bunit;
using Microsoft.AspNetCore.Components;
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
/// grouping is a consumer concern (deliver all rows or pre-aggregate). Most tests
/// below use a single Items batch so the per-page constraint is irrelevant; the
/// "SurvivesAPageRefresh" tests specifically swap Items across multiple renders to
/// exercise that constraint (production bug: expand/collapse state used to be
/// wiped on every page/sort/filter/search refresh — see those tests for detail).
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

        // 2.2.0: add Department via the panel DropdownMenu (was <select>).
        cut.Find("[data-slot=\"datagrid-group-add-trigger\"]").Click();
        cut.Find("[role=\"menu\"] [data-group-add-field=\"Department\"]").Click();

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

        // 2.2.0: Add Department, then Status via the DropdownMenu — second
        // add must promote to multi-level (was silently no-op on the old
        // ServerMode short-circuit path that v2.1.0 already fixed).
        cut.Find("[data-slot=\"datagrid-group-add-trigger\"]").Click();
        cut.Find("[role=\"menu\"] [data-group-add-field=\"Department\"]").Click();
        cut.Find("[data-slot=\"datagrid-group-add-trigger\"]").Click();
        cut.Find("[role=\"menu\"] [data-group-add-field=\"Status\"]").Click();

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
        var removeButtons = cut.FindAll("button[aria-label=\"Remove grouping\"]");
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
        var clearButton = cut.Find("button[aria-label=\"Clear all grouping\"]");
        clearButton.Click();

        Assert.Empty(cut.FindAll("[data-slot=\"datagrid-group-row\"]"));
    }

    // ===========================================================================
    // Regression: group expand/collapse toggles regroup in ServerMode
    // (ToggleGroupExpand / ToggleGroupPath called ProcessClientData
    // unconditionally, which early-returns without client Items — clicking a
    // group row silently did nothing on every ServerMode grid.)
    // ===========================================================================

    [Fact]
    public void ServerMode_GroupRowClick_Collapses_SingleLevel_Group()
    {
        var items = GetEmployees();
        var cut = _ctx.Render<DataGrid<Employee>>(p =>
        {
            p.Add(x => x.ServerMode, true);
            p.Add(x => x.TotalCount, items.Count);
            p.Add(x => x.Items, items);
            p.Add(x => x.Columns, GetColumns());
            p.Add(x => x.GroupByFields, (IReadOnlyList<string>)new[] { "Department" });
            p.Add(x => x.GroupsExpandedByDefault, true);
        });

        Assert.Contains("Alice", cut.Markup); // Engineering rows visible

        // Collapse the first (Engineering) group via the group-row click.
        cut.Find("[data-slot=\"datagrid-group-row\"]").Click();

        Assert.DoesNotContain("Alice", cut.Markup);
        Assert.DoesNotContain("Bob", cut.Markup);
        Assert.Contains("Carol", cut.Markup); // other groups untouched
    }

    // ===========================================================================
    // Production bug (post-4.0.0): a collapsed group's state must survive a real
    // page/sort/filter/search refresh — not just the single static Items batch every
    // other test in this file uses. RegroupServerItems() used to IntersectWith the
    // CURRENT page's group keys against _expandedGroups/_knownGroupKeys on every
    // refresh; a new server page's keys are almost never IDENTICAL to the previous
    // page's, so the intersection wiped out nearly all tracked state and every "new"
    // key (i.e. every key on the new page) was silently re-seeded from
    // GroupsExpandedByDefault — auto-expanding groups and making the user's manual
    // collapse choice (and the rows they were looking at) vanish.
    // ===========================================================================

    [Fact]
    public void ServerMode_CollapsedGroup_SurvivesAPageRefresh_WithDifferentGroupKeys()
    {
        var page1 = new List<Employee>
        {
            new(1, "Alice", "Engineering", "Active"),
            new(2, "Bob",   "Engineering", "Active"),
            new(3, "Carol", "Marketing",   "Active"),
        };
        var cut = _ctx.Render<DataGrid<Employee>>(p =>
        {
            p.Add(x => x.ServerMode, true);
            p.Add(x => x.TotalCount, 5);
            p.Add(x => x.Items, page1);
            p.Add(x => x.Columns, GetColumns());
            p.Add(x => x.GroupByFields, (IReadOnlyList<string>)new[] { "Department" });
            p.Add(x => x.GroupsExpandedByDefault, true);
        });

        // Collapse the alphabetically-first group (Engineering).
        cut.Find("[data-slot=\"datagrid-group-row\"]").Click();
        Assert.DoesNotContain("Alice", cut.Markup);

        // Page 2: a genuinely different server page. Engineering doesn't even appear
        // here (it's a different page of rows) — only Marketing and a new HR group.
        var page2 = new List<Employee>
        {
            new(4, "Dan", "Marketing", "Inactive"),
            new(5, "Eve", "HR",        "Active"),
        };
        cut.Render(p => p.Add(x => x.Items, page2));

        // Back to page 1 (e.g. the user pages back, or re-sorts and lands on the same
        // rows again): Engineering reappears. Its collapse must have survived — it
        // must NOT have been silently re-expanded by the intervening page-2 refresh.
        cut.Render(p => p.Add(x => x.Items, page1));

        Assert.DoesNotContain("Alice", cut.Markup);
        Assert.DoesNotContain("Bob", cut.Markup);
        Assert.Contains("Carol", cut.Markup); // Marketing was never collapsed
    }

    [Fact]
    public void ServerMode_CollapsedMultiLevelPath_SurvivesAPageRefresh_WithDifferentGroupKeys()
    {
        var page1 = new List<Employee>
        {
            new(1, "Alice", "Engineering", "Active"),
            new(2, "Bob",   "Engineering", "Active"),
            new(3, "Carol", "Marketing",   "Active"),
        };
        var cut = _ctx.Render<DataGrid<Employee>>(p =>
        {
            p.Add(x => x.ServerMode, true);
            p.Add(x => x.TotalCount, 5);
            p.Add(x => x.Items, page1);
            p.Add(x => x.Columns, GetColumns());
            p.Add(x => x.GroupByFields, (IReadOnlyList<string>)new[] { "Department", "Status" });
            p.Add(x => x.GroupsExpandedByDefault, true);
        });

        cut.Find("[data-slot=\"datagrid-group-row\"]").Click(); // collapse Engineering path
        Assert.DoesNotContain("Alice", cut.Markup);

        var page2 = new List<Employee>
        {
            new(4, "Dan", "Marketing", "Inactive"),
            new(5, "Eve", "HR",        "Active"),
        };
        cut.Render(p => p.Add(x => x.Items, page2));
        cut.Render(p => p.Add(x => x.Items, page1));

        Assert.DoesNotContain("Alice", cut.Markup);
        Assert.DoesNotContain("Bob", cut.Markup);
        Assert.Contains("Carol", cut.Markup);
    }

    [Fact]
    public async Task ServerMode_SubsequentServerRequest_CarriesTheCurrentRuntimeGroupField_NotAStaleGroupByParameter()
    {
        // Production bug: RequestServerData sent the static GroupBy [Parameter] to
        // OnServerRequest instead of _runtimeGroupFields — a consumer grouping via the
        // panel (AddGroupField) or GroupByFields never saw their real grouping choice in
        // the server callback and couldn't pre-sort/pre-group server-side to cooperate.
        DataGridServerRequest? received = null;
        var items = GetEmployees();
        var cut = _ctx.Render<DataGrid<Employee>>(p =>
        {
            p.Add(x => x.ServerMode, true);
            p.Add(x => x.TotalCount, items.Count);
            p.Add(x => x.Items, items);
            p.Add(x => x.Columns, GetColumns());
            p.Add(x => x.PageSize, 2); // 5 items / page size 2 = 3 pages, so "Next" is enabled
            p.Add(x => x.ShowGroupPanel, true);
            p.Add(x => x.OnServerRequest, EventCallback.Factory.Create<DataGridServerRequest>(_ctx, (DataGridServerRequest r) => received = r));
        });

        // Add Department grouping via the panel — a local, no-network regroup (does not
        // itself invoke OnServerRequest). Awaited via InvokeAsync so the runtime field is
        // fully committed before the next interaction reads it.
        await cut.InvokeAsync(() => cut.Find("[data-slot=\"datagrid-group-add-trigger\"]").Click());
        await cut.InvokeAsync(() => cut.Find("[role=\"menu\"] [data-group-add-field=\"Department\"]").Click());

        // Trigger a genuine server round-trip (paging) — its request must carry the
        // runtime grouping the user just picked, not the absent/stale static GroupBy.
        await cut.InvokeAsync(() => cut.Find("button[aria-label=\"Next\"]").Click());

        Assert.NotNull(received);
        Assert.Equal("Department", received!.GroupBy);
    }

    [Fact]
    public void ServerMode_GroupRowClick_Collapses_MultiLevel_Path()
    {
        var items = GetEmployees();
        var cut = _ctx.Render<DataGrid<Employee>>(p =>
        {
            p.Add(x => x.ServerMode, true);
            p.Add(x => x.TotalCount, items.Count);
            p.Add(x => x.Items, items);
            p.Add(x => x.Columns, GetColumns());
            p.Add(x => x.GroupByFields, (IReadOnlyList<string>)new[] { "Department", "Status" });
            p.Add(x => x.GroupsExpandedByDefault, true);
        });

        var groupRowsBefore = cut.FindAll("[data-slot=\"datagrid-group-row\"]").Count;
        Assert.Contains("Alice", cut.Markup);

        // Collapse the first (Engineering) top-level path node — its child
        // group rows and leaf rows must disappear.
        cut.Find("[data-slot=\"datagrid-group-row\"]").Click();

        Assert.DoesNotContain("Alice", cut.Markup);
        Assert.True(cut.FindAll("[data-slot=\"datagrid-group-row\"]").Count < groupRowsBefore);
    }
}
