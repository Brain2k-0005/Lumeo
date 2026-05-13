using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.PivotGrid;

/// <summary>
/// bUnit coverage for <see cref="L.PivotGrid{TItem}"/> — table rendering, aggregated
/// cell values, grand totals, the <c>ShowRowGrandTotal</c> toggle, collapse behaviour
/// and the empty state.
/// </summary>
public class PivotGridTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PivotGridTests() => _ctx.AddLumeoServices();

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

    private static IReadOnlyList<L.PivotField<Sale>> RowFields(int levels = 2)
    {
        var list = new List<L.PivotField<Sale>>
        {
            new("Region", s => s.Region),
        };
        if (levels >= 2) list.Add(new("Country", s => s.Country));
        return list;
    }

    private static IReadOnlyList<L.PivotField<Sale>> ColumnFields() => new List<L.PivotField<Sale>>
    {
        new("Year", s => s.Year),
    };

    private static IReadOnlyList<L.PivotMeasure<Sale>> SumMeasure() => new List<L.PivotMeasure<Sale>>
    {
        new("Amount", s => s.Amount, L.PivotAggregate.Sum),
    };

    [Fact]
    public void Renders_A_Table_Element()
    {
        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.RowFields, RowFields())
            .Add(g => g.ColumnFields, ColumnFields())
            .Add(g => g.Measures, SumMeasure()));

        Assert.NotNull(cut.Find("table"));
        Assert.NotNull(cut.Find("[role='table']"));
    }

    [Fact]
    public void Aggregated_Cell_Value_Appears()
    {
        // North x 2023 = 100 + 50 = 150 ; North x 2024 = 200 ; South x 2024 = 70
        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.RowFields, new List<L.PivotField<Sale>> { new("Region", s => s.Region) })
            .Add(g => g.ColumnFields, ColumnFields())
            .Add(g => g.Measures, SumMeasure()));

        var markup = cut.Markup;
        Assert.Contains("150", markup);   // North/2023
        Assert.Contains("200", markup);   // North/2024
        Assert.Contains("70", markup);    // South/2024
    }

    [Fact]
    public void Grand_Total_Row_Appears_By_Default()
    {
        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.RowFields, new List<L.PivotField<Sale>> { new("Region", s => s.Region) })
            .Add(g => g.ColumnFields, ColumnFields())
            .Add(g => g.Measures, SumMeasure()));

        Assert.Contains("Grand Total", cut.Markup);
        // Overall total = 450
        Assert.Contains("450", cut.Markup);
    }

    [Fact]
    public void ShowRowGrandTotal_False_Hides_The_Trailing_Total_Column_Header()
    {
        var with = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.RowFields, new List<L.PivotField<Sale>> { new("Region", s => s.Region) })
            .Add(g => g.ColumnFields, ColumnFields())
            .Add(g => g.Measures, SumMeasure()));
        var headerCountWith = with.FindAll("[role='columnheader']").Count;

        var without = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.RowFields, new List<L.PivotField<Sale>> { new("Region", s => s.Region) })
            .Add(g => g.ColumnFields, ColumnFields())
            .Add(g => g.Measures, SumMeasure())
            .Add(g => g.ShowRowGrandTotal, false)
            .Add(g => g.ShowColumnGrandTotal, false));

        // Removing the row grand-total column drops header cells; the body grand-total
        // row is also gone, so "Grand Total" should no longer be in the markup at all.
        Assert.True(without.FindAll("[role='columnheader']").Count < headerCountWith);
        Assert.DoesNotContain("Grand Total", without.Markup);
    }

    [Fact]
    public void Collapsing_A_Row_Group_Hides_Descendants()
    {
        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.RowFields, RowFields(2))
            .Add(g => g.ColumnFields, ColumnFields())
            .Add(g => g.Measures, SumMeasure())
            .Add(g => g.Collapsible, true));

        // Leaf country labels are visible while expanded.
        Assert.Contains("USA", cut.Markup);
        Assert.Contains("Brazil", cut.Markup);

        var rowCountBefore = cut.FindAll("tbody tr").Count;

        // Collapse the first expandable group ("North").
        var toggle = cut.FindAll("button[aria-expanded='true']").First();
        toggle.Click();

        var rowCountAfter = cut.FindAll("tbody tr").Count;
        Assert.True(rowCountAfter < rowCountBefore);
        // North's descendants (USA, Canada) are gone; South's (Brazil) remain.
        Assert.DoesNotContain("USA", cut.Markup);
        Assert.Contains("Brazil", cut.Markup);
    }

    // ---- Multi-level column hierarchy -------------------------------------

    private record QSale(string Region, int Year, int Quarter, decimal Amount);

    private static List<QSale> QData() => new()
    {
        new("North", 2023, 1, 100m),
        new("North", 2023, 2,  20m),
        new("North", 2024, 1, 200m),
        new("North", 2024, 3,  50m),
        new("South", 2023, 1,  30m),
        new("South", 2024, 2,  70m),
    };

    private static IReadOnlyList<L.PivotField<QSale>> YearQuarterFields() => new List<L.PivotField<QSale>>
    {
        new("Year",    s => s.Year),
        new("Quarter", s => s.Quarter, v => "Q" + v),
    };

    private static IReadOnlyList<L.PivotMeasure<QSale>> QSumMeasure() => new List<L.PivotMeasure<QSale>>
    {
        new("Amount", s => s.Amount, L.PivotAggregate.Sum),
    };

    [Fact]
    public void MultiLevel_Columns_Render_Two_Plus_One_Header_Rows()
    {
        // ColumnFields = [Year, Quarter] => 2 column-tree levels + 1 measure-name row = 3 thead rows.
        var cut = _ctx.Render<L.PivotGrid<QSale>>(p => p
            .Add(g => g.Items, QData())
            .Add(g => g.RowFields, new List<L.PivotField<QSale>> { new("Region", s => s.Region) })
            .Add(g => g.ColumnFields, YearQuarterFields())
            .Add(g => g.Measures, QSumMeasure()));

        var theadRows = cut.FindAll("thead tr");
        // 2 column-field levels + 1 measure-name row = 3.
        Assert.Equal(3, theadRows.Count);

        // The Year level should show "2023" and "2024".
        Assert.Contains("2023", cut.Markup);
        Assert.Contains("2024", cut.Markup);
        // The Quarter level should show formatted quarters.
        Assert.Contains("Q1", cut.Markup);
        Assert.Contains("Q2", cut.Markup);
        Assert.Contains("Q3", cut.Markup);
    }

    [Fact]
    public void MultiLevel_Columns_Cell_Lookup_Returns_Right_Aggregate()
    {
        // North x 2023 x Q1 = 100; North x 2024 x Q1 = 200; North x 2024 x Q3 = 50; South x 2024 x Q2 = 70.
        var cut = _ctx.Render<L.PivotGrid<QSale>>(p => p
            .Add(g => g.Items, QData())
            .Add(g => g.RowFields, new List<L.PivotField<QSale>> { new("Region", s => s.Region) })
            .Add(g => g.ColumnFields, YearQuarterFields())
            .Add(g => g.Measures, QSumMeasure())
            .Add(g => g.ShowColumnGrandTotal, false));

        var markup = cut.Markup;
        // Specific cell aggregates appear (formatted "N0" — counts are whole, otherwise no decimals when integral).
        Assert.Contains(">100<", markup);
        Assert.Contains(">200<", markup);
        Assert.Contains(">50<", markup);
        Assert.Contains(">70<", markup);
        // North x 2023 x Q2 = 20 (the row total for North 2023 = 120 should NOT be the cell value).
        Assert.Contains(">20<", markup);
    }

    [Fact]
    public void MultiLevel_Columns_Grand_Total_Column_Aggregates_Across_All_Columns()
    {
        // North overall = 100+20+200+50 = 370; South overall = 30+70 = 100; grand-total bottom-right = 470.
        var cut = _ctx.Render<L.PivotGrid<QSale>>(p => p
            .Add(g => g.Items, QData())
            .Add(g => g.RowFields, new List<L.PivotField<QSale>> { new("Region", s => s.Region) })
            .Add(g => g.ColumnFields, YearQuarterFields())
            .Add(g => g.Measures, QSumMeasure())
            .Add(g => g.ShowRowGrandTotal, true)
            .Add(g => g.ShowColumnGrandTotal, true));

        var markup = cut.Markup;
        Assert.Contains("Grand Total", markup);
        Assert.Contains(">370<", markup);
        Assert.Contains(">100<", markup);
        Assert.Contains(">470<", markup);
    }

    [Fact]
    public void SingleLevel_Column_Fallback_Renders_Two_Header_Rows()
    {
        // Backwards-compat: ColumnFields = [Year] only — must still render the classic 1 col-level + 1 measure row.
        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, Data())
            .Add(g => g.RowFields, new List<L.PivotField<Sale>> { new("Region", s => s.Region) })
            .Add(g => g.ColumnFields, ColumnFields())
            .Add(g => g.Measures, SumMeasure()));

        var theadRows = cut.FindAll("thead tr");
        Assert.Equal(2, theadRows.Count);

        // Sanity: aggregates still match the legacy single-level expectations.
        Assert.Contains("150", cut.Markup); // North 2023
        Assert.Contains("200", cut.Markup); // North 2024
        Assert.Contains("70",  cut.Markup); // South 2024
        Assert.Contains("Grand Total", cut.Markup);
    }

    [Fact]
    public void Empty_Items_Shows_EmptyContent()
    {
        var cut = _ctx.Render<L.PivotGrid<Sale>>(p => p
            .Add(g => g.Items, new List<Sale>())
            .Add(g => g.RowFields, RowFields())
            .Add(g => g.ColumnFields, ColumnFields())
            .Add(g => g.Measures, SumMeasure())
            .Add(g => g.EmptyContent, (Microsoft.AspNetCore.Components.RenderFragment)(b => b.AddContent(0, "Nothing here"))));

        Assert.Contains("Nothing here", cut.Markup);
        Assert.Empty(cut.FindAll("table"));
    }
}
