using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>The DataGrid items of the DocFlow field report that stayed open after 5.6.0:
/// SelectOnRowClick (1.8), ShowReorderHandle / ShowPinButton (1.11), DefaultSort (1.10),
/// SortIconTemplate (1.15), FooterTemplate (1.9), ColumnMenuContent (1.18) and a reactive
/// column Title (1.5).</summary>
public class DataGridFieldReport57Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridFieldReport57Tests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name, string City);

    private static List<Row> Data() => new()
    {
        new(1, "Alice", "Berlin"), new(2, "Bob", "Zagreb"), new(3, "Charlie", "Vienna"),
    };

    private static List<DataGridColumn<Row>> Columns(SortDirection defaultSort = SortDirection.None) => new()
    {
        new() { Field = "Name", Title = "Name", Sortable = true, Pinnable = true, Reorderable = true, Resizable = true, DefaultSort = defaultSort },
        new() { Field = "City", Title = "City", Sortable = true, Pinnable = true, Reorderable = true },
    };

    private IRenderedComponent<DataGrid<Row>> RenderGrid(Action<ComponentParameterCollectionBuilder<DataGrid<Row>>>? configure = null, SortDirection defaultSort = SortDirection.None)
        => _ctx.Render<DataGrid<Row>>(p =>
        {
            p.Add(x => x.Items, Data())
             .Add(x => x.Columns, Columns(defaultSort))
             .Add(x => x.Reorderable, true)
             .Add(x => x.ShowPagination, false)
             .Add(x => x.ShowToolbar, false);
            configure?.Invoke(p);
        });

    private static AngleSharp.Dom.IElement Header(IRenderedComponent<DataGrid<Row>> cut, int col)
        => cut.FindAll("th[data-slot=\"datagrid-header-cell\"]")[col];


    // ---------------------------------------------------------------- 1.8

    [Fact]
    public void Row_Click_Selects_By_Default()
    {
        var cut = RenderGrid(p => p.Add(x => x.SelectionMode, DataGridSelectionMode.Multiple));
        cut.FindAll("tr[data-slot=\"datagrid-row\"]")[0].Click();
        Assert.Equal("true", cut.FindAll("tr[data-slot=\"datagrid-row\"]")[0].GetAttribute("aria-selected"));
    }

    [Fact]
    public void SelectOnRowClick_False_Leaves_The_Row_Click_To_OnRowClick()
    {
        Row? clicked = null;
        var cut = RenderGrid(p => p
            .Add(x => x.SelectionMode, DataGridSelectionMode.Multiple)
            .Add(x => x.SelectOnRowClick, false)
            .Add(x => x.OnRowClick, EventCallback.Factory.Create<Row>(this, r => clicked = r)));

        cut.FindAll("tr[data-slot=\"datagrid-row\"]")[0].Click();

        Assert.Equal("false", cut.FindAll("tr[data-slot=\"datagrid-row\"]")[0].GetAttribute("aria-selected"));
        Assert.NotNull(clicked);
        Assert.Equal("Alice", clicked!.Name);
    }

    // ---------------------------------------------------------------- 1.11

    [Fact]
    public void Header_Controls_Render_By_Default()
    {
        var cut = RenderGrid();
        Assert.NotEmpty(cut.FindAll("[data-reorder-grip]"));
        Assert.NotEmpty(cut.FindAll("th button[aria-haspopup=\"menu\"]"));
    }

    [Fact]
    public void ShowReorderHandle_And_ShowPinButton_False_Keep_The_Header_Clean()
    {
        var cut = RenderGrid(p => p.Add(x => x.ShowReorderHandle, false).Add(x => x.ShowPinButton, false));

        Assert.Empty(cut.FindAll("[data-reorder-grip]"));
        Assert.Empty(cut.FindAll("th button[aria-haspopup=\"menu\"]"));
        // the header itself still arms a drag: reordering is not lost, only the grip
        Assert.Equal("true", Header(cut, 0).GetAttribute("data-reorderable"));
    }

    // ---------------------------------------------------------------- 1.10

    [Fact]
    public void DefaultSort_Sorts_At_Start_And_Shows_In_The_Header()
    {
        var cut = RenderGrid(defaultSort: SortDirection.Descending);

        Assert.Equal("descending", Header(cut, 0).GetAttribute("aria-sort"));
        var names = cut.FindAll("tr[data-slot=\"datagrid-row\"]").Select(r => r.QuerySelectorAll("td")[0].TextContent.Trim()).ToList();
        Assert.Equal(new[] { "Charlie", "Bob", "Alice" }, names);
    }

    [Fact]
    public void DefaultSort_Rides_On_The_First_Server_Request()
    {
        var requests = new List<DataGridServerRequest>();
        _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Columns, Columns(SortDirection.Descending))
            .Add(x => x.ServerMode, true)
            .Add(x => x.ShowPagination, false)
            .Add(x => x.ShowToolbar, false)
            .Add(x => x.OnServerRequest, EventCallback.Factory.Create<DataGridServerRequest>(this, r => requests.Add(r))));

        Assert.NotEmpty(requests);
        var first = requests[0];
        Assert.NotNull(first.Sorts);
        Assert.Contains(first.Sorts!, s => s.Field == "Name" && s.Direction == SortDirection.Descending);
    }

    [Fact]
    public void A_Declared_Column_DefaultSort_Reaches_The_Server()
    {
        var requests = new List<DataGridServerRequest>();
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.ServerMode, true)
            .Add(x => x.ShowPagination, false)
            .Add(x => x.ShowToolbar, false)
            .Add(x => x.OnServerRequest, EventCallback.Factory.Create<DataGridServerRequest>(this, r => requests.Add(r)))
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(d => d.Field, "Name").Add(d => d.Title, "Name").Add(d => d.Sortable, true).Add(d => d.DefaultSort, SortDirection.Descending)));

        // the child registers after the initial request went out, so a second request carries the sort
        cut.WaitForAssertion(() => Assert.Contains(requests, r => r.Sorts?.Any(s => s.Field == "Name" && s.Direction == SortDirection.Descending) == true));
        Assert.Equal("descending", Header(cut, 0).GetAttribute("aria-sort"));
    }

    [Fact]
    public void A_Saved_Layout_Wins_Over_DefaultSort()
    {
        var saved = new DataGridLayout { Sorts = new List<SortDescriptor> { new("City", SortDirection.Ascending) } };
        var cut = RenderGrid(p => p.Add(x => x.SavedLayout, saved), defaultSort: SortDirection.Descending);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("ascending", Header(cut, 1).GetAttribute("aria-sort"));
            Assert.Equal("none", Header(cut, 0).GetAttribute("aria-sort"));
        });
    }

    // ---------------------------------------------------------------- 1.15

    [Fact]
    public void SortIconTemplate_Replaces_The_Glyphs()
    {
        var cut = RenderGrid(p => p.Add(x => x.SortIconTemplate, (SortDirection d) => (RenderFragment)(b =>
        {
            b.OpenElement(0, "i");
            b.AddAttribute(1, "data-sort", d.ToString());
            b.CloseElement();
        })), defaultSort: SortDirection.Ascending);

        Assert.NotNull(Header(cut, 0).QuerySelector("i[data-sort=\"Ascending\"]"));
        Assert.NotNull(Header(cut, 1).QuerySelector("i[data-sort=\"None\"]"));
        Assert.Empty(cut.FindAll("th [data-slot=\"datagrid-sort-button\"] svg"));
    }

    // ---------------------------------------------------------------- 1.9

    [Fact]
    public void FooterTemplate_Renders_The_Row_Count_Without_Aggregates()
    {
        var cut = RenderGrid(p => p.Add(x => x.FooterTemplate, (DataGridFooterContext<Row> f) => (RenderFragment)(b =>
            b.AddContent(0, $"{f.RowCount} of {f.TotalCount} rows, first {f.Items[0].Name}"))));

        var footer = cut.Find("[data-slot=\"datagrid-footer\"]");
        Assert.Equal("3 of 3 rows, first Alice", footer.TextContent.Trim());
        Assert.Empty(cut.FindAll("[data-slot=\"datagrid-aggregate-strip\"]"));
    }

    // ---------------------------------------------------------------- 1.18

    [Fact]
    public void ColumnMenuContent_Is_Appended_After_A_Separator()
    {
        var cut = RenderGrid(p => p
            .Add(x => x.ColumnMenu, true)
            .Add(x => x.ColumnMenuContent, (DataGridColumn<Row> c) => (RenderFragment)(b =>
            {
                b.OpenElement(0, "button");
                b.AddAttribute(1, "type", "button");
                b.AddAttribute(2, "role", "menuitem");
                b.AddAttribute(3, "data-test", "toggle-" + c.Field);
                b.AddContent(4, "Show customer number");
                b.CloseElement();
            })));

        Header(cut, 0).QuerySelector("[data-slot=\"datagrid-sort-button\"]")!.Click();

        var menu = cut.Find("[role=\"menu\"]");
        var extra = menu.QuerySelector("[data-test=\"toggle-Name\"]");
        Assert.NotNull(extra);
        Assert.Equal(4, menu.QuerySelectorAll("[role=\"separator\"]").Length);
        // the host entry is the last thing in the menu
        Assert.Same(extra, menu.QuerySelectorAll("[role=\"menuitem\"], [role=\"menuitemradio\"]").Last());
    }

    // ---------------------------------------------------------------- 1.5

    [Fact]
    public void A_Changed_Title_Reaches_The_Header()
    {
        var cut = _ctx.Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.ShowPagination, false)
            .Add(x => x.ShowToolbar, false)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(d => d.Field, "Name").Add(d => d.Title, "Name")));
        Assert.Contains("Name", Header(cut, 0).TextContent);

        cut.Render(p => p
            .Add(x => x.Items, Data())
            .Add(x => x.ShowPagination, false)
            .Add(x => x.ShowToolbar, false)
            .AddChildContent<DataGridColumnDef<Row>>(c => c.Add(d => d.Field, "Name").Add(d => d.Title, "Naziv")));

        cut.WaitForAssertion(() => Assert.Contains("Naziv", Header(cut, 0).TextContent));
        Assert.DoesNotContain("Name", Header(cut, 0).QuerySelector("span.truncate")!.TextContent);
    }
}
