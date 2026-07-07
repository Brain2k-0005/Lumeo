using System.Linq;
using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// Wave 5 — Chart accessibility layer (shadcn <c>accessibilityLayer</c> parity for the
/// canvas-rendered ECharts). Covers the pure <see cref="L.ChartAccessibility"/> projection
/// plus the Chart's rendered SR table + keyboard-focusable host.
/// </summary>
public class ChartAccessibilityTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        var v = typeof(ComponentInteropService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(ComponentInteropService).Assembly.GetName().Version?.ToString()
            ?? "0";
        var module = _ctx.JSInterop.SetupModule($"./_content/Lumeo.Charts/js/echarts-interop.js?v={v}");
        module.Mode = Bunit.JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string Cartesian =
        "{\"xAxis\":[{\"type\":\"category\",\"data\":[\"Jan\",\"Feb\"]}]," +
        "\"series\":[{\"name\":\"Revenue\",\"type\":\"bar\",\"data\":[10,20]}," +
        "{\"name\":\"Cost\",\"type\":\"bar\",\"data\":[5,8]}]}";

    private const string Pie =
        "{\"series\":[{\"type\":\"pie\",\"data\":[{\"name\":\"A\",\"value\":30},{\"name\":\"B\",\"value\":70}]}]}";

    // ECharts allows a single-series option to be written as an OBJECT rather than a
    // one-element array: "series": { ... } (round-9 finding).
    private const string SingleObjectPie =
        "{\"series\":{\"type\":\"pie\",\"data\":[{\"name\":\"A\",\"value\":30},{\"name\":\"B\",\"value\":70}]}}";

    private const string SingleObjectBar =
        "{\"xAxis\":{\"type\":\"category\",\"data\":[\"Jan\",\"Feb\"]}," +
        "\"series\":{\"name\":\"Revenue\",\"type\":\"bar\",\"data\":[10,20]}}";

    // ── Pure projection ──────────────────────────────────────────────────────

    [Fact]
    public void Build_Cartesian_Produces_Category_Rows_And_Series_Columns()
    {
        var t = L.ChartAccessibility.Build(Cartesian);

        Assert.NotNull(t);
        Assert.Equal(new[] { "Category", "Revenue", "Cost" }, t!.ColumnHeaders);
        Assert.Equal(2, t.Rows.Count);
        // Row = category; cells = each series' value at that category index.
        Assert.Equal("Jan", t.Rows[0].Header);
        Assert.Equal(new[] { "10", "5" }, t.Rows[0].Cells);
        Assert.Equal("Feb", t.Rows[1].Header);
        Assert.Equal(new[] { "20", "8" }, t.Rows[1].Cells);
        Assert.Contains("2 series", t.Summary);
        Assert.Contains("2 categories", t.Summary);
    }

    [Fact]
    public void Build_Pie_Produces_Name_Value_Rows()
    {
        var t = L.ChartAccessibility.Build(Pie);

        Assert.NotNull(t);
        Assert.Equal(new[] { "Name", "Value" }, t!.ColumnHeaders);
        Assert.Equal("A", t.Rows[0].Header);
        Assert.Equal(new[] { "30" }, t.Rows[0].Cells);
        Assert.Equal("B", t.Rows[1].Header);
        Assert.Equal(new[] { "70" }, t.Rows[1].Cells);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("not json at all")]
    [InlineData("{\"series\":[]}")]
    public void Build_Returns_Null_When_No_Usable_Data(string? json)
    {
        Assert.Null(L.ChartAccessibility.Build(json));
    }

    [Fact]
    public void Build_SingleObject_Pie_Series_Produces_Table_And_Caption()
    {
        // Round-9: the object form of "series" must project the same table as the
        // one-element-array form (previously it yielded null → no SR table/label).
        var t = L.ChartAccessibility.Build(SingleObjectPie);

        Assert.NotNull(t);
        Assert.Equal(new[] { "Name", "Value" }, t!.ColumnHeaders);
        Assert.Equal("A", t.Rows[0].Header);
        Assert.Equal(new[] { "30" }, t.Rows[0].Cells);
        Assert.Equal("B", t.Rows[1].Header);
        Assert.Equal(new[] { "70" }, t.Rows[1].Cells);
        // The caption/summary is non-empty and describes the pie — proves the
        // single-object series was picked up, not silently dropped.
        Assert.Contains("Pie chart", t.Summary);
        Assert.Contains("2 data points", t.Summary);
    }

    [Fact]
    public void Build_SingleObject_Bar_Series_Projects_Cartesian_Rows()
    {
        // Object-form series combined with an object-form axis (also single, not an
        // array) must still yield the cartesian category rows.
        var t = L.ChartAccessibility.Build(SingleObjectBar);

        Assert.NotNull(t);
        Assert.Equal(new[] { "Category", "Revenue" }, t!.ColumnHeaders);
        Assert.Equal("Jan", t.Rows[0].Header);
        Assert.Equal(new[] { "10" }, t.Rows[0].Cells);
        Assert.Equal("Feb", t.Rows[1].Header);
        Assert.Equal(new[] { "20" }, t.Rows[1].Cells);
    }

    [Fact]
    public void Chart_Renders_Hidden_Table_For_SingleObject_Series()
    {
        // End-to-end: the rendered Chart must expose the SR table for the object form
        // too, not just the array form.
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, SingleObjectPie)
            .Add(x => x.ShowLoadingSkeleton, false));

        Assert.NotNull(cut.Find("table.sr-only"));
        Assert.Contains(cut.FindAll("th"), th => th.TextContent == "A");
    }

    // ── Rendered accessibility layer ─────────────────────────────────────────

    [Fact]
    public void Chart_Renders_Hidden_Data_Table_By_Default()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, Cartesian)
            .Add(x => x.ShowLoadingSkeleton, false));

        var table = cut.Find("table.sr-only");
        Assert.NotNull(table);
        // Row header cell proves the series data was projected into the DOM.
        Assert.Contains(cut.FindAll("th"), th => th.TextContent == "Jan");
        Assert.Contains("Revenue", cut.Find("caption").TextContent);
    }

    [Fact]
    public void Chart_Host_Is_Keyboard_Focusable_With_Summary_Label()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, Cartesian)
            .Add(x => x.ShowLoadingSkeleton, false));

        var host = cut.Find(".lumeo-chart-host");
        Assert.Equal("0", host.GetAttribute("tabindex"));
        Assert.Equal("img", host.GetAttribute("role"));
        Assert.False(string.IsNullOrEmpty(host.GetAttribute("aria-label")));
    }

    [Fact]
    public void Explicit_AriaLabel_Wins_On_Host_But_Table_Still_Renders()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, Cartesian)
            .Add(x => x.AriaLabel, "Quarterly revenue")
            .Add(x => x.ShowLoadingSkeleton, false));

        Assert.Equal("Quarterly revenue", cut.Find(".lumeo-chart-host").GetAttribute("aria-label"));
        Assert.NotNull(cut.Find("table.sr-only"));
    }

    [Fact]
    public void AccessibilityLayer_False_Removes_Table_And_Focus_Stop()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, Cartesian)
            .Add(x => x.AccessibilityLayer, false)
            .Add(x => x.ShowLoadingSkeleton, false));

        Assert.Empty(cut.FindAll("table.sr-only"));
        Assert.Null(cut.Find(".lumeo-chart-host").GetAttribute("tabindex"));
    }

    // ── Row cap for big series (Codex P2 — unbounded a11y table) ──────────────

    // A cartesian bar with `count` categories on a single series → `count` rows.
    private static string BigCartesian(int count)
    {
        var cats = string.Join(",", Enumerable.Range(0, count).Select(i => $"\"C{i}\""));
        var data = string.Join(",", Enumerable.Range(0, count));
        return "{\"xAxis\":[{\"type\":\"category\",\"data\":[" + cats + "]}],"
             + "\"series\":[{\"name\":\"Revenue\",\"type\":\"bar\",\"data\":[" + data + "]}]}";
    }

    [Fact]
    public void Build_Caps_Big_Series_Rows_And_Notes_The_Remainder()
    {
        const int total = 120;
        var t = L.ChartAccessibility.Build(BigCartesian(total));

        Assert.NotNull(t);
        // Rows are capped to the default; the overflow is announced in TruncationNote.
        Assert.Equal(L.ChartAccessibility.DefaultMaxAccessibilityRows, t!.Rows.Count);
        var omitted = total - L.ChartAccessibility.DefaultMaxAccessibilityRows;
        Assert.NotNull(t.TruncationNote);
        Assert.Contains($"{omitted} more data points", t.TruncationNote!);
        // Kept rows are the FIRST N (deterministic head), not an arbitrary slice.
        Assert.Equal("C0", t.Rows[0].Header);
        Assert.Equal("C49", t.Rows[^1].Header);
        // The caption/summary keeps the REAL total, never the capped count.
        Assert.Contains($"{total} categories", t.Summary);
    }

    [Fact]
    public void Build_Small_Series_Is_Not_Truncated()
    {
        var t = L.ChartAccessibility.Build(BigCartesian(10));

        Assert.NotNull(t);
        Assert.Equal(10, t!.Rows.Count);
        Assert.Null(t.TruncationNote);
    }

    [Fact]
    public void Build_MaxRows_Zero_Disables_The_Cap()
    {
        var t = L.ChartAccessibility.Build(BigCartesian(120), maxRows: 0);

        Assert.NotNull(t);
        Assert.Equal(120, t!.Rows.Count);
        Assert.Null(t.TruncationNote);
    }

    [Fact]
    public void Chart_Renders_Capped_Table_With_Overflow_Row()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, BigCartesian(120))
            .Add(x => x.ShowLoadingSkeleton, false));

        // Default cap → 50 data rows + 1 overflow row.
        var bodyRows = cut.FindAll("tbody tr");
        Assert.Equal(L.ChartAccessibility.DefaultMaxAccessibilityRows + 1, bodyRows.Count);
        Assert.Contains("more data points", bodyRows[^1].TextContent);
        // aria-label / caption still reflect the full 120 categories.
        Assert.Contains("120 categories", cut.Find("caption").TextContent);
        Assert.Contains("120 categories", cut.Find(".lumeo-chart-host").GetAttribute("aria-label") ?? "");
    }

    [Fact]
    public void Chart_MaxAccessibilityRows_Zero_Renders_Every_Row()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OptionJson, BigCartesian(120))
            .Add(x => x.MaxAccessibilityRows, 0)
            .Add(x => x.ShowLoadingSkeleton, false));

        // No cap → 120 data rows, no overflow marker.
        Assert.Equal(120, cut.FindAll("tbody tr").Count);
        Assert.DoesNotContain("more data points", cut.Find("table.sr-only").TextContent);
    }
}
