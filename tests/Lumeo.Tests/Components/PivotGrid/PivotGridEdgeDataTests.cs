using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.PivotGrid;

/// <summary>
/// Regression coverage for PivotGrid's "edge-data" battle-test bugs on the COLUMN side:
/// <list type="bullet">
///   <item>#142 — column path-keys used a naive <c>'|' + "L{n}={value}"</c> join (the same
///   scheme that was hardened for ROW keys in #212). Distinct multi-level column paths whose
///   raw values embed the separator pattern collapse to the same key and cross-contaminate
///   cells. The fix mirrors RowNode.Key (U+001F Unit Separator + length-prefixed segments).</item>
///   <item>#143 — drill-down <c>ColumnKeys</c> were re-parsed out of the string PathKey, so a
///   non-string column field (e.g. an <see cref="int"/> Year) lost its real type. The fix
///   threads the actual typed RawKey chain onto the leaf and surfaces it verbatim.</item>
/// </list>
/// </summary>
public class PivotGridEdgeDataTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PivotGridEdgeDataTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Sale(string Region, int Year, decimal Amount);

    private static IReadOnlyList<L.PivotMeasure<Sale>> SumMeasure() => new List<L.PivotMeasure<Sale>>
    {
        new("Amount", s => s.Amount, L.PivotAggregate.Sum),
    };

    // Normalised visible text of every <tbody> row (whitespace collapsed).
    private static List<string> BodyRowTexts(IRenderedComponent<L.PivotGrid<Sale>> cut)
        => cut.FindAll("tbody tr")
              .Select(r => System.Text.RegularExpressions.Regex.Replace(r.TextContent, @"\s+", ""))
              .ToList();

    // ---- #142 : column path-key collision ---------------------------------

    // A two-level column field uses string values; the FIRST column field carries an
    // embedded separator pattern in one row so the naive "L0=..|L1=.." join aliases
    // two genuinely different column paths.
    private record ColSale(string Region, string A, string B, decimal Amount);

    private static IReadOnlyList<L.PivotMeasure<ColSale>> ColSum() => new List<L.PivotMeasure<ColSale>>
    {
        new("Amount", s => s.Amount, L.PivotAggregate.Sum),
    };

    [Fact]
    public void Colliding_Column_Paths_Keep_Their_Own_Cell_Aggregates()
    {
        // Two distinct two-level column paths whose naive "L0=..|L1=.." encodings are
        // byte-identical:
        //   leaf 1: A="p",      B="q|L1=r"  => "L0=p|L1=q|L1=r"
        //   leaf 2: A="p|L1=q", B="r"       => "L0=p|L1=q|L1=r"
        // Pre-fix both leaves (and both items' per-item keys) collapse to the same
        // column path-key, so the two items land in ONE cell bucket and one amount
        // clobbers/merges the other. With the U+001F + length-prefix hardening the two
        // column paths get distinct keys, so each cell shows only its own item.
        var data = new List<ColSale>
        {
            new("North", "p",      "q|L1=r", 111m),
            new("North", "p|L1=q", "r",      222m),
        };

        var cut = _ctx.Render<L.PivotGrid<ColSale>>(p => p
            .Add(g => g.Items, data)
            .Add(g => g.RowFields, new List<L.PivotField<ColSale>> { new("Region", s => s.Region) })
            .Add(g => g.ColumnFields, new List<L.PivotField<ColSale>>
            {
                new("A", s => s.A),
                new("B", s => s.B),
            })
            .Add(g => g.Measures, ColSum())
            .Add(g => g.ShowRowGrandTotal, false)
            .Add(g => g.ShowColumnGrandTotal, false)
            .Add(g => g.ShowSubtotals, false)
            .Add(g => g.Collapsible, false));

        var markup = cut.Markup;
        // Each item must keep its own cell value in its own column.
        Assert.Contains(">111<", markup);
        Assert.Contains(">222<", markup);
        // Pre-fix the two items shared a bucket, so the single surviving cell summed
        // them to 333 (or showed only one). The hardened key keeps them separate.
        Assert.DoesNotContain(">333<", markup);
    }

    // ---- #143 : drill-down ColumnKeys preserve the real type ---------------

    [Fact]
    public void Cell_Click_ColumnKeys_Preserve_The_Non_String_Column_Field_Type()
    {
        // The Year column field is an int. Clicking a data cell must surface the column
        // key as the real int value, not a re-parsed string. Pre-fix DecodeColumnKeys
        // split the PathKey and returned the segment as a string, so ColumnKeys[0] was
        // "2023" (string) instead of 2023 (int).
        var data = new List<Sale>
        {
            new("North", 2023, 100m),
            new("North", 2024, 200m),
        };

        L.PivotCellClickArgs? captured = null;

        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, data)
            .Add(g => g.RowFields, new List<L.PivotField<Sale>> { new("Region", s => s.Region) })
            .Add(g => g.ColumnFields, new List<L.PivotField<Sale>> { new("Year", s => s.Year) })
            .Add(g => g.Measures, SumMeasure())
            .Add(g => g.ShowRowGrandTotal, false)
            .Add(g => g.ShowColumnGrandTotal, false)
            .Add(g => g.OnCellClick, EventCallback.Factory.Create<L.PivotCellClickArgs>(this, a => captured = a)));

        // Click the FIRST data cell (North x 2023 = 100). It is the first <td role="cell">.
        var firstCell = cut.FindAll("td[role='cell']")
            .First(td => td.TextContent.Contains("100"));
        firstCell.Click();

        Assert.NotNull(captured);
        var columnKeys = captured!.ColumnKeys;
        Assert.Single(columnKeys);
        // The fix preserves the typed RawKey: the column key is the int 2023, equal by
        // boxed value. Pre-fix this was the string "2023" and the int comparison failed.
        Assert.IsType<int>(columnKeys[0]);
        Assert.Equal(2023, columnKeys[0]);
    }
}
