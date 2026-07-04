using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Regression tests for the faceted (FilterType=Select + FilterOptions) filter
/// popover. It used to render a redundant operator dropdown (contains/equals…)
/// AND a Select-all/Clear helper row that read as a second Apply/Clear. Facet mode
/// now renders ONLY the checkbox list + one shared Apply/Clear row, while the
/// Text/Number/Date modes keep their operator picker + single Apply/Clear.
/// </summary>
public class DataGridFilterFacetTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridFilterFacetTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name);

    private IRenderedComponent<DataGridColumnFilter<Row>> RenderFilter(
        DataGridColumn<Row> column, List<FilterOption>? options)
    {
        return _ctx.Render<DataGridColumnFilter<Row>>(b =>
        {
            b.OpenComponent<DataGridColumnFilter<Row>>(0);
            b.AddAttribute(1, "Column", column);
            b.AddAttribute(2, "FilterOptions", options);
            b.AddAttribute(3, "OnApply", EventCallback.Factory.Create<FilterDescriptor?>(this, _ => { }));
            b.CloseComponent();
        });
    }

    private static int ButtonsWithText(IRenderedComponent<DataGridColumnFilter<Row>> cut, string text) =>
        cut.FindAll("button").Count(b => b.TextContent.Trim() == text);

    [Fact]
    public void Facet_Mode_Has_No_Operator_Dropdown()
    {
        var column = new DataGridColumn<Row> { Field = "Status", Title = "Status", FilterType = DataGridFilterType.Select };
        var opts = new List<FilterOption> { new("Active", "A"), new("Inactive", "I") };

        var cut = RenderFilter(column, opts);

        // Operator picker is a SelectTrigger → role=combobox. None in facet mode.
        Assert.Empty(cut.FindAll("[role=combobox]"));
        // The facet checkbox list is still there (one Checkbox per option).
        Assert.Equal(2, cut.FindAll("button[role=checkbox]").Count);
    }

    [Fact]
    public void Facet_Mode_Has_Exactly_One_Apply_And_One_Clear()
    {
        var column = new DataGridColumn<Row> { Field = "Status", Title = "Status", FilterType = DataGridFilterType.Select };
        var opts = new List<FilterOption> { new("Active", "A"), new("Inactive", "I") };

        var cut = RenderFilter(column, opts);

        // Pre-fix the Select-all/Clear helper row added a SECOND "Clear" button.
        Assert.Equal(1, ButtonsWithText(cut, "Apply"));
        Assert.Equal(1, ButtonsWithText(cut, "Clear"));
    }

    [Fact]
    public void Text_Mode_Still_Shows_Operator_And_Single_Apply_Clear()
    {
        var column = new DataGridColumn<Row> { Field = "Name", Title = "Name", FilterType = DataGridFilterType.Text };

        var cut = RenderFilter(column, options: null);

        Assert.Single(cut.FindAll("[role=combobox]"));
        Assert.Equal(1, ButtonsWithText(cut, "Apply"));
        Assert.Equal(1, ButtonsWithText(cut, "Clear"));
    }
}
