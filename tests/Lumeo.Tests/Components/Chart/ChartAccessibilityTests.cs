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
}
