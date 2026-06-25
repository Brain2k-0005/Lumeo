using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Filter;

/// <summary>
/// Battle-test regressions for the DataGrid column-filter popover ("Filter"
/// component) edge-data class:
///
/// #10 (medium) — Applying a Select-type column filter with ZERO options
///   checked produced an ACTIVE no-op filter: Apply() built
///   <c>filterValue = string.Join(",", _selectedOptions)</c> = "" and still
///   invoked OnApply with a non-null descriptor, which the grid surfaces as an
///   active filter while the Evaluate() "no options => match all" rule makes it
///   match every row. The fix treats an empty selection on Apply as a CLEAR
///   (OnApply.InvokeAsync(null)).
///
/// #11 (medium) — The default operator was seeded purely from
///   <c>Column.FilterType</c> (e.g. Number => Equals) and never consulted the
///   <c>Column.Operators</c> whitelist, so a column that disallowed the
///   type-default left that operator selected-but-not-in-the-list and applied
///   it silently on a bare Apply. The fix clamps the seeded default into the
///   available (whitelisted) set in OnParametersSet.
/// </summary>
public class DataGridColumnFilterEdgeDataTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DataGridColumnFilterEdgeDataTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(int Id, string Name, string Status);

    private IRenderedComponent<DataGridColumnFilter<Row>> RenderFilter(
        DataGridColumn<Row> column, Action<FilterDescriptor?> onApply)
    {
        return _ctx.Render<DataGridColumnFilter<Row>>(b =>
        {
            b.OpenComponent<DataGridColumnFilter<Row>>(0);
            b.AddAttribute(1, "Column", column);
            b.AddAttribute(2, "FilterOptions", column.FilterOptions);
            b.AddAttribute(3, "OnApply",
                EventCallback.Factory.Create<FilterDescriptor?>(this, onApply));
            b.CloseComponent();
        });
    }

    // ── #10 — empty Select selection on Apply must CLEAR, not create a no-op ──

    [Fact]
    public void Apply_With_No_Select_Options_Checked_Clears_Instead_Of_Active_NoOp()
    {
        var column = new DataGridColumn<Row>
        {
            Field = "Status", Title = "Status", Filterable = true,
            FilterType = DataGridFilterType.Select,
            FilterOptions = new List<FilterOption>
            {
                new("Open", "open"),
                new("Closed", "closed"),
            }
        };

        FilterDescriptor? applied = null;
        var invoked = false;
        var cut = RenderFilter(column, d => { applied = d; invoked = true; });

        // No checkboxes toggled — _selectedOptions is empty. Click Apply.
        // The Apply button uniquely carries the flex-1 + bg-primary classes
        // (SelectAll/ClearAll use text-primary / text-muted-foreground, and the
        // Lumeo Checkbox is a role=checkbox <button>, never type=text/checkbox).
        cut.Find("button.flex-1.bg-primary").Click();

        Assert.True(invoked, "OnApply should fire when Apply is clicked.");
        Assert.Null(applied);
            // ^ The fix routes an empty Select selection to OnApply.InvokeAsync(null)
            //   (a CLEAR). Without the fix `applied` would be a non-null descriptor
            //   carrying Value="" — an active filter that matches every row.
    }

    [Fact]
    public void Apply_With_A_Select_Option_Checked_Still_Emits_A_Descriptor()
    {
        // Guard the normal path: a non-empty selection must still produce an
        // active filter descriptor (the empty-clear shortcut didn't swallow it).
        var column = new DataGridColumn<Row>
        {
            Field = "Status", Title = "Status", Filterable = true,
            FilterType = DataGridFilterType.Select,
            FilterOptions = new List<FilterOption>
            {
                new("Open", "open"),
                new("Closed", "closed"),
            }
        };

        FilterDescriptor? applied = null;
        var cut = RenderFilter(column, d => applied = d);

        // Check the first option (the Lumeo Checkbox is a role=checkbox <button>
        // toggled by click), then Apply.
        cut.FindAll("button[role=checkbox]")[0].Click();
        cut.Find("button.flex-1.bg-primary").Click();

        Assert.NotNull(applied);
        Assert.Equal("Status", applied!.Field);
        Assert.Equal("open", applied.Value?.ToString());
    }

    // ── #11 — seeded default operator must respect the Operators whitelist ────

    [Fact]
    public void Default_Operator_Is_Clamped_Into_The_Column_Operators_Whitelist()
    {
        // Number's type-default is Equals, but this column disallows Equals via
        // the whitelist. The seeded operator must clamp to an allowed one (the
        // first available) rather than staying on the disallowed Equals.
        var column = new DataGridColumn<Row>
        {
            Field = "Id", Title = "ID", Filterable = true,
            FilterType = DataGridFilterType.Number,
            Operators = new List<FilterOperator>
            {
                FilterOperator.GreaterThan,
                FilterOperator.LessThan,
            }
        };

        FilterDescriptor? applied = null;
        var cut = RenderFilter(column, d => applied = d);

        // Apply immediately (no operator interaction) so the descriptor carries
        // whatever default the component seeded.
        cut.Find("button.flex-1.bg-primary").Click();

        Assert.NotNull(applied);
        Assert.NotEqual(FilterOperator.Equals, applied!.Operator);
            // ^ Without the clamp the seeded default Equals would apply silently
            //   even though the column forbids it.
        Assert.Contains(applied.Operator, column.Operators!);
    }

    [Fact]
    public void Default_Operator_Unchanged_When_Type_Default_Is_Allowed()
    {
        // Guard the normal path: when the type-default IS in the whitelist the
        // clamp is a no-op and Equals is preserved.
        var column = new DataGridColumn<Row>
        {
            Field = "Id", Title = "ID", Filterable = true,
            FilterType = DataGridFilterType.Number,
            Operators = new List<FilterOperator>
            {
                FilterOperator.Equals,
                FilterOperator.GreaterThan,
            }
        };

        FilterDescriptor? applied = null;
        var cut = RenderFilter(column, d => applied = d);

        cut.Find("button.flex-1.bg-primary").Click();

        Assert.NotNull(applied);
        Assert.Equal(FilterOperator.Equals, applied!.Operator);
    }
}
