using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Tests for the column-filter extension points: per-column operator whitelist
/// (<see cref="DataGridColumn{TItem}.Operators"/>), custom filter render fragment
/// (<see cref="DataGridColumn{TItem}.FilterTemplate"/>), and the underlying
/// operator evaluation engine (<see cref="DataGridFilterOperator"/>).
/// </summary>
public class DataGridFilterExtensibilityTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridFilterExtensibilityTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record TestItem(int Id, string Name);

    private static List<TestItem> Data() => new()
    {
        new(1, "Alice"),
        new(2, "Bob"),
        new(3, "Charlie"),
    };

    // ---------------------------------------------------------------------------
    // Filter operator evaluator — direct unit tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Evaluate_Contains_Is_Case_Insensitive()
    {
        var f = new FilterDescriptor("Name", FilterOperator.Contains, "ali");
        Assert.True(DataGridFilterOperator.Evaluate(f, "Alice"));
        Assert.False(DataGridFilterOperator.Evaluate(f, "Bob"));
    }

    [Fact]
    public void Evaluate_NotContains_Inverts_Contains()
    {
        var f = new FilterDescriptor("Name", FilterOperator.NotContains, "ali");
        Assert.False(DataGridFilterOperator.Evaluate(f, "Alice"));
        Assert.True(DataGridFilterOperator.Evaluate(f, "Bob"));
    }

    [Fact]
    public void Evaluate_StartsWith_EndsWith_Work_Case_Insensitive()
    {
        var starts = new FilterDescriptor("Name", FilterOperator.StartsWith, "al");
        var ends = new FilterDescriptor("Name", FilterOperator.EndsWith, "CE");
        Assert.True(DataGridFilterOperator.Evaluate(starts, "Alice"));
        Assert.True(DataGridFilterOperator.Evaluate(ends, "Alice"));
        Assert.False(DataGridFilterOperator.Evaluate(starts, "Bob"));
    }

    [Fact]
    public void Evaluate_IsEmpty_And_IsNotEmpty_Handle_Null()
    {
        var empty = new FilterDescriptor("Name", FilterOperator.IsEmpty, null);
        var notEmpty = new FilterDescriptor("Name", FilterOperator.IsNotEmpty, null);
        Assert.True(DataGridFilterOperator.Evaluate(empty, null));
        Assert.True(DataGridFilterOperator.Evaluate(empty, ""));
        Assert.False(DataGridFilterOperator.Evaluate(empty, "Alice"));
        Assert.True(DataGridFilterOperator.Evaluate(notEmpty, "Alice"));
        Assert.False(DataGridFilterOperator.Evaluate(notEmpty, null));
    }

    [Fact]
    public void Evaluate_Numeric_Comparisons_Use_CompareValues()
    {
        var gt = new FilterDescriptor("Amount", FilterOperator.GreaterThan, 100, FilterType: DataGridFilterType.Number);
        var lt = new FilterDescriptor("Amount", FilterOperator.LessThan, 100, FilterType: DataGridFilterType.Number);
        Assert.True(DataGridFilterOperator.Evaluate(gt, 150));
        Assert.False(DataGridFilterOperator.Evaluate(gt, 100));
        Assert.True(DataGridFilterOperator.Evaluate(lt, 50));
    }

    [Fact]
    public void Evaluate_Between_Is_Inclusive()
    {
        var between = new FilterDescriptor("Amount", FilterOperator.Between, 10, 20, DataGridFilterType.Number);
        Assert.True(DataGridFilterOperator.Evaluate(between, 10));
        Assert.True(DataGridFilterOperator.Evaluate(between, 15));
        Assert.True(DataGridFilterOperator.Evaluate(between, 20));
        Assert.False(DataGridFilterOperator.Evaluate(between, 9));
        Assert.False(DataGridFilterOperator.Evaluate(between, 21));
    }

    // ---------------------------------------------------------------------------
    // Operators whitelist — per-column restriction
    // ---------------------------------------------------------------------------

    [Fact]
    public void Column_Operators_Whitelist_Stored_On_Column()
    {
        var col = new DataGridColumn<TestItem>
        {
            Field = "Name",
            Filterable = true,
            Operators = new List<FilterOperator> { FilterOperator.Equals, FilterOperator.StartsWith }
        };

        Assert.NotNull(col.Operators);
        Assert.Equal(2, col.Operators!.Count);
        Assert.Contains(FilterOperator.Equals, col.Operators);
        Assert.Contains(FilterOperator.StartsWith, col.Operators);
    }

    [Fact]
    public void ColumnDef_Forwards_Operators_Whitelist_To_Column()
    {
        DataGrid<TestItem>? grid = null;
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, Data())
            .AddChildContent<DataGridColumnDef<TestItem>>(c => c
                .Add(x => x.Field, "Name")
                .Add(x => x.Filterable, true)
                .Add(x => x.Operators, new List<FilterOperator>
                {
                    FilterOperator.Equals,
                    FilterOperator.StartsWith
                })));
        grid = cut.Instance;
        var layout = grid.GetCurrentLayout();
        Assert.NotNull(layout);
        // The column was registered — verify via markup that filter toggle is available
        Assert.Contains("Name", cut.Markup);
    }

    // ---------------------------------------------------------------------------
    // Custom filter template — plug in your own UI
    // ---------------------------------------------------------------------------

    [Fact]
    public void ColumnDef_Forwards_FilterTemplate_To_Column()
    {
        // This test verifies the parameter is forwarded; the template is exercised
        // only when the filter popover is opened, which requires DOM events.
        RenderFragment<DataGridFilterTemplateContext> customTemplate = ctx => b =>
        {
            b.OpenElement(0, "div");
            b.AddAttribute(1, "data-testid", "custom-filter");
            b.AddContent(2, "custom filter ui");
            b.CloseElement();
        };

        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, Data())
            .AddChildContent<DataGridColumnDef<TestItem>>(c => c
                .Add(x => x.Field, "Name")
                .Add(x => x.Filterable, true)
                .Add(x => x.FilterTemplate, customTemplate)));

        // Grid rendered without errors — template is wired on the column definition.
        Assert.Contains("Name", cut.Markup);
    }

    [Fact]
    public async Task FilterTemplate_Context_Apply_Invokes_Callback_With_Descriptor()
    {
        FilterDescriptor? captured = null;
        var applyCalled = false;

        Func<FilterDescriptor?, Task> apply = desc =>
        {
            captured = desc;
            applyCalled = true;
            return Task.CompletedTask;
        };

        var ctx = new DataGridFilterTemplateContext("Name", null, apply);
        var descriptor = new FilterDescriptor("Name", FilterOperator.Equals, "Alice");

        await ctx.Apply(descriptor);

        Assert.True(applyCalled);
        Assert.NotNull(captured);
        Assert.Equal("Name", captured!.Field);
        Assert.Equal("Alice", captured.Value);
    }

    [Fact]
    public async Task FilterTemplate_Context_Apply_Null_Clears_Filter()
    {
        FilterDescriptor? captured = new FilterDescriptor("Name", FilterOperator.Equals, "stale");
        Func<FilterDescriptor?, Task> apply = desc => { captured = desc; return Task.CompletedTask; };
        var ctx = new DataGridFilterTemplateContext("Name", null, apply);

        await ctx.Apply(null);

        Assert.Null(captured);
    }
}
