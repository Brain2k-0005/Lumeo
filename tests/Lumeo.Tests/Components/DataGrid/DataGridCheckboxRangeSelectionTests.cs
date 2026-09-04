using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Field report §16.2, after #440: Shift-click on the selection CHECKBOX (not the row) selected
/// only the two clicked rows. The Checkbox's own click handler runs before the cell's, toggled
/// the row and re-anchored the range on it, so the cell's Shift handler filled a range of one.
/// The earlier tests clicked the row, which never went through that path. These click what a
/// browser clicks, in the order a browser fires it: pointerdown on the cell, the checkbox's
/// click, then the cell's click, both with Shift held.
/// </summary>
public class DataGridCheckboxRangeSelectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridCheckboxRangeSelectionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<Row> Rows(int n) => Enumerable.Range(0, n).Select(i => new Row(i, $"Row {i}")).ToList();

    private IRenderedComponent<Lumeo.DataGrid<Row>> Grid(List<Row> data, List<Row> selected, bool serverMode = false, bool selectOnRowClick = true)
    {
        var cols = new List<DataGridColumn<Row>> { new DataGridColumn<Row> { Field = "Name", Title = "Name" } };
        var sink = EventCallback.Factory.Create<IReadOnlyList<Row>>(this, v => { selected.Clear(); selected.AddRange(v); });
        return _ctx.Render<Lumeo.DataGrid<Row>>(p =>
        {
            p.Add(g => g.Items, data)
             .Add(g => g.Columns, cols)
             .Add(g => g.SelectionMode, DataGridSelectionMode.Multiple)
             .Add(g => g.SelectOnRowClick, selectOnRowClick)
             .Add(g => g.ShowPagination, false)
             .Add(g => g.ShowToolbar, false)
             .Add(g => g.SelectedItemsChanged, sink);
            if (serverMode)
            {
                // DocFlow's shape: ServerMode with OnServerRequest, the host loads the rows into
                // Items itself, no Virtualized / OnRangeRequest.
                p.Add(g => g.ServerMode, true)
                 .Add(g => g.TotalCount, data.Count)
                 .Add(g => g.OnServerRequest, EventCallback.Factory.Create<DataGridServerRequest>(this, _ => { }));
            }
        });
    }

    private static IReadOnlyList<AngleSharp.Dom.IElement> Checkboxes(IRenderedComponent<Lumeo.DataGrid<Row>> cut)
        => cut.FindAll("tbody[data-slot='datagrid-body'] tr [data-slot='checkbox-control']");

    /// <summary>A browser's Shift-click on the checkbox: pointerdown on the cell, then the click
    /// on the checkbox, which bubbles to the cell.</summary>
    private static void ShiftClickCheckbox(IRenderedComponent<Lumeo.DataGrid<Row>> cut, int row)
    {
        Checkboxes(cut)[row].PointerDown(new PointerEventArgs { ShiftKey = true });
        Checkboxes(cut)[row].Click(new MouseEventArgs { ShiftKey = true });
    }

    [Fact]
    public void Shift_Click_On_The_Checkbox_Selects_The_Range()
    {
        var selected = new List<Row>();
        var cut = Grid(Rows(10), selected);

        Checkboxes(cut)[0].PointerDown(new PointerEventArgs());
        Checkboxes(cut)[0].Click();
        Assert.Single(selected);

        // the reporter's test 1: row 1, then row 7 with Shift; they measured 2
        ShiftClickCheckbox(cut, 6);

        Assert.Equal(7, selected.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, selected.Select(r => r.Id).OrderBy(i => i));
    }

    [Fact]
    public void Shift_Click_On_The_Checkbox_Selects_The_Range_In_Server_Mode_Without_Virtualization()
    {
        var selected = new List<Row>();
        var cut = Grid(Rows(10), selected, serverMode: true, selectOnRowClick: false);

        Checkboxes(cut)[2].PointerDown(new PointerEventArgs());
        Checkboxes(cut)[2].Click();
        Assert.Single(selected);

        ShiftClickCheckbox(cut, 8);

        Assert.Equal(7, selected.Count);
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7, 8 }, selected.Select(r => r.Id).OrderBy(i => i));
    }

    [Fact]
    public void Shift_Click_On_The_Checkbox_Keeps_The_Anchor_For_The_Next_Extension()
    {
        var selected = new List<Row>();
        var cut = Grid(Rows(10), selected);

        Checkboxes(cut)[1].PointerDown(new PointerEventArgs());
        Checkboxes(cut)[1].Click();
        ShiftClickCheckbox(cut, 3);
        ShiftClickCheckbox(cut, 5);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, selected.Select(r => r.Id).OrderBy(i => i));
    }

    [Fact]
    public void A_Plain_Checkbox_Click_After_A_Shift_Pointer_That_Never_Clicked_Still_Toggles()
    {
        var selected = new List<Row>();
        var cut = Grid(Rows(10), selected);

        Checkboxes(cut)[0].Click();
        // a Shift pointerdown that ends elsewhere, then a plain click on another row
        Checkboxes(cut)[5].PointerDown(new PointerEventArgs { ShiftKey = true });
        Checkboxes(cut)[5].Click();

        // the stale Shift extends the range once; a plain click afterwards toggles again
        Checkboxes(cut)[5].PointerDown(new PointerEventArgs());
        Checkboxes(cut)[5].Click();
        Assert.DoesNotContain(selected, r => r.Id == 5);
    }

    [Fact]
    public void Shift_Click_On_The_Cell_Beside_The_Checkbox_Still_Selects_The_Range()
    {
        var selected = new List<Row>();
        var cut = Grid(Rows(10), selected);
        var cells = cut.FindAll("tbody[data-slot='datagrid-body'] tr td:first-child");

        Checkboxes(cut)[0].Click();
        cells[4].PointerDown(new PointerEventArgs { ShiftKey = true });
        cells[4].Click(new MouseEventArgs { ShiftKey = true });

        Assert.Equal(5, selected.Count);
    }

    [Fact]
    public void A_Shift_Click_Inside_The_Range_Leaves_The_Checkbox_Checked()
    {
        var selected = new List<Row>();
        var cut = Grid(Rows(10), selected);

        Checkboxes(cut)[0].Click();
        ShiftClickCheckbox(cut, 4);
        Assert.Equal(5, selected.Count);

        // a Shift-click on a row already inside the range changes nothing in the model; the
        // checkbox must not stay flipped by its own optimistic toggle
        ShiftClickCheckbox(cut, 2);
        Assert.Equal(5, selected.Count);
        Assert.Equal("true", Checkboxes(cut)[2].GetAttribute("aria-checked"));
    }
}
