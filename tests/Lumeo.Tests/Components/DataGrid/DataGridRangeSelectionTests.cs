using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// #440. SelectionMode's XML summary has promised "Ctrl/Shift-click and Ctrl+A" since the
/// parameter existed. Ctrl+A was implemented and Ctrl-click was already satisfied by the plain
/// per-row toggle Multiple mode does — but Shift-click did nothing at all, and that summary
/// ships in the package, so a consumer reads the promise at the call site while typing.
/// </summary>
public class DataGridRangeSelectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridRangeSelectionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private static List<Row> Rows(int n) =>
        Enumerable.Range(0, n).Select(i => new Row(i, $"Row {i}")).ToList();

    private IRenderedComponent<Lumeo.DataGrid<Row>> Grid(List<Row> data, List<Row> selected)
    {
        var cols = new List<DataGridColumn<Row>>
        {
            new DataGridColumn<Row> { Field = "Name", Title = "Name" },
        };

        // SelectedItems alone is one-way; the grid keeps its own list and reports through
        // SelectedItemsChanged, so that is what the assertions read.
        var sink = EventCallback.Factory.Create<IReadOnlyList<Row>>(this, v =>
        {
            selected.Clear();
            selected.AddRange(v);
        });

        return _ctx.Render<Lumeo.DataGrid<Row>>(p => p
            .Add(g => g.Items, data)
            .Add(g => g.Columns, cols)
            .Add(g => g.SelectionMode, DataGridSelectionMode.Multiple)
            .Add(g => g.SelectedItemsChanged, sink));
    }

    /// <summary>Body rows only — the header row has no data cells.</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> BodyRows(IRenderedComponent<Lumeo.DataGrid<Row>> cut) =>
        cut.FindAll("tbody[data-slot='datagrid-body'] tr");

    [Fact]
    public void Shift_Click_Selects_The_Inclusive_Range_From_The_Anchor()
    {
        var selected = new List<Row>();
        var cut = Grid(Rows(10), selected);

        // Plain click sets the anchor.
        BodyRows(cut)[0].Click();
        Assert.Single(selected);

        // Shift-click four rows down: 0..4 inclusive, which is five rows — the reporter's
        // case exactly (they measured two).
        BodyRows(cut)[4].Click(new MouseEventArgs { ShiftKey = true });

        Assert.Equal(5, selected.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, selected.Select(r => r.Id).OrderBy(i => i));
    }

    [Fact]
    public void Shift_Click_Works_Upwards_Too()
    {
        var selected = new List<Row>();
        var cut = Grid(Rows(10), selected);

        BodyRows(cut)[6].Click();
        BodyRows(cut)[2].Click(new MouseEventArgs { ShiftKey = true });

        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, selected.Select(r => r.Id).OrderBy(i => i));
    }

    [Fact]
    public void The_Anchor_Stays_Put_So_A_Range_Can_Be_Adjusted()
    {
        var selected = new List<Row>();
        var cut = Grid(Rows(10), selected);

        BodyRows(cut)[2].Click();
        BodyRows(cut)[7].Click(new MouseEventArgs { ShiftKey = true });
        Assert.Equal(6, selected.Count);

        // Second Shift-click shrinks the range rather than starting a new one from row 7.
        // The rows dropped from the range stay selected — extending is additive, because
        // destroying an existing checkbox selection on a mis-click is the worse failure.
        BodyRows(cut)[4].Click(new MouseEventArgs { ShiftKey = true });

        Assert.Contains(selected, r => r.Id == 3);
        Assert.Contains(selected, r => r.Id == 4);
    }

    [Fact]
    public void A_Plain_Click_Re_Anchors()
    {
        var selected = new List<Row>();
        var cut = Grid(Rows(10), selected);

        BodyRows(cut)[0].Click();
        BodyRows(cut)[5].Click();          // re-anchors here
        BodyRows(cut)[7].Click(new MouseEventArgs { ShiftKey = true });

        // 5..7 from the new anchor, plus row 0 from the first click.
        Assert.Equal(new[] { 0, 5, 6, 7 }, selected.Select(r => r.Id).OrderBy(i => i));
    }

    [Fact]
    public void Shift_Without_An_Anchor_Is_A_Plain_Toggle()
    {
        var selected = new List<Row>();
        var cut = Grid(Rows(10), selected);

        BodyRows(cut)[3].Click(new MouseEventArgs { ShiftKey = true });

        Assert.Single(selected);
        Assert.Equal(3, selected[0].Id);
    }
}
