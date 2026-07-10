using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Icon;

public class IconTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public IconTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Decorative-by-default a11y (#286) ---

    [Fact]
    public void Decorative_By_Default_Is_Aria_Hidden()
    {
        var cut = _ctx.Render<L.Icon>(p => p.Add(i => i.Name, "Search"));
        var svg = cut.Find("svg");
        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        Assert.Null(svg.GetAttribute("role"));
    }

    [Fact]
    public void Title_Promotes_To_Role_Img_With_Aria_Label()
    {
        var cut = _ctx.Render<L.Icon>(p => p
            .Add(i => i.Name, "Search")
            .Add(i => i.Title, "Search"));
        var svg = cut.Find("svg");
        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Equal("Search", svg.GetAttribute("aria-label"));
        // Not hidden when it has an accessible name.
        Assert.Null(svg.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Title_Also_Sets_Title_Attribute()
    {
        var cut = _ctx.Render<L.Icon>(p => p
            .Add(i => i.Name, "Search")
            .Add(i => i.Title, "Find things"));
        Assert.Equal("Find things", cut.Find("svg").GetAttribute("title"));
    }

    [Fact]
    public void Whitespace_Title_Is_Decorative()
    {
        // A whitespace-only Title carries no accessible name, so the icon must
        // fall back to decorative (aria-hidden) rather than role="img" with a
        // blank aria-label.
        var cut = _ctx.Render<L.Icon>(p => p
            .Add(i => i.Name, "Search")
            .Add(i => i.Title, "   "));
        var svg = cut.Find("svg");
        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        Assert.Null(svg.GetAttribute("role"));
    }

    [Fact]
    public void Consumer_Aria_Label_Wins_Over_Default_Hidden()
    {
        var cut = _ctx.Render<L.Icon>(p => p
            .Add(i => i.Name, "Search")
            .Add(i => i.AdditionalAttributes, new Dictionary<string, object>
            {
                ["aria-label"] = "Custom",
            }));
        var svg = cut.Find("svg");
        Assert.Equal("Custom", svg.GetAttribute("aria-label"));
        // We did NOT also stamp aria-hidden when the consumer provided a label.
        Assert.Null(svg.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Consumer_Aria_Hidden_Wins_Over_Title()
    {
        var cut = _ctx.Render<L.Icon>(p => p
            .Add(i => i.Name, "Search")
            .Add(i => i.Title, "ignored-because-consumer-hid-it")
            .Add(i => i.AdditionalAttributes, new Dictionary<string, object>
            {
                ["aria-hidden"] = "true",
            }));
        var svg = cut.Find("svg");
        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        // Our role=img default is suppressed because the consumer took control.
        Assert.Null(svg.GetAttribute("role"));
    }

    [Fact]
    public void Size_Class_Still_Applies_With_A11y()
    {
        var cut = _ctx.Render<L.Icon>(p => p
            .Add(i => i.Name, "Search")
            .Add(i => i.Size, L.Size.Lg));
        var cls = cut.Find("svg").GetAttribute("class") ?? "";
        Assert.Contains("h-5", cls);
        Assert.Contains("w-5", cls);
    }

    // --- IconSource rendering (dependency-decoupled) ---

    [Fact]
    public void Svg_IconSource_Renders_Through_SvgGlyph()
    {
        var source = L.IconSource.Stroke("<path d=\"M3 6h18\" />");
        var cut = _ctx.Render<L.Icon>(p => p.Add(i => i.Svg, source));
        var svg = cut.Find("svg");

        // Icon delegates rendering to SvgGlyph: outline root + the exact inner markup.
        Assert.Equal("none", svg.GetAttribute("fill"));
        Assert.Equal("currentColor", svg.GetAttribute("stroke"));
        Assert.Single(svg.QuerySelectorAll("path"));
    }

    [Fact]
    public void Svg_Takes_Precedence_Over_Name()
    {
        var source = L.IconSource.Stroke("<path d=\"M3 6h18\" />");
        var viaSvg = _ctx.Render<L.Icon>(p => p
            .Add(i => i.Name, "Trash2")   // ignored — Svg wins
            .Add(i => i.Svg, source)).Find("svg").InnerHtml;
        var direct = _ctx.Render<L.SvgGlyph>(p => p.Add(g => g.Svg, source)).Find("svg").InnerHtml;
        Assert.Equal(direct, viaSvg);
    }

    [Theory]
    [InlineData("Search")]
    [InlineData("ChevronDown")]
    [InlineData("TrendingUp")]
    public void Name_Vocabulary_Resolves_To_LumeoIcons(string name)
    {
        // The name maps to the matching LumeoIcons glyph; comparing inner markup keeps the test
        // decoupled from Lucide's exact path data (only that the mapping targets the right icon).
        var expected = name switch
        {
            "Search" => L.LumeoIcons.Search,
            "ChevronDown" => L.LumeoIcons.ChevronDown,
            "TrendingUp" => L.LumeoIcons.TrendingUp,
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        var viaName = _ctx.Render<L.Icon>(p => p.Add(i => i.Name, name)).Find("svg").InnerHtml;
        var viaSource = _ctx.Render<L.SvgGlyph>(p => p.Add(g => g.Svg, expected)).Find("svg").InnerHtml;
        Assert.Equal(viaSource, viaName);
        Assert.NotEqual(string.Empty, viaName);
    }

    [Fact]
    public void Unknown_Name_Falls_Back_To_Circle()
    {
        var viaName = _ctx.Render<L.Icon>(p => p.Add(i => i.Name, "NotARealIconName"))
            .Find("svg").InnerHtml;
        var circle = _ctx.Render<L.SvgGlyph>(p => p.Add(g => g.Svg, L.LumeoIcons.Circle))
            .Find("svg").InnerHtml;
        Assert.Equal(circle, viaName);
    }
}
