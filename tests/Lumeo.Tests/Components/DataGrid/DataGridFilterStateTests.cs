using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// #317 — the column-filter popover must hydrate its editor from the column's
/// currently-applied filter so reopening it shows the active criteria instead of
/// a blank form (the header cell re-creates the popover on every open).
/// </summary>
public class DataGridFilterStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridFilterStateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private IRenderedComponent<DataGridColumnFilter<Row>> RenderFilter(
        DataGridColumn<Row> column, FilterDescriptor? current)
    {
        return _ctx.Render<DataGridColumnFilter<Row>>(b =>
        {
            b.OpenComponent<DataGridColumnFilter<Row>>(0);
            b.AddAttribute(1, "Column", column);
            b.AddAttribute(2, "CurrentFilter", current);
            b.AddAttribute(3, "OnApply", EventCallback.Factory.Create<FilterDescriptor?>(this, _ => { }));
            b.CloseComponent();
        });
    }

    [Fact]
    public void Text_Filter_Seeds_Value_From_Current_Filter()
    {
        var column = new DataGridColumn<Row> { Field = "Name", Title = "Name", FilterType = DataGridFilterType.Text };
        var current = new FilterDescriptor("Name", FilterOperator.StartsWith, "Al", null, DataGridFilterType.Text);

        var cut = RenderFilter(column, current);

        var input = cut.Find("input[type=text]");
        Assert.Equal("Al", input.GetAttribute("value"));
    }

    [Fact]
    public void Text_Filter_Empty_When_No_Current_Filter()
    {
        var column = new DataGridColumn<Row> { Field = "Name", Title = "Name", FilterType = DataGridFilterType.Text };

        var cut = RenderFilter(column, current: null);

        var input = cut.Find("input[type=text]");
        // No active filter → blank value (null or empty string attribute).
        Assert.True(string.IsNullOrEmpty(input.GetAttribute("value")));
    }

    [Fact]
    public void Number_Filter_Seeds_Value_From_Current_Filter()
    {
        var column = new DataGridColumn<Row> { Field = "Id", Title = "ID", FilterType = DataGridFilterType.Number };
        var current = new FilterDescriptor("Id", FilterOperator.GreaterThan, "10", null, DataGridFilterType.Number);

        var cut = RenderFilter(column, current);

        var input = cut.Find("input[type=number]");
        Assert.Equal("10", input.GetAttribute("value"));
    }

    [Fact]
    public void Boolean_Filter_Seeds_Checked_State()
    {
        var column = new DataGridColumn<Row> { Field = "Active", Title = "Active", FilterType = DataGridFilterType.Boolean };
        var current = new FilterDescriptor("Active", FilterOperator.Equals, true, null, DataGridFilterType.Boolean);

        var cut = RenderFilter(column, current);

        var checkbox = cut.Find("[role=checkbox]");
        Assert.Equal("true", checkbox.GetAttribute("aria-checked"));
    }
}
