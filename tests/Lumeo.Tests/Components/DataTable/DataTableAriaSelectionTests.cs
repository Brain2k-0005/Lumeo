using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DataTable;

/// <summary>
/// Tests for the #318 DataTable fixes: ARIA grid/row/cell roles, ref-stable
/// selection binding (a fresh HashSet per change), and keyed virtualized rows.
/// </summary>
public class DataTableAriaSelectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataTableAriaSelectionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Person(string Name, int Age);

    private static readonly List<Person> Data = new()
    {
        new("Alice", 30),
        new("Bob", 25),
        new("Charlie", 35),
    };

    private IRenderedComponent<L.DataTable<Person>> RenderTable(
        bool selectable = false,
        HashSet<Person>? selected = null,
        EventCallback<HashSet<Person>> selectedChanged = default,
        bool virtualize = false)
    {
        return _ctx.Render<L.DataTable<Person>>(builder =>
        {
            builder.OpenComponent<L.DataTable<Person>>(0);
            builder.AddAttribute(1, "Items", Data);
            builder.AddAttribute(2, "Selectable", selectable);
            if (selected is not null) builder.AddAttribute(3, "SelectedItems", selected);
            builder.AddAttribute(4, "SelectedItemsChanged", selectedChanged);
            builder.AddAttribute(5, "Virtualize", virtualize);
            builder.AddAttribute(6, "HeaderTemplate", (RenderFragment)(h =>
            {
                h.OpenElement(0, "th"); h.AddContent(1, "Name"); h.CloseElement();
            }));
            builder.AddAttribute(7, "RowTemplate", (RenderFragment<Person>)(p => rb =>
            {
                rb.OpenElement(0, "td"); rb.AddContent(1, p.Name); rb.CloseElement();
            }));
            builder.CloseComponent();
        });
    }

    // --- ARIA roles ---

    [Fact]
    public void Table_Has_Grid_Role_And_RowCount()
    {
        var cut = RenderTable();
        var table = cut.Find("table");
        Assert.Equal("grid", table.GetAttribute("role"));
        // 3 data rows + 1 header row.
        Assert.Equal("4", table.GetAttribute("aria-rowcount"));
    }

    [Fact]
    public void Thead_And_Tbody_Are_RowGroups()
    {
        var cut = RenderTable();
        Assert.Equal("rowgroup", cut.Find("thead").GetAttribute("role"));
        Assert.Equal("rowgroup", cut.Find("tbody").GetAttribute("role"));
    }

    [Fact]
    public void Data_Rows_Have_Row_Role()
    {
        var cut = RenderTable();
        var bodyRows = cut.FindAll("tbody tr");
        Assert.All(bodyRows, r => Assert.Equal("row", r.GetAttribute("role")));
    }

    [Fact]
    public void Multiselectable_Set_Only_When_Selectable()
    {
        var plain = RenderTable(selectable: false);
        Assert.Null(plain.Find("table").GetAttribute("aria-multiselectable"));

        var selectable = RenderTable(selectable: true, selected: new HashSet<Person>());
        Assert.Equal("true", selectable.Find("table").GetAttribute("aria-multiselectable"));
    }

    [Fact]
    public void Selected_Row_Has_AriaSelected_True()
    {
        var selected = new HashSet<Person> { Data[0] };
        var cut = RenderTable(selectable: true, selected: selected);

        var rows = cut.FindAll("tbody tr");
        Assert.Equal("true", rows[0].GetAttribute("aria-selected"));
        Assert.Equal("false", rows[1].GetAttribute("aria-selected"));
    }

    [Fact]
    public void Selection_Cells_Have_GridCell_Role()
    {
        var cut = RenderTable(selectable: true, selected: new HashSet<Person>());
        // First cell in each body row is the selection checkbox cell.
        var firstCell = cut.Find("tbody tr td");
        Assert.Equal("gridcell", firstCell.GetAttribute("role"));
    }

    [Fact]
    public void AriaColCount_Forwarded_When_Set()
    {
        var cut = _ctx.Render<L.DataTable<Person>>(builder =>
        {
            builder.OpenComponent<L.DataTable<Person>>(0);
            builder.AddAttribute(1, "Items", Data);
            builder.AddAttribute(2, "AriaColCount", 4);
            builder.AddAttribute(3, "HeaderTemplate", (RenderFragment)(h => h.AddContent(0, "")));
            builder.AddAttribute(4, "RowTemplate", (RenderFragment<Person>)(_ => _ => { }));
            builder.CloseComponent();
        });
        Assert.Equal("4", cut.Find("table").GetAttribute("aria-colcount"));
    }

    // --- Ref-stable selection binding ---

    [Fact]
    public void Toggling_A_Row_Emits_A_New_HashSet_Instance()
    {
        var original = new HashSet<Person>();
        HashSet<Person>? emitted = null;
        var cb = EventCallback.Factory.Create<HashSet<Person>>(this, set => emitted = set);

        var cut = RenderTable(selectable: true, selected: original, selectedChanged: cb);

        // Click the first row's selection checkbox.
        var checkbox = cut.FindAll("tbody tr td [role=checkbox]").First();
        checkbox.Click();

        Assert.NotNull(emitted);
        Assert.NotSame(original, emitted);          // fresh reference (ref-stable contract)
        Assert.Contains(Data[0], emitted!);
        Assert.Empty(original);                     // input set was not mutated in place
    }

    [Fact]
    public void Select_All_Emits_New_Set_With_Every_Item()
    {
        var original = new HashSet<Person>();
        HashSet<Person>? emitted = null;
        var cb = EventCallback.Factory.Create<HashSet<Person>>(this, set => emitted = set);

        var cut = RenderTable(selectable: true, selected: original, selectedChanged: cb);

        // The header checkbox is the select-all toggle.
        var headerCheckbox = cut.Find("thead [role=checkbox]");
        headerCheckbox.Click();

        Assert.NotNull(emitted);
        Assert.NotSame(original, emitted);
        Assert.Equal(Data.Count, emitted!.Count);
    }

    // --- Keyed virtualization ---

    [Fact]
    public void Virtualized_Rows_Render_With_Role()
    {
        // bUnit renders the Virtualize initial batch synchronously. <Virtualize>
        // also emits two spacer <tr> elements (SpacerElement="tr") with no role —
        // so filter to the data rows (role="row") and assert all three rendered
        // via the keyed <tr> path.
        var cut = RenderTable(virtualize: true);
        var dataRows = cut.FindAll("tbody tr[role=row]");
        Assert.Equal(Data.Count, dataRows.Count);
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Charlie", cut.Markup);
    }
}
