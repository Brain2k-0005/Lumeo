using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Icon;

public class SvgGlyphTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SvgGlyphTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private const string StrokeContent = "<path d=\"M10 11v6\" /><path d=\"M3 6h18\" />";
    private const string FillContent = "<path d=\"M12 2 2 22h20z\" />";

    // --- Stroke render style ---

    [Fact]
    public void Stroke_Mode_Sets_Outline_Root_Attributes()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p
            .Add(g => g.Svg, L.IconSource.Stroke(StrokeContent)));
        var svg = cut.Find("svg");

        Assert.Equal("none", svg.GetAttribute("fill"));
        Assert.Equal("currentColor", svg.GetAttribute("stroke"));
        Assert.Equal("2", svg.GetAttribute("stroke-width"));
        Assert.Equal("round", svg.GetAttribute("stroke-linecap"));
        Assert.Equal("round", svg.GetAttribute("stroke-linejoin"));
    }

    [Fact]
    public void Stroke_Width_Honors_IconSource_Value()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p
            .Add(g => g.Svg, L.IconSource.Stroke(StrokeContent, strokeWidth: 1.75)));
        // Invariant formatting — never a localized comma.
        Assert.Equal("1.75", cut.Find("svg").GetAttribute("stroke-width"));
    }

    [Fact]
    public void Stroke_Width_Override_Param_Wins()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p
            .Add(g => g.Svg, L.IconSource.Stroke(StrokeContent, strokeWidth: 2))
            .Add(g => g.StrokeWidth, 1.5));
        Assert.Equal("1.5", cut.Find("svg").GetAttribute("stroke-width"));
    }

    // --- Fill render style ---

    [Fact]
    public void Fill_Mode_Sets_Solid_Root_Attributes()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p
            .Add(g => g.Svg, L.IconSource.Fill(FillContent)));
        var svg = cut.Find("svg");

        Assert.Equal("currentColor", svg.GetAttribute("fill"));
        // No stroke plumbing on a filled icon.
        Assert.Null(svg.GetAttribute("stroke"));
        Assert.Null(svg.GetAttribute("stroke-width"));
        Assert.Null(svg.GetAttribute("stroke-linecap"));
        Assert.Null(svg.GetAttribute("stroke-linejoin"));
    }

    [Fact]
    public void Fill_Mode_Ignores_Stroke_Width_Override()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p
            .Add(g => g.Svg, L.IconSource.Fill(FillContent))
            .Add(g => g.StrokeWidth, 4));
        Assert.Null(cut.Find("svg").GetAttribute("stroke-width"));
    }

    // --- viewBox + content ---

    [Fact]
    public void ViewBox_And_Inner_Content_Are_Rendered()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p
            .Add(g => g.Svg, L.IconSource.Stroke(StrokeContent, viewBox: "0 0 16 16")));
        var svg = cut.Find("svg");

        Assert.Equal("0 0 16 16", svg.GetAttribute("viewBox"));
        // Both inner paths made it into the DOM.
        Assert.Equal(2, svg.QuerySelectorAll("path").Length);
    }

    // --- attribute splat ---

    [Fact]
    public void Class_And_Aria_Splat_Onto_Svg_Root()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p
            .Add(g => g.Svg, L.IconSource.Stroke(StrokeContent))
            .Add(g => g.AdditionalAttributes, new Dictionary<string, object>
            {
                ["class"] = "h-4 w-4 text-red-500",
                ["aria-label"] = "Delete",
            }));
        var svg = cut.Find("svg");

        Assert.Contains("h-4", svg.GetAttribute("class"));
        Assert.Contains("w-4", svg.GetAttribute("class"));
        Assert.Equal("Delete", svg.GetAttribute("aria-label"));
    }

    // --- Class parameter (regression: SvgGlyph gets a real Class parameter) ---

    [Fact]
    public void Class_Parameter_Renders_Lowercase_Class_Attribute_Not_Class()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p
            .Add(g => g.Svg, L.IconSource.Stroke(StrokeContent))
            .Add(g => g.Class, "h-4 w-4"));
        var svg = cut.Find("svg");

        Assert.Equal("h-4 w-4", svg.GetAttribute("class"));
        // Case-sensitive DOM attribute: there must be no literal "Class" attribute on the svg.
        Assert.False(svg.HasAttribute("Class"));
    }

    [Fact]
    public void Lowercase_Class_Splat_Still_Works_Without_Class_Parameter()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p
            .Add(g => g.Svg, L.IconSource.Stroke(StrokeContent))
            .Add(g => g.AdditionalAttributes, new Dictionary<string, object>
            {
                ["class"] = "size-5",
            }));
        var svg = cut.Find("svg");

        Assert.Equal("size-5", svg.GetAttribute("class"));
    }

    [Fact]
    public void Class_Parameter_And_Splatted_Class_Are_Merged_Not_Clobbered()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p
            .Add(g => g.Svg, L.IconSource.Stroke(StrokeContent))
            .Add(g => g.Class, "text-red-500")
            .Add(g => g.AdditionalAttributes, new Dictionary<string, object>
            {
                ["class"] = "h-4 w-4",
            }));
        var svg = cut.Find("svg");

        Assert.Contains("text-red-500", svg.GetAttribute("class"));
        Assert.Contains("h-4", svg.GetAttribute("class"));
        Assert.Contains("w-4", svg.GetAttribute("class"));
    }

    [Fact]
    public void DropdownButton_Chevron_Renders_Class_Not_Class()
    {
        var cut = _ctx.Render<L.DropdownButton>(p => p
            .AddChildContent("Menu"));
        var svg = cut.Find("svg[data-slot='svg-glyph']");

        Assert.Contains("h-4", svg.GetAttribute("class"));
        Assert.Contains("w-4", svg.GetAttribute("class"));
        Assert.False(svg.HasAttribute("Class"));
    }

    [Fact]
    public void SplitButton_Chevron_Renders_Class_Not_Class()
    {
        var cut = _ctx.Render<L.SplitButton>(p => p
            .AddChildContent("Action"));
        var svg = cut.Find("svg[data-slot='svg-glyph']");

        Assert.Contains("h-4", svg.GetAttribute("class"));
        Assert.Contains("w-4", svg.GetAttribute("class"));
        Assert.False(svg.HasAttribute("Class"));
    }
}
