using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Chart;

/// <summary>
/// Tests for the declarative <see cref="ChartTooltip"/> RenderFragment slot. Drives
/// the registration mechanics and the hidden portal-rendering path directly — the
/// JS-side formatter wiring is exercised by the chart's e2e suite, not bUnit.
/// </summary>
public class ChartTooltipSlotTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        var module = _ctx.JSInterop.SetupModule("./_content/Lumeo.Charts/js/echarts-interop.js");
        module.Mode = Bunit.JSRuntimeMode.Loose;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Registration_Set_And_Clear_Trigger_OnChanged()
    {
        var changeCount = 0;
        var reg = new ChartTooltipSlotRegistration(() => changeCount++);

        var info = new ChartTooltipSlotInfo(ctx => b => b.AddContent(0, "x"), Class: null);
        reg.Set(info);
        Assert.Same(info, reg.Current);
        Assert.Equal(1, changeCount);

        // Identical Set is a no-op to keep parameter-set re-renders from spinning.
        reg.Set(info);
        Assert.Equal(1, changeCount);

        reg.Clear();
        Assert.Null(reg.Current);
        Assert.Equal(2, changeCount);
    }

    [Fact]
    public void Chart_Without_ChartTooltip_Renders_No_Portal()
    {
        var cut = _ctx.Render<Lumeo.Chart>(p => p
            .Add(c => c.OptionJson, "{\"series\":[{\"type\":\"line\",\"data\":[1,2,3]}]}")
            .Add(c => c.ShowLoadingSkeleton, false));

        // No tooltip portal div in the markup.
        Assert.DoesNotContain("lumeo-chart-tooltip-portal", cut.Markup);
    }

    [Fact]
    public void ChartTooltip_Child_Renders_Hidden_Portal()
    {
        var cut = _ctx.Render<Lumeo.Chart>(p => p
            .Add(c => c.OptionJson, "{\"series\":[{\"type\":\"line\",\"data\":[1,2,3]}]}")
            .Add(c => c.ShowLoadingSkeleton, false)
            .Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<ChartTooltip>(0);
                b.AddAttribute(1, "ChildContent",
                    (RenderFragment<ChartTooltipContext>)(ctx => cb => cb.AddContent(0, $"hovered: {ctx.SeriesName}")));
                b.CloseComponent();
            })));

        var portal = cut.Find(".lumeo-chart-tooltip-portal");
        Assert.NotNull(portal);
        // display:none keeps it off-screen — ECharts pulls innerHTML, not display.
        Assert.Contains("display:none", portal.GetAttribute("style") ?? "");
        // Initial render uses ChartTooltipContext.Empty (empty SeriesName).
        Assert.Contains("hovered:", portal.TextContent);
    }

    [Fact]
    public void OnTooltipPointChange_Updates_Portal_Content()
    {
        var cut = _ctx.Render<Lumeo.Chart>(p => p
            .Add(c => c.OptionJson, "{\"series\":[{\"type\":\"line\",\"data\":[1,2,3]}]}")
            .Add(c => c.ShowLoadingSkeleton, false)
            .Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<ChartTooltip>(0);
                b.AddAttribute(1, "ChildContent",
                    (RenderFragment<ChartTooltipContext>)(ctx => cb =>
                        cb.AddContent(0, $"{ctx.SeriesName} = {ctx.Value?.ToString() ?? "-"}")));
                b.CloseComponent();
            })));

        // Simulate the JS-side hover notification.
        var payload = "{\"seriesName\":\"Sales\",\"seriesType\":\"line\",\"seriesIndex\":0,\"name\":\"Q1\",\"dataIndex\":0,\"value\":42.5,\"color\":\"#22c55e\"}";
        cut.Instance.OnTooltipPointChange(payload).GetAwaiter().GetResult();

        var portal = cut.Find(".lumeo-chart-tooltip-portal");
        Assert.Contains("Sales = 42.5", portal.TextContent);
    }

    [Fact]
    public void OnTooltipPointChange_Multi_Dim_Value_Picks_First_Dimension()
    {
        // scatter / candlestick send value as an array; the headline Value picks [0].
        var cut = _ctx.Render<Lumeo.Chart>(p => p
            .Add(c => c.OptionJson, "{\"series\":[{\"type\":\"scatter\",\"data\":[[1,10]]}]}")
            .Add(c => c.ShowLoadingSkeleton, false)
            .Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<ChartTooltip>(0);
                b.AddAttribute(1, "ChildContent",
                    (RenderFragment<ChartTooltipContext>)(ctx => cb =>
                        cb.AddContent(0, $"v={ctx.Value?.ToString() ?? "-"}")));
                b.CloseComponent();
            })));

        cut.Instance.OnTooltipPointChange("{\"seriesName\":\"\",\"value\":[7,99]}").GetAwaiter().GetResult();
        Assert.Contains("v=7", cut.Find(".lumeo-chart-tooltip-portal").TextContent);
    }

    [Fact]
    public void OnTooltipPointChange_Malformed_Json_Keeps_Last_Good_Context()
    {
        var cut = _ctx.Render<Lumeo.Chart>(p => p
            .Add(c => c.OptionJson, "{\"series\":[{\"type\":\"line\",\"data\":[1]}]}")
            .Add(c => c.ShowLoadingSkeleton, false)
            .Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<ChartTooltip>(0);
                b.AddAttribute(1, "ChildContent",
                    (RenderFragment<ChartTooltipContext>)(ctx => cb => cb.AddContent(0, $"name={ctx.DataName}")));
                b.CloseComponent();
            })));

        // First a good update to set state.
        cut.Instance.OnTooltipPointChange("{\"name\":\"Good\"}").GetAwaiter().GetResult();
        Assert.Contains("name=Good", cut.Find(".lumeo-chart-tooltip-portal").TextContent);

        // Now garbage — must not throw, must keep the previous good state.
        cut.Instance.OnTooltipPointChange("not json").GetAwaiter().GetResult();
        Assert.Contains("name=Good", cut.Find(".lumeo-chart-tooltip-portal").TextContent);
    }

    [Fact]
    public void ChartTooltip_Class_Applies_To_Portal_Wrapper()
    {
        var cut = _ctx.Render<Lumeo.Chart>(p => p
            .Add(c => c.OptionJson, "{\"series\":[{\"type\":\"line\",\"data\":[1]}]}")
            .Add(c => c.ShowLoadingSkeleton, false)
            .Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<ChartTooltip>(0);
                b.AddAttribute(1, "Class", "my-tooltip");
                b.AddAttribute(2, "ChildContent",
                    (RenderFragment<ChartTooltipContext>)(ctx => cb => cb.AddContent(0, "x")));
                b.CloseComponent();
            })));

        var portal = cut.Find(".lumeo-chart-tooltip-portal");
        Assert.Contains("my-tooltip", portal.GetAttribute("class") ?? "");
    }
}
