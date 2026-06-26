using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for group expand/collapse state PERSISTENCE across data
/// refreshes and grid operations.
///
/// Bug: a user-collapsed single-level group silently RE-EXPANDED whenever the
/// bound <c>Items</c> momentarily became empty (an async refresh / loading cycle
/// that lots-of-data apps do constantly). ProcessSingleLevelGrouping ran
/// <c>_expandedGroups.IntersectWith(validKeys)</c> / <c>_knownGroupKeys.IntersectWith(...)</c>
/// against an EMPTY validKeys set, wiping the collapse memory — so when data
/// returned every key looked "new" and was re-expanded by GroupsExpandedByDefault.
/// ProcessMultiLevelGrouping never had this bug because it early-returns when the
/// node tree is empty; the fix gives the single-level path the same guard.
/// </summary>
public class DataGridGroupCollapsePersistenceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DataGridGroupCollapsePersistenceTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Emp(int Id, string Name, string Dept);

    private static List<Emp> Make(int n) =>
        Enumerable.Range(1, n)
            .Select(i => new Emp(i, $"Name{i}", i % 2 == 0 ? "Engineering" : "Marketing"))
            .ToList();

    private static List<DataGridColumn<Emp>> Cols() => new()
    {
        new() { Field = "Id", Title = "ID" },
        new() { Field = "Name", Title = "Name" },
        new() { Field = "Dept", Title = "Department", Groupable = true },
    };

    private static bool GroupCollapsedByLabel(IRenderedComponent<DataGrid<Emp>> cut, string label) =>
        cut.FindAll("[data-slot=\"datagrid-group-row\"]")
            .First(r => r.TextContent.Contains(label))
            .InnerHtml.Contains("-rotate-90"); // collapsed chevron

    private static bool FirstGroupCollapsed(IRenderedComponent<DataGrid<Emp>> cut) =>
        cut.FindAll("[data-slot=\"datagrid-group-row\"]")[0].InnerHtml.Contains("-rotate-90");

    // ===========================================================================
    // The actual bug: collapse, then Items momentarily becomes empty (loading
    // cycle), then data returns. Collapse MUST survive.
    // ===========================================================================
    [Fact]
    public void Collapse_Survives_Empty_Items_Flicker()
    {
        var cut = _ctx.Render<DataGrid<Emp>>(p => p
            .Add(x => x.Items, Make(30))
            .Add(x => x.Columns, Cols())
            .Add(x => x.GroupBy, "Dept")
            .Add(x => x.GroupsExpandedByDefault, true)
            .Add(x => x.ShowPagination, false));

        cut.FindAll("[data-slot=\"datagrid-group-row\"]")
            .First(r => r.TextContent.Contains("Engineering")).Click();
        Assert.True(GroupCollapsedByLabel(cut, "Engineering"));

        // Items -> empty (loading), then -> back to data.
        cut.Render(p => p.Add(x => x.Items, new List<Emp>()));
        cut.Render(p => p.Add(x => x.Items, Make(30)));

        Assert.True(GroupCollapsedByLabel(cut, "Engineering"),
            "Collapsed group re-expanded after an empty-Items loading flicker.");
    }

    // ===========================================================================
    // Persistence across ordinary operations that must NOT lose collapse.
    // ===========================================================================

    [Fact]
    public void Collapse_Survives_ItemsRefresh_SameContent()
    {
        var cut = _ctx.Render<DataGrid<Emp>>(p => p
            .Add(x => x.Items, Make(30))
            .Add(x => x.Columns, Cols())
            .Add(x => x.GroupBy, "Dept")
            .Add(x => x.GroupsExpandedByDefault, true)
            .Add(x => x.ShowPagination, false));

        cut.FindAll("[data-slot=\"datagrid-group-row\"]")
            .First(r => r.TextContent.Contains("Engineering")).Click();
        Assert.True(GroupCollapsedByLabel(cut, "Engineering"));

        cut.Render(p => p.Add(x => x.Items, Make(30))); // fresh list instance, same content

        Assert.True(GroupCollapsedByLabel(cut, "Engineering"),
            "Collapse lost after Items refresh with identical content.");
    }

    [Fact]
    public void Collapse_Survives_LotsOfData_Refresh()
    {
        var cut = _ctx.Render<DataGrid<Emp>>(p => p
            .Add(x => x.Items, Make(1200))
            .Add(x => x.Columns, Cols())
            .Add(x => x.GroupBy, "Dept")
            .Add(x => x.GroupsExpandedByDefault, true)
            .Add(x => x.ShowPagination, false));

        cut.FindAll("[data-slot=\"datagrid-group-row\"]")[0].Click();
        Assert.True(FirstGroupCollapsed(cut));

        cut.Render(p => p.Add(x => x.Items, Make(1200)));

        Assert.True(FirstGroupCollapsed(cut),
            "Collapse lost after Items refresh with lots of data.");
    }

    [Fact]
    public void Collapse_Survives_Sort()
    {
        var cut = _ctx.Render<DataGrid<Emp>>(p => p
            .Add(x => x.Items, Make(40))
            .Add(x => x.Columns, Cols())
            .Add(x => x.GroupBy, "Dept")
            .Add(x => x.GroupsExpandedByDefault, true)
            .Add(x => x.ShowPagination, false));

        cut.FindAll("[data-slot=\"datagrid-group-row\"]")[0].Click();
        Assert.True(FirstGroupCollapsed(cut));

        var nameHeader = cut.FindAll("th").FirstOrDefault(h => h.TextContent.Contains("Name"));
        nameHeader?.QuerySelector("button")?.Click();

        Assert.True(FirstGroupCollapsed(cut), "Collapse lost after sort.");
    }

    [Fact]
    public void Collapse_Survives_When_NewGroupKey_Appears()
    {
        var cut = _ctx.Render<DataGrid<Emp>>(p => p
            .Add(x => x.Items, Make(20))
            .Add(x => x.Columns, Cols())
            .Add(x => x.GroupBy, "Dept")
            .Add(x => x.GroupsExpandedByDefault, true)
            .Add(x => x.ShowPagination, false));

        cut.FindAll("[data-slot=\"datagrid-group-row\"]")
            .First(r => r.TextContent.Contains("Engineering")).Click();
        Assert.True(GroupCollapsedByLabel(cut, "Engineering"));

        // New data introduces a new Dept ("Design") alongside the originals.
        var data2 = Make(20).Append(new Emp(999, "Zed", "Design")).ToList();
        cut.Render(p => p.Add(x => x.Items, data2));

        Assert.True(GroupCollapsedByLabel(cut, "Engineering"),
            "Collapse lost after a new group key appeared.");
    }

    [Fact]
    public void Collapse_Survives_Declarative_Columns_Refresh()
    {
        var cut = _ctx.Render<DataGrid<Emp>>(p => p
            .Add(x => x.Items, Make(30))
            .Add(x => x.GroupBy, "Dept")
            .Add(x => x.GroupsExpandedByDefault, true)
            .Add(x => x.ShowPagination, false)
            .AddChildContent<DataGridColumnDef<Emp>>(c => c
                .Add(x => x.Field, "Id").Add(x => x.Title, "ID"))
            .AddChildContent<DataGridColumnDef<Emp>>(c => c
                .Add(x => x.Field, "Name").Add(x => x.Title, "Name"))
            .AddChildContent<DataGridColumnDef<Emp>>(c => c
                .Add(x => x.Field, "Dept").Add(x => x.Title, "Department").Add(x => x.Groupable, true)));

        cut.FindAll("[data-slot=\"datagrid-group-row\"]")
            .First(r => r.TextContent.Contains("Engineering")).Click();
        Assert.True(GroupCollapsedByLabel(cut, "Engineering"));

        cut.Render(p => p.Add(x => x.Items, Make(30)));

        Assert.True(GroupCollapsedByLabel(cut, "Engineering"),
            "Collapse lost after Items refresh with declarative columns.");
    }

    // ===========================================================================
    // Multi-level (path-based) grouping never regressed — keep it covered.
    // ===========================================================================

    private record Emp3(int Id, string Name, string Region, string Country);

    private static List<Emp3> Make3(int n) =>
        Enumerable.Range(1, n).Select(i => new Emp3(
            i, $"N{i}",
            i % 2 == 0 ? "EMEA" : "AMER",
            i % 3 == 0 ? "DE" : "UK")).ToList();

    private static List<DataGridColumn<Emp3>> Cols3() => new()
    {
        new() { Field = "Id", Title = "ID" },
        new() { Field = "Name", Title = "Name" },
        new() { Field = "Region", Title = "Region", Groupable = true },
        new() { Field = "Country", Title = "Country", Groupable = true },
    };

    [Fact]
    public void MultiLevel_Collapse_Survives_Empty_Items_Flicker()
    {
        var cut = _ctx.Render<DataGrid<Emp3>>(p => p
            .Add(x => x.Items, Make3(60))
            .Add(x => x.Columns, Cols3())
            .Add(x => x.GroupByFields, (IReadOnlyList<string>)new[] { "Region", "Country" })
            .Add(x => x.GroupsExpandedByDefault, true)
            .Add(x => x.ShowPagination, false));

        var before = cut.FindAll("[data-slot=\"datagrid-group-row\"]").Count;
        cut.FindAll("[data-slot=\"datagrid-group-row\"]").First(r => r.TextContent.Contains("AMER")).Click();
        var collapsedCount = cut.FindAll("[data-slot=\"datagrid-group-row\"]").Count;
        Assert.True(collapsedCount < before);

        cut.Render(p => p.Add(x => x.Items, new List<Emp3>()));
        cut.Render(p => p.Add(x => x.Items, Make3(60)));

        Assert.Equal(collapsedCount, cut.FindAll("[data-slot=\"datagrid-group-row\"]").Count);
    }
}
