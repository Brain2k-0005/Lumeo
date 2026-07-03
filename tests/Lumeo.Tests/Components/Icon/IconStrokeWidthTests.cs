using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Icon;

/// <summary>
/// Tests for <see cref="Lumeo.Icon.StrokeWidth"/> passthrough to the inner <c>SvgGlyph</c>.
/// </summary>
public class IconStrokeWidthTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public IconStrokeWidthTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // A known Stroke-style icon (Lucide, default stroke-width 2).
    private static readonly IconSource StrokeIcon = IconSource.Stroke("<circle cx='12' cy='12' r='10'/>", strokeWidth: 2);

    // A known Fill-style icon.
    private static readonly IconSource FillIcon = IconSource.Fill("<circle cx='12' cy='12' r='10'/>");

    [Fact]
    public void StrokeWidth_Override_Renders_On_Svg()
    {
        var cut = _ctx.Render<Lumeo.Icon>(p => p
            .Add(x => x.Svg, StrokeIcon)
            .Add(x => x.StrokeWidth, 1.5));

        var svg = cut.Find("svg");
        Assert.Equal("1.5", svg.GetAttribute("stroke-width"));
    }

    [Fact]
    public void Default_StrokeWidth_Uses_IconSource_Value()
    {
        // StrokeIcon has StrokeWidth=2; not passing StrokeWidth on Icon → source default.
        var cut = _ctx.Render<Lumeo.Icon>(p => p
            .Add(x => x.Svg, StrokeIcon));

        var svg = cut.Find("svg");
        Assert.Equal("2", svg.GetAttribute("stroke-width"));
    }

    [Fact]
    public void Fill_Icon_Has_No_StrokeWidth_Attribute_Even_When_Override_Passed()
    {
        // SvgGlyph omits stroke-width entirely for Fill icons (stroke attr is null).
        // Icon.StrokeWidth is forwarded but SvgGlyph ignores it for Fill style.
        var cut = _ctx.Render<Lumeo.Icon>(p => p
            .Add(x => x.Svg, FillIcon)
            .Add(x => x.StrokeWidth, 1.5));

        var svg = cut.Find("svg");
        Assert.False(svg.HasAttribute("stroke-width"),
            "Fill-style icons must not render a stroke-width attribute");
    }
}
