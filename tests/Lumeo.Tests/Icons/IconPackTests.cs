using System.Reflection;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Icons;
using L = Lumeo;

namespace Lumeo.Tests.Icons;

/// <summary>
/// Coverage for the Phase 1 icon packs (<c>Lumeo.Icons.*</c>): a handful of sample icons resolve to a
/// non-empty <see cref="L.IconSource"/> with the right <see cref="L.IconRenderStyle"/>, the vendored
/// set is at least as large as the pinned upstream release, and a sample icon renders through the
/// first-party <c>SvgGlyph</c> as a real <c>&lt;svg&gt;</c>.
/// </summary>
public class IconPackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public IconPackTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertSample(L.IconSource icon, L.IconRenderStyle style)
    {
        Assert.False(string.IsNullOrWhiteSpace(icon.Content), "icon Content must be non-empty");
        Assert.Contains("<", icon.Content, StringComparison.Ordinal); // real inner markup
        Assert.Equal(style, icon.RenderStyle);
    }

    // Counts the public static IconSource properties a pack class exposes.
    private static int IconCount(Type packClass) => packClass
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Count(p => p.PropertyType == typeof(L.IconSource));

    // --- Lucide (stroke) ---

    [Fact]
    public void Lucide_Samples_Are_Stroke_And_NonEmpty()
    {
        AssertSample(Lucide.House, L.IconRenderStyle.Stroke);
        AssertSample(Lucide.Check, L.IconRenderStyle.Stroke);
        AssertSample(Lucide.Heart, L.IconRenderStyle.Stroke);
    }

    [Fact]
    public void Lucide_Has_The_Full_Set() => Assert.True(IconCount(typeof(Lucide)) >= 1500);

    // --- Tabler outline (stroke) + filled (fill) ---

    [Fact]
    public void Tabler_Samples_Are_Stroke_And_NonEmpty()
    {
        AssertSample(Tabler.Home, L.IconRenderStyle.Stroke);
        AssertSample(Tabler.Check, L.IconRenderStyle.Stroke);
        AssertSample(Tabler.Heart, L.IconRenderStyle.Stroke);
    }

    [Fact]
    public void TablerFilled_Samples_Are_Fill_And_NonEmpty()
    {
        AssertSample(TablerFilled.Home, L.IconRenderStyle.Fill);
        AssertSample(TablerFilled.Check, L.IconRenderStyle.Fill);
        AssertSample(TablerFilled.Heart, L.IconRenderStyle.Fill);
    }

    [Fact]
    public void Tabler_Outline_Has_The_Full_Set() => Assert.True(IconCount(typeof(Tabler)) >= 4500);

    [Fact]
    public void Tabler_Filled_Has_A_Full_Set() => Assert.True(IconCount(typeof(TablerFilled)) >= 1000);

    // --- Phosphor, every weight (fill) ---

    [Fact]
    public void Phosphor_Weight_Samples_Are_Fill_And_NonEmpty()
    {
        AssertSample(Phosphor.House, L.IconRenderStyle.Fill);
        AssertSample(PhosphorBold.House, L.IconRenderStyle.Fill);
        AssertSample(PhosphorFill.Check, L.IconRenderStyle.Fill);
        AssertSample(PhosphorDuotone.Heart, L.IconRenderStyle.Fill);
        AssertSample(PhosphorLight.House, L.IconRenderStyle.Fill);
        AssertSample(PhosphorThin.Check, L.IconRenderStyle.Fill);
    }

    [Theory]
    [InlineData(typeof(Phosphor))]
    [InlineData(typeof(PhosphorBold))]
    [InlineData(typeof(PhosphorFill))]
    [InlineData(typeof(PhosphorDuotone))]
    [InlineData(typeof(PhosphorLight))]
    [InlineData(typeof(PhosphorThin))]
    // Phosphor core v2.0.8 ships 1,248 icons per weight (the design-spec's ~1,500 estimate predates
    // the pinned release); assert against the real floor.
    public void Phosphor_Weight_Has_The_Full_Set(Type weight) => Assert.True(IconCount(weight) >= 1200);

    [Fact]
    public void Phosphor_ViewBox_Is_256() => Assert.Equal("0 0 256 256", Phosphor.House.ViewBox);

    [Fact]
    public void Phosphor_Duotone_Preserves_Two_Tone_Opacity() =>
        Assert.Contains("opacity=\"0.2\"", PhosphorDuotone.House.Content, StringComparison.Ordinal);

    // --- bUnit render through the first-party SvgGlyph ---

    [Fact]
    public void Tabler_Home_Renders_As_Svg_With_ViewBox()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p.Add(g => g.Svg, Tabler.Home));
        var svg = cut.Find("svg");
        Assert.Equal("0 0 24 24", svg.GetAttribute("viewBox"));
        Assert.Equal("none", svg.GetAttribute("fill"));           // stroke style
        Assert.Equal("currentColor", svg.GetAttribute("stroke"));
        Assert.NotEmpty(svg.InnerHtml);
    }

    [Fact]
    public void Phosphor_House_Renders_As_Filled_Svg()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p.Add(g => g.Svg, Phosphor.House));
        var svg = cut.Find("svg");
        Assert.Equal("0 0 256 256", svg.GetAttribute("viewBox"));
        Assert.Equal("currentColor", svg.GetAttribute("fill"));   // fill style
        Assert.Null(svg.GetAttribute("stroke"));
        Assert.NotEmpty(svg.InnerHtml);
    }
}
