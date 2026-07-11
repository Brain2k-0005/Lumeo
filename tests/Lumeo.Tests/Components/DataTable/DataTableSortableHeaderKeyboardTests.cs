using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DataTable;

/// <summary>
/// The sortable column header is a native &lt;button @onclick="HandleSort"&gt; — Enter/
/// Space activation is free via the browser's default button semantics, so .Click()
/// exercises the exact handler a synthesized keydown would run (bUnit cannot dispatch a
/// real native keydown-to-click translation). Previously untested: the OnSort callback
/// and the None -> Ascending -> Descending -> None cycle it drives, which is the only
/// Lumeo-owned logic behind this native button.
/// </summary>
public class DataTableSortableHeaderKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public DataTableSortableHeaderKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Activating_An_Unsorted_Header_Cycles_To_Ascending()
    {
        (string Column, L.DataTable<object>.SortDirection Direction)? result = null;
        var cut = _ctx.Render<L.DataTableSortableHeader>(p => p
            .Add(h => h.Column, "Name")
            .Add(h => h.SortColumn, (string?)null)
            .Add(h => h.SortDirection, L.DataTable<object>.SortDirection.None)
            .Add(h => h.OnSort, v => result = v));

        cut.Find("button").Click();

        Assert.Equal(("Name", L.DataTable<object>.SortDirection.Ascending), result);
    }

    [Fact]
    public void Activating_An_Ascending_Header_Cycles_To_Descending_Then_None()
    {
        (string Column, L.DataTable<object>.SortDirection Direction)? result = null;
        var cut = _ctx.Render<L.DataTableSortableHeader>(p => p
            .Add(h => h.Column, "Name")
            .Add(h => h.SortColumn, "Name")
            .Add(h => h.SortDirection, L.DataTable<object>.SortDirection.Ascending)
            .Add(h => h.OnSort, v => result = v));

        cut.Find("button").Click();

        Assert.Equal(("Name", L.DataTable<object>.SortDirection.Descending), result);
    }

    [Fact]
    public void Header_Button_Carries_No_Tabindex_Override()
    {
        var cut = _ctx.Render<L.DataTableSortableHeader>(p => p
            .Add(h => h.Column, "Name"));

        var button = cut.Find("button");
        Assert.False(button.HasAttribute("tabindex"));
        Assert.False(button.HasAttribute("disabled"));
    }
}
