using Xunit;
using Lumeo.IconGen;

namespace Lumeo.Tests.Icons;

/// <summary>
/// Unit coverage for the pure IconGen transforms that drive the pack pipeline: the Phosphor weight
/// suffix-strip, the kebab→PascalCase name transform (incl. the leading-digit <c>_</c> prefix), and
/// the SVG parser's color scrub + child opacity/fill preservation (the duotone contract).
/// </summary>
public class IconGenTransformTests
{
    // --- Phosphor suffix-strip (weight lives in the class, not the member) ---

    [Theory]
    [InlineData("house-duotone", "duotone", "house")]
    [InlineData("address-book-bold", "bold", "address-book")]
    [InlineData("heart-fill", "fill", "heart")]
    [InlineData("arrows-in-line-vertical-thin", "thin", "arrows-in-line-vertical")]
    public void StripSuffix_Removes_Trailing_Weight(string name, string suffix, string expected) =>
        Assert.Equal(expected, NameTransform.StripSuffix(name, suffix));

    [Fact]
    public void StripSuffix_Leaves_NonMatching_Name_Untouched() =>
        Assert.Equal("house", NameTransform.StripSuffix("house", "bold"));

    [Fact]
    public void Phosphor_Bold_Pipeline_Yields_Base_PascalName() =>
        // The full phosphor transform: strip "-bold", then PascalCase → the plain base name.
        Assert.Equal("AddressBook", NameTransform.ToPascal(NameTransform.StripSuffix("address-book-bold", "bold")));

    // --- kebab → PascalCase, incl. digit handling ---

    [Theory]
    [InlineData("trash-2", "Trash2")]
    [InlineData("brand-github", "BrandGithub")]
    [InlineData("house", "House")]
    [InlineData("123", "_123")]       // Tabler digit-only name → leading-digit prefix
    [InlineData("2fa", "_2fa")]
    public void ToPascal_Transforms_And_Prefixes_Digits(string kebab, string expected) =>
        Assert.Equal(expected, NameTransform.ToPascal(kebab));

    // --- SVG parser: color scrub + duotone attribute preservation ---

    [Fact]
    public void Parse_Extracts_ViewBox_And_Preserves_Duotone_Opacity()
    {
        const string svg =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 256 256\" fill=\"currentColor\">" +
            "<path d=\"M1 2\" opacity=\"0.2\"/><path d=\"M3 4\"/></svg>";

        var parsed = SvgParser.Parse(svg);

        Assert.Equal("0 0 256 256", parsed.ViewBox);
        // The lighter duotone tone's per-path opacity must survive parsing verbatim.
        Assert.Contains("opacity=\"0.2\"", parsed.Content, StringComparison.Ordinal);
        // Root fill is dropped (the renderer re-emits it); inner shapes remain.
        Assert.Contains("<path", parsed.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Scrubs_Literal_Colors_But_Keeps_Opacity_And_CurrentColor()
    {
        const string svg =
            "<svg viewBox=\"0 0 24 24\"><path fill=\"#ff0000\" opacity=\"0.5\" d=\"M0 0\"/>" +
            "<path fill=\"currentColor\" d=\"M1 1\"/></svg>";

        var content = SvgParser.Parse(svg).Content;

        Assert.DoesNotContain("#ff0000", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fill=\"currentColor\"", content, StringComparison.Ordinal);
        Assert.Contains("opacity=\"0.5\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Strips_Class_And_Xml_Namespace_From_Children()
    {
        const string svg =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\">" +
            "<path class=\"foo\" d=\"M0 0\"/></svg>";

        var content = SvgParser.Parse(svg).Content;

        Assert.DoesNotContain("class=", content, StringComparison.Ordinal);
        Assert.DoesNotContain("xmlns", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Preserves_FillRule_And_ClipRule_EvenOdd_On_Solid_Sets()
    {
        // Heroicons solid / mini / micro and other fill packs paint with the even-odd rule; the
        // parser must keep fill-rule/clip-rule verbatim (they are not the plain `fill` color attr,
        // so the currentColor scrub must not touch them).
        const string svg =
            "<svg viewBox=\"0 0 20 20\" fill=\"currentColor\">" +
            "<path fill-rule=\"evenodd\" clip-rule=\"evenodd\" d=\"M9 1Z\"/></svg>";

        var content = SvgParser.Parse(svg).Content;

        Assert.Contains("fill-rule=\"evenodd\"", content, StringComparison.Ordinal);
        Assert.Contains("clip-rule=\"evenodd\"", content, StringComparison.Ordinal);
    }
}
