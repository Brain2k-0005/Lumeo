using System.Xml.Linq;

namespace Lumeo.IconGen;

/// <summary>A parsed, scrubbed icon: its <c>viewBox</c> and the compact inner SVG markup.</summary>
public readonly record struct ParsedIcon(string ViewBox, string Content);

/// <summary>
/// Turns a raw upstream <c>&lt;svg&gt;</c> document into an <see cref="ParsedIcon"/>: it extracts the
/// <c>viewBox</c>, drops the root element (the renderer re-emits a styled root), strips the SVG XML
/// namespace and presentational <c>class</c> attributes from the inner nodes, maps literal colors to
/// <c>currentColor</c>, and serializes the inner shapes as a single compact whitespace-free string.
/// Geometric <c>width</c>/<c>height</c> on inner shapes (e.g. <c>&lt;rect&gt;</c>) are preserved.
/// </summary>
public static class SvgParser
{
    private const string DefaultViewBox = "0 0 24 24";

    public static ParsedIcon Parse(string svgText)
    {
        var root = XDocument.Parse(svgText).Root
                   ?? throw new InvalidOperationException("SVG has no root element.");

        var viewBox = root.Attribute("viewBox")?.Value?.Trim();
        if (string.IsNullOrEmpty(viewBox)) viewBox = DefaultViewBox;

        var content = string.Concat(
            root.Nodes()
                .OfType<XElement>()
                .Select(e => Strip(e).ToString(SaveOptions.DisableFormatting)));

        return new ParsedIcon(viewBox, content);
    }

    /// <summary>
    /// Recursively rebuilds <paramref name="e"/> with no XML namespace, no <c>class</c> attribute,
    /// and <c>fill</c>/<c>stroke</c> colors scrubbed to <c>currentColor</c>. Attribute order is
    /// preserved; all other attributes (including geometric width/height) are kept verbatim.
    /// </summary>
    private static XElement Strip(XElement e)
    {
        var copy = new XElement(e.Name.LocalName);

        foreach (var attr in e.Attributes())
        {
            if (attr.IsNamespaceDeclaration) continue;

            var name = attr.Name.LocalName;
            if (name is "class") continue;

            var value = attr.Value;
            if (name is "fill" or "stroke") value = NameTransform.ScrubColor(value);

            copy.SetAttributeValue(name, value);
        }

        foreach (var child in e.Elements())
            copy.Add(Strip(child));

        return copy;
    }
}
