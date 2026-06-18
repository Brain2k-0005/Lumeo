using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.A11yPolish;

/// <summary>
/// Covers the a11y/i18n polish batch:
///   #297 Hero / #298 CTASection / #299 FeatureGrid — the &lt;section&gt; landmark
///     gains an accessible name via aria-labelledby -> its heading.
///   #281 Skeleton / #282 Spinner — the screen-reader label is localizable.
///   #278 RingProgress — aria-valuenow is clamped to [valuemin, valuemax].
/// </summary>
public class A11yPolishTests
{
    private static BunitContext NewCtx()
    {
        var ctx = new BunitContext();
        ctx.AddLumeoServices();
        return ctx;
    }

    [Fact]
    public void CTASection_section_is_labelled_by_its_heading()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.CTASection>(p => p.Add(x => x.Title, "Get started"));

        var section = cut.Find("section");
        var h2 = cut.Find("h2");
        var labelledBy = section.GetAttribute("aria-labelledby");

        Assert.False(string.IsNullOrEmpty(labelledBy));
        Assert.Equal(h2.Id, labelledBy);
    }

    [Fact]
    public void FeatureGrid_section_is_labelled_by_its_heading()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.FeatureGrid>(p => p.Add(x => x.Title, "Features"));

        var section = cut.Find("section");
        var h2 = cut.Find("h2");

        Assert.Equal(h2.Id, section.GetAttribute("aria-labelledby"));
        Assert.False(string.IsNullOrEmpty(h2.Id));
    }

    [Fact]
    public void Hero_section_is_labelled_by_its_heading()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Hero>(p => p.Add(x => x.Title, "Build faster"));

        var section = cut.Find("section");
        var h1 = cut.Find("h1");

        Assert.Equal(h1.Id, section.GetAttribute("aria-labelledby"));
        Assert.False(string.IsNullOrEmpty(h1.Id));
    }

    [Fact]
    public void CTASection_without_title_has_no_dangling_labelledby()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.CTASection>();

        // No heading id to point at -> attribute must be absent, not "".
        Assert.False(cut.Find("section").HasAttribute("aria-labelledby"));
    }

    [Fact]
    public void Spinner_uses_localizable_aria_label()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Spinner>(p => p.Add(x => x.AriaLabel, "Lädt"));

        Assert.Equal("Lädt", cut.Find("[role='status']").GetAttribute("aria-label"));
    }

    [Fact]
    public void Spinner_defaults_aria_label_to_loading()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Spinner>();

        Assert.Equal("Loading", cut.Find("[role='status']").GetAttribute("aria-label"));
    }

    [Fact]
    public void Skeleton_uses_localizable_aria_label()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Skeleton>(p => p.Add(x => x.AriaLabel, "Lädt"));

        Assert.Equal("Lädt", cut.Find("[role='status']").GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData(150, "100")]
    [InlineData(-10, "0")]
    [InlineData(42.6, "43")]
    public void RingProgress_clamps_and_rounds_aria_valuenow(double value, string expected)
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.RingProgress>(p => p.Add(x => x.Value, value));

        var bar = cut.Find("[role='progressbar']");
        Assert.Equal(expected, bar.GetAttribute("aria-valuenow"));
        Assert.Equal("0", bar.GetAttribute("aria-valuemin"));
        Assert.Equal("100", bar.GetAttribute("aria-valuemax"));
    }
}
