using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.PivotGrid;

/// <summary>
/// Regression coverage for PivotGrid's "lifecycle" battle-test bugs — specifically
/// that the internal <c>_collapsed</c> set is reconciled against the freshly-built
/// row tree on every real rebuild, so a collapsed group whose path later disappears
/// cannot leak its key and silently re-collapse a future same-path group.
/// </summary>
public class PivotGridLifecycleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PivotGridLifecycleTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Sale(string Region, string Country, int Year, decimal Amount);

    // Two-level rows (Region -> Country) so "North"/"South" are collapsible group nodes.
    private static IReadOnlyList<L.PivotField<Sale>> RegionCountryFields() => new List<L.PivotField<Sale>>
    {
        new("Region", s => s.Region),
        new("Country", s => s.Country),
    };

    private static IReadOnlyList<L.PivotField<Sale>> ColumnFields() => new List<L.PivotField<Sale>>
    {
        new("Year", s => s.Year),
    };

    private static IReadOnlyList<L.PivotMeasure<Sale>> SumMeasure() => new List<L.PivotMeasure<Sale>>
    {
        new("Amount", s => s.Amount, L.PivotAggregate.Sum),
    };

    // Data that contains a "North" region group (with descendants USA / Canada).
    private static List<Sale> DataWithNorth() => new()
    {
        new("North", "USA",    2023, 100m),
        new("North", "Canada", 2023,  50m),
        new("South", "Brazil", 2023,  30m),
    };

    // Data that contains NO "North" region group — only "South".
    private static List<Sale> DataWithoutNorth() => new()
    {
        new("South", "Brazil", 2023,  30m),
    };

    [Fact]
    public void Stale_Collapse_Key_Does_Not_ReCollapse_A_Reappearing_Same_Path_Group()
    {
        // The RowNode.Key is derived deterministically from the (path, level) tuple,
        // so the "North" group node produces the SAME key every rebuild. If the
        // _collapsed set is never pruned, collapsing "North", then dropping it from
        // the data, then re-adding it, leaves North's stale key in _collapsed — so
        // the reappearing North group renders COLLAPSED (descendants hidden) even
        // though the user never collapsed this fresh group.
        var rowFields = RegionCountryFields();
        var columnFields = ColumnFields();
        var measures = SumMeasure();

        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, DataWithNorth())
            .Add(g => g.RowFields, rowFields)
            .Add(g => g.ColumnFields, columnFields)
            .Add(g => g.Measures, measures)
            .Add(g => g.Collapsible, true));

        // North is expanded => its descendants are visible.
        Assert.Contains("USA", cut.Markup);

        // Collapse the "North" group — this records North's key in _collapsed.
        var northToggle = cut.FindAll("button[aria-expanded='true']").First();
        northToggle.Click();
        Assert.DoesNotContain("USA", cut.Markup); // collapsed: descendants hidden

        // Data change #1: a NEW Items reference WITHOUT North. The rebuild must prune
        // North's now-orphaned key from _collapsed.
        cut.Render(p => p
            .Add(g => g.Items, DataWithoutNorth())
            .Add(g => g.RowFields, rowFields)
            .Add(g => g.ColumnFields, columnFields)
            .Add(g => g.Measures, measures)
            .Add(g => g.Collapsible, true));
        Assert.DoesNotContain("North", cut.Markup);

        // Data change #2: a NEW Items reference where North reappears with the SAME
        // path. With the fix the stale key was pruned, so North renders EXPANDED.
        cut.Render(p => p
            .Add(g => g.Items, DataWithNorth())
            .Add(g => g.RowFields, rowFields)
            .Add(g => g.ColumnFields, columnFields)
            .Add(g => g.Measures, measures)
            .Add(g => g.Collapsible, true));

        // Fix: the reappearing North group is expanded — its descendants are visible
        // and its toggle reports aria-expanded='true'.
        Assert.Contains("USA", cut.Markup);
        Assert.Contains(cut.FindAll("button[aria-expanded]"),
            b => b.TextContent.Contains("North") && b.GetAttribute("aria-expanded") == "true");
        // Pre-fix the leaked stale key re-collapsed North, hiding its descendants.
    }
}
