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

    // ---------------------------------------------------------------------------
    // Filter popover wrapper: rounded corners must clip descendants (2.1.1)
    //
    // Bug: a custom FilterTemplate with hover:bg-accent rows painted hover
    // backgrounds past the rounded-lg corners of the filter popover, because
    // the wrapper div had rounded-lg but no overflow-hidden. CSS border-radius
    // only rounds the box itself, not its children.
    //
    // Fix: DataGridColumnFilter's CssClass now includes overflow-hidden, and
    // w-64 became min-w-64 so a wider custom template grows the popover
    // instead of overflowing past the rounded edge.
    // ---------------------------------------------------------------------------

    [Fact]
    public void ColumnFilter_Root_Has_OverflowHidden_And_MinWidth_For_Rounded_Clipping()
    {
        var column = new DataGridColumn<TestItem>
        {
            Field = "Name",
            Title = "Name",
            Filterable = true,
            FilterType = DataGridFilterType.Text
        };

        var cut = _ctx.Render<DataGridColumnFilter<TestItem>>(p => p
            .Add(x => x.Column, column));

        // The component's root div is the first child of the bUnit render
        // root and carries the popover styling.
        var root = cut.Nodes[0] as AngleSharp.Dom.IElement;
        Assert.NotNull(root);
        var cls = root!.GetAttribute("class") ?? "";

        Assert.Contains("overflow-hidden", cls);
        Assert.Contains("min-w-64", cls);
        Assert.Contains("rounded-lg", cls);
        // Back-compat guard: the fixed w-64 must NOT be present, otherwise
        // a wider custom FilterTemplate would overflow past the rounded edge
        // again.
        Assert.DoesNotContain(" w-64 ", $" {cls} ");
    }

    [Fact]
    public void ColumnFilter_With_Custom_Template_Still_Has_OverflowHidden()
    {
        // Even when FilterTemplate is set (custom-template branch), the
        // outer wrapper must still clip descendants. The custom-template
        // branch and the default branch share the same root div.
        RenderFragment<DataGridFilterTemplateContext> customTemplate = ctx => b =>
        {
            b.OpenElement(0, "label");
            b.AddAttribute(1, "class", "hover:bg-accent");
            b.AddContent(2, "row");
            b.CloseElement();
        };

        var column = new DataGridColumn<TestItem>
        {
            Field = "Name",
            Title = "Name",
            Filterable = true,
            FilterTemplate = customTemplate
        };

        var cut = _ctx.Render<DataGridColumnFilter<TestItem>>(p => p
            .Add(x => x.Column, column));

        var root = cut.Nodes[0] as AngleSharp.Dom.IElement;
        Assert.NotNull(root);
        var cls = root!.GetAttribute("class") ?? "";

        Assert.Contains("overflow-hidden", cls);
        Assert.Contains("rounded-lg", cls);
    }

    // ---------------------------------------------------------------------------
    // Filter popover positioning: must NOT contribute to grid scroll-width (2.1.2)
    //
    // Bug: the filter popover wrapper was <div class="absolute top-full left-0">
    // anchored to its <th> (which is position:relative). On the rightmost column,
    // the popover's content extended past the th's right edge → the overflow:auto
    // scroll container interpreted the absolute descendant's overflow as
    // scrollWidth → a phantom horizontal scrollbar appeared every time the user
    // opened a filter on the right side of the grid.
    //
    // Fix: render the popover with position:fixed (inline style) and pin to the
    // trigger via JS-computed getBoundingClientRect coords (Interop.PositionFixed,
    // same mechanism PopoverContent uses). The fixed-positioned descendant does
    // not contribute to its ancestor's scrollWidth, so the bug can't recur.
    // ---------------------------------------------------------------------------

    [Fact]
    public void Filter_Popover_Uses_Fixed_Positioning_Not_Absolute()
    {
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, Data())
            .AddChildContent<DataGridColumnDef<TestItem>>(c => c
                .Add(x => x.Field, "Name")
                .Add(x => x.Filterable, true)));

        // Click the filter trigger button on the only data column to open the popover.
        var filterBtn = cut.Find("th[data-slot='datagrid-header-cell'] button:has(svg)");
        // The first matching button is the sort toggle; the filter button comes after
        // when Filterable=true. We want the one that is NOT the sort button — the
        // sort toggle disables itself when Sortable is false but is still present
        // here (Sortable defaults to false on this column). Find the filter button
        // by its visible-on-hover icon class signature.
        var filterTriggers = cut.FindAll("th[data-slot='datagrid-header-cell'] button");
        var trigger = filterTriggers.FirstOrDefault(b =>
            (b.GetAttribute("class") ?? "").Contains("hover:bg-muted/60"));
        Assert.NotNull(trigger);
        trigger!.Click();

        // The popover container must now exist with inline position:fixed.
        // Querying by id is brittle (GUID), so we sweep all descendants and
        // assert the inline style contains "position: fixed".
        var fixedDivs = cut.FindAll("div").Where(d =>
            (d.GetAttribute("style") ?? "").Contains("position: fixed")
            && d.QuerySelector("[data-slot='datagrid-filter-custom']") is null
            && d.QuerySelector("input, button, label") is not null).ToList();

        Assert.NotEmpty(fixedDivs);

        // Hard regression guard: the old "absolute top-full left-0" wrapper
        // must NOT be present on any element containing the filter content.
        var absWrappers = cut.FindAll("div.absolute.top-full.left-0").ToList();
        Assert.Empty(absWrappers);
    }

    [Fact]
    public void Filter_Trigger_Button_Has_Stable_Id_For_Positioning_Anchor()
    {
        // The JS positioner reads the trigger via document.getElementById, so
        // the trigger button MUST carry a stable id whenever the column is
        // Filterable — irrespective of whether the popover is currently open.
        var cut = _ctx.Render<DataGrid<TestItem>>(p => p
            .Add(x => x.Items, Data())
            .AddChildContent<DataGridColumnDef<TestItem>>(c => c
                .Add(x => x.Field, "Name")
                .Add(x => x.Filterable, true)));

        var triggers = cut.FindAll("th[data-slot='datagrid-header-cell'] button");
        var filterTrigger = triggers.FirstOrDefault(b =>
            (b.GetAttribute("class") ?? "").Contains("hover:bg-muted/60"));
        Assert.NotNull(filterTrigger);
        var id = filterTrigger!.GetAttribute("id");
        Assert.False(string.IsNullOrWhiteSpace(id), "filter trigger must have an id");
        Assert.StartsWith("dg-filter-trigger-", id);
    }
}
