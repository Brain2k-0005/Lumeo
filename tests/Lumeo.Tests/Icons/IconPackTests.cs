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

    // --- Heroicons: outline (stroke 1.5) + solid / mini (20px) / micro (16px) fills, ONE package ---

    [Fact]
    public void Heroicons_Outline_Samples_Are_Stroke_And_NonEmpty()
    {
        AssertSample(Heroicons.Home, L.IconRenderStyle.Stroke);
        AssertSample(Heroicons.Heart, L.IconRenderStyle.Stroke);
        AssertSample(Heroicons.Check, L.IconRenderStyle.Stroke);
    }

    [Fact]
    // Heroicons outline strokes at 1.5, not the Lucide/Tabler default of 2.
    public void Heroicons_Outline_Strokes_At_1_5() => Assert.Equal(1.5, Heroicons.Home.StrokeWidth);

    [Fact]
    public void Heroicons_Solid_Variants_Are_Fill_And_NonEmpty()
    {
        AssertSample(HeroiconsSolid.Home, L.IconRenderStyle.Fill);
        AssertSample(HeroiconsMini.Heart, L.IconRenderStyle.Fill);
        AssertSample(HeroiconsMicro.Check, L.IconRenderStyle.Fill);
    }

    [Fact]
    public void Heroicons_Mini_And_Micro_Carry_Native_ViewBoxes()
    {
        Assert.Equal("0 0 24 24", HeroiconsSolid.Home.ViewBox);
        Assert.Equal("0 0 20 20", HeroiconsMini.Home.ViewBox);
        Assert.Equal("0 0 16 16", HeroiconsMicro.Home.ViewBox);
    }

    [Fact]
    // The solid sets rely on fill-rule/clip-rule="evenodd"; the SVG parser must preserve them.
    public void Heroicons_Solid_Preserves_FillRule_EvenOdd() =>
        Assert.Contains("fill-rule=\"evenodd\"", HeroiconsMini.Home.Content, StringComparison.Ordinal);

    [Theory]
    [InlineData(typeof(Heroicons))]
    [InlineData(typeof(HeroiconsSolid))]
    [InlineData(typeof(HeroiconsMini))]
    [InlineData(typeof(HeroiconsMicro))]
    public void Heroicons_Each_Variant_Has_The_Full_Set(Type variant) => Assert.True(IconCount(variant) >= 300);

    // --- RemixIcon: '-line' → Remix and '-fill' → RemixFilled, both fill-rendered, ONE package ---

    [Fact]
    public void Remix_Line_Samples_Are_Fill_And_NonEmpty()
    {
        AssertSample(Remix.Home, L.IconRenderStyle.Fill);
        AssertSample(Remix.Heart, L.IconRenderStyle.Fill);
        AssertSample(Remix.Check, L.IconRenderStyle.Fill);
    }

    [Fact]
    public void RemixFilled_Samples_Are_Fill_And_NonEmpty()
    {
        AssertSample(RemixFilled.Home, L.IconRenderStyle.Fill);
        AssertSample(RemixFilled.Heart, L.IconRenderStyle.Fill);
        AssertSample(RemixFilled.Check, L.IconRenderStyle.Fill);
    }

    // RemixIcon v4.9.1 ships 1,539 designs per style (line + fill). The design-spec's ">=2900 per
    // style" floor predates the pinned release — that figure is the combined ~3,000 total, not a
    // per-style count; assert against the real per-style floor.
    [Fact]
    public void Remix_Line_Has_The_Full_Set() => Assert.True(IconCount(typeof(Remix)) >= 1500);

    [Fact]
    public void Remix_Fill_Has_The_Full_Set() => Assert.True(IconCount(typeof(RemixFilled)) >= 1500);

    // --- Bootstrap Icons: flat 16px fill set; the '-fill' suffix is kept as part of the name ---

    [Fact]
    public void Bootstrap_Samples_Are_Fill_And_NonEmpty()
    {
        AssertSample(Bootstrap.Bell, L.IconRenderStyle.Fill);
        AssertSample(Bootstrap.Heart, L.IconRenderStyle.Fill);
        AssertSample(Bootstrap.Check, L.IconRenderStyle.Fill);
    }

    [Fact]
    // Bootstrap's '-fill' marks a DISTINCT icon (bell vs bell-fill), so the suffix stays on the member.
    public void Bootstrap_Keeps_Fill_Suffix_In_Member_Name()
    {
        AssertSample(Bootstrap.BellFill, L.IconRenderStyle.Fill);
        AssertSample(Bootstrap.HeartFill, L.IconRenderStyle.Fill);
    }

    [Fact]
    public void Bootstrap_ViewBox_Is_16() => Assert.Equal("0 0 16 16", Bootstrap.Bell.ViewBox);

    [Fact]
    public void Bootstrap_Has_The_Full_Set() => Assert.True(IconCount(typeof(Bootstrap)) >= 1900);

    // --- Iconoir: stroke set, 24px, stroke width 1.5 ---

    [Fact]
    public void Iconoir_Samples_Are_Stroke_And_NonEmpty()
    {
        AssertSample(Iconoir.Home, L.IconRenderStyle.Stroke);
        AssertSample(Iconoir.Heart, L.IconRenderStyle.Stroke);
        AssertSample(Iconoir.Check, L.IconRenderStyle.Stroke);
    }

    [Fact]
    public void Iconoir_Strokes_At_1_5() => Assert.Equal(1.5, Iconoir.Home.StrokeWidth);

    [Fact]
    public void Iconoir_Has_The_Full_Set() => Assert.True(IconCount(typeof(Iconoir)) >= 1300);

    // --- bUnit render through the first-party SvgGlyph (one per new pack) ---

    [Fact]
    public void Heroicons_Outline_Renders_As_Stroked_Svg_At_1_5()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p.Add(g => g.Svg, Heroicons.Home));
        var svg = cut.Find("svg");
        Assert.Equal("0 0 24 24", svg.GetAttribute("viewBox"));
        Assert.Equal("none", svg.GetAttribute("fill"));
        Assert.Equal("currentColor", svg.GetAttribute("stroke"));
        Assert.Equal("1.5", svg.GetAttribute("stroke-width"));
        Assert.NotEmpty(svg.InnerHtml);
    }

    [Fact]
    public void HeroiconsMini_Renders_As_Filled_Svg_With_20_ViewBox()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p.Add(g => g.Svg, HeroiconsMini.Home));
        var svg = cut.Find("svg");
        Assert.Equal("0 0 20 20", svg.GetAttribute("viewBox"));
        Assert.Equal("currentColor", svg.GetAttribute("fill"));
        Assert.Null(svg.GetAttribute("stroke"));
        Assert.NotEmpty(svg.InnerHtml);
    }

    [Fact]
    public void Remix_Home_Renders_As_Filled_Svg()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p.Add(g => g.Svg, Remix.Home));
        var svg = cut.Find("svg");
        Assert.Equal("0 0 24 24", svg.GetAttribute("viewBox"));
        Assert.Equal("currentColor", svg.GetAttribute("fill"));
        Assert.NotEmpty(svg.InnerHtml);
    }

    [Fact]
    public void Bootstrap_Bell_Renders_As_Filled_Svg_With_16_ViewBox()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p.Add(g => g.Svg, Bootstrap.Bell));
        var svg = cut.Find("svg");
        Assert.Equal("0 0 16 16", svg.GetAttribute("viewBox"));
        Assert.Equal("currentColor", svg.GetAttribute("fill"));
        Assert.NotEmpty(svg.InnerHtml);
    }

    [Fact]
    public void Iconoir_Home_Renders_As_Stroked_Svg_At_1_5()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p.Add(g => g.Svg, Iconoir.Home));
        var svg = cut.Find("svg");
        Assert.Equal("0 0 24 24", svg.GetAttribute("viewBox"));
        Assert.Equal("none", svg.GetAttribute("fill"));
        Assert.Equal("currentColor", svg.GetAttribute("stroke"));
        Assert.Equal("1.5", svg.GetAttribute("stroke-width"));
        Assert.NotEmpty(svg.InnerHtml);
    }

    // --- Material Symbols STANDARD CUT: outlined / rounded / sharp, each a base + *Filled class ---
    // Weight-400 only; all fill-rendered; the native 0 -960 960 960 viewBox is preserved (SvgGlyph
    // scales via viewBox). Material's heart icon is `favorite`. Each style ships 3,892 icons per class
    // (@material-symbols/svg-400 v0.45.5) — the design-spec's >=3000 floor is set just under the actual.

    [Fact]
    public void MaterialSymbols_Samples_Are_Fill_And_NonEmpty()
    {
        AssertSample(MaterialSymbols.Home, L.IconRenderStyle.Fill);
        AssertSample(MaterialSymbols.Check, L.IconRenderStyle.Fill);
        AssertSample(MaterialSymbols.Favorite, L.IconRenderStyle.Fill);
        AssertSample(MaterialSymbolsFilled.Home, L.IconRenderStyle.Fill);
        AssertSample(MaterialSymbolsRounded.Home, L.IconRenderStyle.Fill);
        AssertSample(MaterialSymbolsRoundedFilled.Favorite, L.IconRenderStyle.Fill);
        AssertSample(MaterialSymbolsSharp.Check, L.IconRenderStyle.Fill);
        AssertSample(MaterialSymbolsSharpFilled.Home, L.IconRenderStyle.Fill);
    }

    [Fact]
    // Material's native viewBox is 0 -960 960 960 (NOT 0 0 24 24) — it must survive parse/emit verbatim.
    public void MaterialSymbols_Preserve_Native_ViewBox()
    {
        Assert.Equal("0 -960 960 960", MaterialSymbols.Home.ViewBox);
        Assert.Equal("0 -960 960 960", MaterialSymbolsFilled.Home.ViewBox);
        Assert.Equal("0 -960 960 960", MaterialSymbolsRounded.Home.ViewBox);
        Assert.Equal("0 -960 960 960", MaterialSymbolsSharp.Home.ViewBox);
    }

    [Theory]
    [InlineData(typeof(MaterialSymbols))]
    [InlineData(typeof(MaterialSymbolsFilled))]
    [InlineData(typeof(MaterialSymbolsRounded))]
    [InlineData(typeof(MaterialSymbolsRoundedFilled))]
    [InlineData(typeof(MaterialSymbolsSharp))]
    [InlineData(typeof(MaterialSymbolsSharpFilled))]
    // Actual: 3,892 per class (weight 400, v0.45.5). Floor set just below.
    public void MaterialSymbols_Each_Class_Has_The_Full_Set(Type cls) => Assert.True(IconCount(cls) >= 3800);

    [Fact]
    public void MaterialSymbols_Home_Renders_As_Filled_Svg_With_Native_ViewBox()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p.Add(g => g.Svg, MaterialSymbols.Home));
        var svg = cut.Find("svg");
        Assert.Equal("0 -960 960 960", svg.GetAttribute("viewBox"));
        Assert.Equal("currentColor", svg.GetAttribute("fill"));
        Assert.Null(svg.GetAttribute("stroke"));
        Assert.NotEmpty(svg.InnerHtml);
    }

    // --- Fluent STANDARD CUT: 24px regular (Fluent) + filled (FluentFilled), ONE package ---
    // Fill-rendered, native 24x24 viewBox. Fluent's check icon is `checkmark`. @fluentui/svg-icons
    // v1.1.331 ships 2,449 regular + 2,485 filled at 24px — the design-spec's >=2000 floor sits under both.

    [Fact]
    public void Fluent_Samples_Are_Fill_And_NonEmpty()
    {
        AssertSample(Fluent.Home, L.IconRenderStyle.Fill);
        AssertSample(Fluent.Heart, L.IconRenderStyle.Fill);
        AssertSample(Fluent.Checkmark, L.IconRenderStyle.Fill);
        AssertSample(FluentFilled.Home, L.IconRenderStyle.Fill);
        AssertSample(FluentFilled.Heart, L.IconRenderStyle.Fill);
        AssertSample(FluentFilled.Checkmark, L.IconRenderStyle.Fill);
    }

    [Fact]
    public void Fluent_ViewBox_Is_24() => Assert.Equal("0 0 24 24", Fluent.Home.ViewBox);

    [Fact]
    public void Fluent_Has_The_Full_Set() => Assert.True(IconCount(typeof(Fluent)) >= 2400);

    [Fact]
    public void Fluent_Filled_Has_A_Full_Set() => Assert.True(IconCount(typeof(FluentFilled)) >= 2400);

    [Fact]
    public void Fluent_Home_Renders_As_Filled_Svg_With_24_ViewBox()
    {
        var cut = _ctx.Render<L.SvgGlyph>(p => p.Add(g => g.Svg, Fluent.Home));
        var svg = cut.Find("svg");
        Assert.Equal("0 0 24 24", svg.GetAttribute("viewBox"));
        Assert.Equal("currentColor", svg.GetAttribute("fill"));
        Assert.Null(svg.GetAttribute("stroke"));
        Assert.NotEmpty(svg.InnerHtml);
    }
}
