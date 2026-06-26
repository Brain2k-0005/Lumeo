using System.Diagnostics;
using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.PivotGrid;

/// <summary>
/// Enterprise-scale "battle test" for <see cref="L.PivotGrid{TItem}"/>: a pivot table's
/// whole job is to reduce a huge fact table to a small cross-tab, so feed it a
/// MILLION source rows and prove the aggregation pipeline (a) stays within a CI-safe
/// time budget, (b) collapses to a tiny bounded grid in the DOM, and (c) is CORRECT
/// at scale — the grand total must equal the sum over every one of the million rows.
/// </summary>
public class PivotGridScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public PivotGridScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Sale(string Region, int Year, decimal Amount);

    private const int Millions = 1_000_000;
    private static readonly string[] Regions = { "North", "South", "East", "West", "Central" };

    private static string DigitsOnly(string s) => new(s.Where(char.IsDigit).ToArray());

    [Fact]
    public void Pivoting_a_million_source_rows_aggregates_correctly_and_within_budget()
    {
        // 1,000,000 sales spread across 5 regions x 4 years = a 5x4 cross-tab.
        // Every Amount is 1, so each of the 20 cells == its row count and the grand
        // total == 1,000,000 exactly — a format-robust correctness anchor.
        var data = new List<Sale>(Millions);
        for (var i = 0; i < Millions; i++)
            data.Add(new Sale(Regions[i % Regions.Length], 2020 + (i % 4), 1m));

        var sw = Stopwatch.StartNew();
        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, data)
            .Add(g => g.RowFields, new List<L.PivotField<Sale>> { new("Region", s => s.Region) })
            .Add(g => g.ColumnFields, new List<L.PivotField<Sale>> { new("Year", s => s.Year) })
            .Add(g => g.Measures, new List<L.PivotMeasure<Sale>> { new("Amount", s => s.Amount, L.PivotAggregate.Sum) }));
        sw.Stop();

        // Aggregating a million rows stays within a generous CI-safe budget — a
        // blow-out flags an accidental-quadratic regression in the pivot engine.
        Assert.True(sw.ElapsedMilliseconds < 10_000,
            $"Pivot of 1M rows took {sw.ElapsedMilliseconds}ms.");

        // The output is a tiny cross-tab — NOT a million rows in the DOM.
        var bodyRows = cut.FindAll("tbody tr");
        Assert.InRange(bodyRows.Count, 1, 50);

        // Correctness at scale: the grand total (1,000,000) must appear — proves
        // every source row was folded into the aggregate, not just a sample.
        Assert.Contains("1000000", DigitsOnly(cut.Markup));
    }
}
