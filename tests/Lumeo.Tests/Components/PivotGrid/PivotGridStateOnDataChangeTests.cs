using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.PivotGrid;

/// <summary>
/// Regression coverage for PivotGrid's "state-on-data-change" battle-test bugs —
/// the OnParametersSet short-circuit gate must not skip a rebuild when a tree-shaping
/// parameter (other than Items / dimensions / the previously-tracked flags) changes
/// while the Items reference is stable.
/// </summary>
public class PivotGridStateOnDataChangeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PivotGridStateOnDataChangeTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Sale(string Region, string Country, int Year, decimal Amount);

    private static List<Sale> Data() => new()
    {
        new("North", "USA",     2023, 100m),
        new("North", "Canada",  2023,  50m),
        new("North", "USA",     2024, 200m),
        new("South", "Brazil",  2023,  30m),
        new("South", "Brazil",  2024,  70m),
    };

    private static IReadOnlyList<L.PivotField<Sale>> RegionRowField() => new List<L.PivotField<Sale>>
    {
        new("Region", s => s.Region),
    };

    private static IReadOnlyList<L.PivotField<Sale>> ColumnFields() => new List<L.PivotField<Sale>>
    {
        new("Year", s => s.Year),
    };

    private static IReadOnlyList<L.PivotMeasure<Sale>> SumMeasure() => new List<L.PivotMeasure<Sale>>
    {
        new("Amount", s => s.Amount, L.PivotAggregate.Sum),
    };

    [Fact]
    public void Changing_GrandTotalLabel_With_Stable_Items_Updates_The_Column_Header()
    {
        // Hold every tree-shaping input by a STABLE reference so the OnParametersSet
        // short-circuit gate (ReferenceEquals(Items, _lastItemsRef) + dims hash +
        // ShowRowGrandTotal/ShowSubtotals) fires on the second render. Only
        // GrandTotalLabel changes. The grand-total leaf column header label is baked
        // into the cached _columnHeaderRows at build time, so a gate that ignores
        // GrandTotalLabel leaves the stale label in the <thead>.
        //
        // ShowColumnGrandTotal is disabled because its footer row renders
        // EffectiveGrandTotalLabel directly (uncached) and would update regardless,
        // masking the bug. ShowRowGrandTotal stays true so the grand-total COLUMN
        // (the cached header under test) exists.
        var items = Data();
        var rowFields = RegionRowField();
        var columnFields = ColumnFields();
        var measures = SumMeasure();

        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, items)
            .Add(g => g.RowFields, rowFields)
            .Add(g => g.ColumnFields, columnFields)
            .Add(g => g.Measures, measures)
            .Add(g => g.ShowRowGrandTotal, true)
            .Add(g => g.ShowColumnGrandTotal, false)
            .Add(g => g.GrandTotalLabel, "Total A"));

        // Initial label is present in the cached column header.
        var theadBefore = cut.Find("thead").InnerHtml;
        Assert.Contains("Total A", theadBefore);

        // Re-render the SAME instance with the SAME Items + dimension references; only
        // the grand-total label changes. The gate would otherwise short-circuit.
        cut.Render(p => p
            .Add(g => g.Items, items)
            .Add(g => g.RowFields, rowFields)
            .Add(g => g.ColumnFields, columnFields)
            .Add(g => g.Measures, measures)
            .Add(g => g.ShowRowGrandTotal, true)
            .Add(g => g.ShowColumnGrandTotal, false)
            .Add(g => g.GrandTotalLabel, "Total B"));

        var theadAfter = cut.Find("thead").InnerHtml;
        // Fix: the cached grand-total column header reflects the new label.
        Assert.Contains("Total B", theadAfter);
        // Pre-fix: the stale "Total A" survives in the <thead> column header.
        Assert.DoesNotContain("Total A", theadAfter);
    }
}
