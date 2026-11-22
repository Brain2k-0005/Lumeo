using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Icons;

/// <summary><see cref="L.IconSource.FromSvg"/> (#600): drop a complete SVG string into Icon/SvgGlyph.</summary>
public class IconSourceFromSvgTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public IconSourceFromSvgTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string IssueSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="1em" height="1em" viewBox="0 0 24 24">
          <path d="M0 0h24v24H0z" fill="none" />
          <path fill="currentColor" d="M12 3.5a3.75 3.75 0 1 0 0 7.5a3.75 3.75 0 0 0 0-7.5m-5 9a3.75 3.75 0 1 0 0 7.498A3.75 3.75 0 0 0 7 12.5m10 0a3.75 3.75 0 1 0 0 7.499a3.75 3.75 0 0 0 0-7.499" />
        </svg>
        """;

    [Fact]
    public void IssueSvg_IsFill_WithBothPaths()
    {
        var icon = L.IconSource.FromSvg(IssueSvg);
        Assert.Equal(L.IconRenderStyle.Fill, icon.RenderStyle);
        Assert.Equal("0 0 24 24", icon.ViewBox);
        Assert.Contains("""<path d="M0 0h24v24H0z" fill="none" />""", icon.Content);
        Assert.Contains("M12 3.5a3.75", icon.Content);
        Assert.StartsWith("<path", icon.Content);
        Assert.EndsWith("/>", icon.Content);
        Assert.DoesNotContain("<svg", icon.Content);
        Assert.DoesNotContain("</svg", icon.Content);
    }

    [Fact]
    public void LucideStyle_IsStroke_WithStrokeWidth()
    {
        var icon = L.IconSource.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"><path d="M13 2 3 14h9l-1 8 10-12h-9l1-8Z"/></svg>
            """);
        Assert.Equal(L.IconRenderStyle.Stroke, icon.RenderStyle);
        Assert.Equal(1.5, icon.StrokeWidth);
        Assert.Equal("""<path d="M13 2 3 14h9l-1 8 10-12h-9l1-8Z"/>""", icon.Content);
    }

    [Fact]
    public void StrokeWithoutStrokeWidth_DefaultsToTwo()
    {
        var icon = L.IconSource.FromSvg("""<svg fill="none" stroke="currentColor"><path d="M0 0"/></svg>""");
        Assert.Equal(L.IconRenderStyle.Stroke, icon.RenderStyle);
        Assert.Equal(2, icon.StrokeWidth);
    }

    [Fact]
    public void FillNone_WithStrokeNone_IsFill()
    {
        var icon = L.IconSource.FromSvg("""<svg fill="none" stroke="none"><path d="M0 0"/></svg>""");
        Assert.Equal(L.IconRenderStyle.Fill, icon.RenderStyle);
    }

    [Theory]
    [InlineData("""<svg width="16" height="20"><path d="M0 0"/></svg>""", "0 0 16 20")]
    [InlineData("""<svg width="16px" height="20.5px"><path d="M0 0"/></svg>""", "0 0 16 20.5")]
    [InlineData("""<svg width="1em" height="1em"><path d="M0 0"/></svg>""", "0 0 24 24")]
    [InlineData("""<svg><path d="M0 0"/></svg>""", "0 0 24 24")]
    public void MissingViewBox_FallsBack(string svg, string expected)
        => Assert.Equal(expected, L.IconSource.FromSvg(svg).ViewBox);

    [Fact]
    public void SelfClosing_HasEmptyContent()
    {
        var icon = L.IconSource.FromSvg("""<svg viewBox="0 0 16 16" />""");
        Assert.Equal("", icon.Content);
        Assert.Equal("0 0 16 16", icon.ViewBox);
        Assert.Equal("", L.IconSource.FromSvg("<svg/>").Content);
    }

    [Fact]
    public void SingleQuotes_AnyAttributeOrder_Namespaced()
    {
        var icon = L.IconSource.FromSvg("<svg xmlns:xlink='http://www.w3.org/1999/xlink' stroke-width='3' stroke='currentColor' viewBox='0 0 32 32' fill='none'><use xlink:href='#a'/></svg>");
        Assert.Equal("0 0 32 32", icon.ViewBox);
        Assert.Equal(L.IconRenderStyle.Stroke, icon.RenderStyle);
        Assert.Equal(3, icon.StrokeWidth);
        Assert.Equal("<use xlink:href='#a'/>", icon.Content);
    }

    [Fact]
    public void GreaterThanInsideAttributeValue_DoesNotEndTag()
    {
        var icon = L.IconSource.FromSvg("""<svg data-x="a>b" viewBox="0 0 8 8"><path d="M0 0"/></svg>""");
        Assert.Equal("0 0 8 8", icon.ViewBox);
        Assert.Equal("""<path d="M0 0"/>""", icon.Content);
    }

    [Fact]
    public void XmlDeclarationDoctypeAndComments_Tolerated()
    {
        var icon = L.IconSource.FromSvg("  \n<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!-- hi <svg> -->\n<!DOCTYPE svg>\n<svg viewBox=\"0 0 10 10\"><g><path d=\"M0 0\"/></g></svg>\n  ");
        Assert.Equal("0 0 10 10", icon.ViewBox);
        Assert.Equal("""<g><path d="M0 0"/></g>""", icon.Content);
    }

    [Fact]
    public void NestedSvg_KeptVerbatim()
    {
        var icon = L.IconSource.FromSvg("""<svg viewBox="0 0 8 8"><svg x="1"><path d="M0 0"/></svg></svg>""");
        Assert.Equal("""<svg x="1"><path d="M0 0"/></svg>""", icon.Content);
    }

    [Fact]
    public void Null_Throws() => Assert.Throws<ArgumentNullException>(() => L.IconSource.FromSvg(null!));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<div></div>")]
    [InlineData("just text")]
    [InlineData("<svgx viewBox=\"0 0 1 1\"></svgx>")]
    [InlineData("<svg viewBox=\"0 0 1 1\"><path/>")]
    [InlineData("<svg viewBox=\"0 0 1 1\"")]
    public void Invalid_Throws(string svg)
        => Assert.Throws<ArgumentException>(() => L.IconSource.FromSvg(svg));

    [Fact]
    public void RendersThroughSvgGlyph()
    {
        var icon = L.IconSource.FromSvg(IssueSvg);
        var cut = _ctx.Render<L.SvgGlyph>(p => p.Add(x => x.Svg, icon).Add(x => x.Class, "h-4 w-4"));
        var svg = cut.Find("svg");
        Assert.Equal("0 0 24 24", svg.GetAttribute("viewBox"));
        Assert.Equal("currentColor", svg.GetAttribute("fill"));
        Assert.Null(svg.GetAttribute("stroke"));
        Assert.Contains("h-4", svg.GetAttribute("class"));
        Assert.Equal(2, cut.FindAll("path").Count);
        Assert.Contains("M12 3.5a3.75", cut.Markup);
    }

    [Fact]
    public void RendersThroughIcon_Stroke()
    {
        var icon = L.IconSource.FromSvg("""<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M13 2 3 14"/></svg>""");
        var cut = _ctx.Render<L.Icon>(p => p.Add(x => x.Svg, icon));
        var svg = cut.Find("svg");
        Assert.Equal("none", svg.GetAttribute("fill"));
        Assert.Equal("currentColor", svg.GetAttribute("stroke"));
        Assert.Equal("1.5", svg.GetAttribute("stroke-width"));
        Assert.Single(cut.FindAll("path"));
    }
}
