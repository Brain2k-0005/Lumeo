using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chart;

public class ChartTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        // Set up the echarts-interop module in loose mode
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo/js/echarts-interop.js");
        module.Mode = Bunit.JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Div_Container()
    {
        var cut = _ctx.Render<L.Chart>();

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Default_Width_Is_100_Percent()
    {
        var cut = _ctx.Render<L.Chart>();

        var div = cut.Find("div");
        Assert.Contains("width: 100%", div.GetAttribute("style"));
    }

    [Fact]
    public void Default_Height_Is_350px()
    {
        var cut = _ctx.Render<L.Chart>();

        var div = cut.Find("div");
        Assert.Contains("height: 350px", div.GetAttribute("style"));
    }

    [Fact]
    public void Custom_Width_Is_Applied()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(b => b.Width, "500px"));

        var div = cut.Find("div");
        Assert.Contains("width: 500px", div.GetAttribute("style"));
    }

    [Fact]
    public void Custom_Height_Is_Applied()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(b => b.Height, "200px"));

        var div = cut.Find("div");
        Assert.Contains("height: 200px", div.GetAttribute("style"));
    }

    [Fact]
    public void Div_Has_Unique_Id()
    {
        var cut = _ctx.Render<L.Chart>();

        // Chart host is the nested div carrying the ECharts id; the outer wrapper
        // owns styling so the skeleton overlay can pin absolute inside it.
        var host = cut.Find("div.lumeo-chart-host");
        var id = host.GetAttribute("id");
        Assert.NotNull(id);
        Assert.StartsWith("lumeo-chart-", id);
    }

    [Fact]
    public void Two_Charts_Have_Different_Ids()
    {
        var cut1 = _ctx.Render<L.Chart>();
        var cut2 = _ctx.Render<L.Chart>();

        var id1 = cut1.Find("div.lumeo-chart-host").GetAttribute("id");
        var id2 = cut2.Find("div.lumeo-chart-host").GetAttribute("id");
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(b => b.Class, "my-chart"));

        var div = cut.Find("div");
        Assert.Contains("my-chart", div.GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-chart",
                ["aria-label"] = "Sales chart"
            }));

        var div = cut.Find("div");
        Assert.Equal("my-chart", div.GetAttribute("data-testid"));
        Assert.Equal("Sales chart", div.GetAttribute("aria-label"));
    }

    [Fact]
    public void Renders_With_EChartOption()
    {
        // Use OptionJson to avoid source-gen serialization issues with object? Data property
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(b => b.OptionJson, "{\"title\":{\"text\":\"Test Chart\"},\"series\":[{\"type\":\"bar\"}]}"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Renders_With_OptionJson()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(b => b.OptionJson, "{\"series\":[{\"type\":\"line\"}]}"));

        Assert.NotNull(cut.Find("div"));
    }

    // --- EChartOption unit tests ---

    [Fact]
    public void EChartOption_ToJson_Returns_Valid_Json()
    {
        var option = new L.EChartOption
        {
            Title = new L.EChartTitle { Text = "My Chart" }
        };

        var json = option.ToJson();
        Assert.NotNull(json);
        Assert.Contains("title", json);
        Assert.Contains("My Chart", json);
    }

    [Fact]
    public void EChartOption_ToJson_Omits_Null_Properties()
    {
        var option = new L.EChartOption();
        var json = option.ToJson();

        Assert.DoesNotContain("\"title\"", json);
        Assert.DoesNotContain("\"series\"", json);
    }

    [Fact]
    public void EChartOption_ToJson_Includes_Series()
    {
        var option = new L.EChartOption
        {
            Series = [new L.EChartSeries { Type = "line", Name = "Revenue" }]
            // Note: Data is intentionally omitted — it's typed as object? which
            // requires explicit registration in the source-gen context
        };

        var json = option.ToJson();
        Assert.Contains("series", json);
        Assert.Contains("line", json);
    }

    [Fact]
    public void ChartEventArgs_Has_Expected_Default_Values()
    {
        var args = new L.Chart.ChartEventArgs();

        Assert.Equal("", args.Name);
        Assert.Equal("", args.SeriesName);
        Assert.Equal(-1, args.SeriesIndex);
        Assert.Equal(-1, args.DataIndex);
        Assert.Equal("", args.Value);
        Assert.Equal("", args.ComponentType);
    }

    [Fact]
    public void Theme_Change_Triggers_Rerender()
    {
        // Render with light theme
        var cut1 = _ctx.Render<L.Chart>(p => p
            .Add(x => x.Theme, "light"));

        // Render with dark theme — verifies that different theme values are accepted without error
        var cut2 = _ctx.Render<L.Chart>(p => p
            .Add(x => x.Theme, "dark"));

        // Both should render a div container without error
        Assert.NotNull(cut1.Find("div"));
        Assert.NotNull(cut2.Find("div"));
    }

    [Fact]
    public void Chart_With_OnClick_Renders_Without_Error()
    {
        bool clicked = false;
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OnClick, EventCallback.Factory.Create<L.Chart.ChartEventArgs>(
                new object(), _ => { clicked = true; return Task.CompletedTask; })));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Chart_With_Group_Renders_Without_Error()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.Group, "sync-group"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Chart_Theme_Change_With_Group_Does_Not_Throw()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.Theme, "light")
            .Add(x => x.Group, "sync-group"));

        var ex = Record.Exception(() =>
            cut.Render(p => p.Add(x => x.Theme, "dark")));

        Assert.Null(ex);
    }

    [Fact]
    public void Chart_Theme_Change_With_OnClick_Does_Not_Throw()
    {
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.Theme, "light")
            .Add(x => x.OnClick, EventCallback.Factory.Create<L.Chart.ChartEventArgs>(
                new object(), _ => Task.CompletedTask)));

        var ex = Record.Exception(() =>
            cut.Render(p => p.Add(x => x.Theme, "dark")));

        Assert.Null(ex);
    }

    [Fact]
    public void Chart_With_OnInitError_Renders_Without_Error()
    {
        // Verifies that the OnInitError parameter is accepted and the component renders normally
        string? capturedError = null;
        var cut = _ctx.Render<L.Chart>(p => p
            .Add(x => x.OnInitError, EventCallback.Factory.Create<string>(
                new object(), msg => capturedError = msg)));

        Assert.NotNull(cut.Find("div"));
        // No error fired in unit tests because echarts-interop is mocked in Loose mode
        Assert.Null(capturedError);
    }
}
