using System.Collections.Generic;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Rendering;

public class ChartLegendTests
{
    private readonly BunitContext _ctx = new();

    private static readonly L.ChartLegendItem[] Items =
    {
        new("a", "Series A", "var(--color-chart-1)", "42"),
        new("b", "Series B", "var(--color-chart-2)"),
    };

    [Fact]
    public void Renders_One_Button_Per_Item_With_Its_Name()
    {
        var cut = _ctx.Render<L.ChartLegend>(p => p.Add(b => b.Items, Items));

        var buttons = cut.FindAll("button");
        Assert.Equal(2, buttons.Count);
        Assert.Contains("Series A", buttons[0].TextContent);
        Assert.Contains("Series B", buttons[1].TextContent);
    }

    [Fact]
    public void Value_Renders_When_Present_And_Omitted_When_Null()
    {
        var cut = _ctx.Render<L.ChartLegend>(p => p.Add(b => b.Items, Items));

        Assert.Contains("42", cut.FindAll("button")[0].TextContent);
        Assert.DoesNotContain("<b", cut.FindAll("button")[1].InnerHtml);
    }

    [Fact]
    public void Swatch_Background_Uses_The_Items_Color()
    {
        var cut = _ctx.Render<L.ChartLegend>(p => p.Add(b => b.Items, Items));

        var swatch = cut.Find("button .lumeo-chart-legend-swatch");
        Assert.Contains("var(--color-chart-1)", swatch.GetAttribute("style"));
    }

    [Fact]
    public void Hidden_Item_Renders_An_Outline_Only_Swatch()
    {
        var cut = _ctx.Render<L.ChartLegend>(p => p
            .Add(b => b.Items, Items)
            .Add(b => b.HiddenKeys, new[] { "a" }));

        var swatch = cut.Find("button .lumeo-chart-legend-swatch");
        Assert.Contains("background:transparent", swatch.GetAttribute("style"));
        Assert.Contains("box-shadow", swatch.GetAttribute("style"));
    }

    [Fact]
    public void Clicking_A_Swatch_Invokes_OnToggle_With_Its_Key()
    {
        string? toggled = null;
        var cut = _ctx.Render<L.ChartLegend>(p => p
            .Add(b => b.Items, Items)
            .Add(b => b.OnToggle, EventCallback.Factory.Create<string>(this, k => toggled = k)));

        cut.FindAll("button")[1].Click();

        Assert.Equal("b", toggled);
    }

    [Fact]
    public void Hovering_A_Swatch_Invokes_OnHover_With_Its_Key_Then_Null_On_Leave()
    {
        var hovered = new List<string?>();
        var cut = _ctx.Render<L.ChartLegend>(p => p
            .Add(b => b.Items, Items)
            .Add(b => b.OnHover, EventCallback.Factory.Create<string?>(this, k => hovered.Add(k))));

        var button = cut.FindAll("button")[0];
        button.MouseEnter();
        button.MouseLeave();

        Assert.Equal(new List<string?> { "a", null }, hovered);
    }

    [Fact]
    public void Non_Hovered_Item_Dims_When_Another_Item_Is_Hovered()
    {
        var cut = _ctx.Render<L.ChartLegend>(p => p
            .Add(b => b.Items, Items)
            .Add(b => b.HoveredKey, "a"));

        var buttons = cut.FindAll("button");
        Assert.Contains("opacity:1", buttons[0].GetAttribute("style"));
        Assert.Contains("opacity:0.45", buttons[1].GetAttribute("style"));
    }
}
