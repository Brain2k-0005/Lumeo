using System.Globalization;
using System.Text.RegularExpressions;

namespace Lumeo;

/// <summary>
/// One renderable SVG icon: its inner markup (paths/shapes) plus the metadata that tells a
/// renderer how the root <c>&lt;svg&gt;</c> element must be styled.
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="IconSource"/> is the first-party, dependency-free replacement for a third-party
/// <c>SvgIcon</c>. It carries only the <em>inner</em> markup — the surrounding <c>&lt;svg&gt;</c>
/// root (viewBox, fill/stroke, stroke-width, line caps/joins) is emitted by the renderer
/// (<c>SvgGlyph</c>) from the fields on this record. That split keeps the vendored icon strings
/// tiny and lets a single renderer style every pack consistently.
/// </para>
/// <para>
/// Instances are immutable value records — two icons with identical <see cref="Content"/>,
/// <see cref="ViewBox"/>, <see cref="RenderStyle"/> and <see cref="StrokeWidth"/> compare equal.
/// Generated icon packs expose one expression-bodied static property per icon (never a
/// <c>static readonly</c> field) so a WASM trimmer keeps only the icons a call site actually
/// references.
/// </para>
/// </remarks>
public sealed record IconSource
{
    /// <summary>
    /// The inner SVG markup — the paths and shapes that live <em>between</em> the
    /// <c>&lt;svg&gt;</c> tags (for example <c>&lt;path d="…" /&gt;&lt;circle … /&gt;</c>).
    /// </summary>
    /// <remarks>
    /// This markup may carry its own per-element <c>opacity</c>/<c>fill</c> attributes, which is
    /// exactly how duotone families (e.g. Phosphor Duotone) encode their two-tone look — no
    /// special renderer support is needed because the shading lives here in the content.
    /// </remarks>
    public required string Content { get; init; }

    /// <summary>
    /// The value of the root <c>&lt;svg&gt;</c> <c>viewBox</c> attribute. Defaults to
    /// <c>"0 0 24 24"</c>, the 24×24 grid used by Lucide and Tabler.
    /// </summary>
    public string ViewBox { get; init; } = "0 0 24 24";

    /// <summary>
    /// How the root <c>&lt;svg&gt;</c> paints this icon:
    /// <see cref="IconRenderStyle.Stroke"/> for outline families (Lucide/Tabler style —
    /// <c>fill="none" stroke="currentColor"</c>) or <see cref="IconRenderStyle.Fill"/> for solid
    /// families (Phosphor/Material/Bootstrap style — <c>fill="currentColor"</c>).
    /// Defaults to <see cref="IconRenderStyle.Stroke"/>.
    /// </summary>
    public IconRenderStyle RenderStyle { get; init; } = IconRenderStyle.Stroke;

    /// <summary>
    /// The root <c>stroke-width</c>, applied only when <see cref="RenderStyle"/> is
    /// <see cref="IconRenderStyle.Stroke"/>. Defaults to <c>2</c> (the Lucide and Tabler default);
    /// ignored for <see cref="IconRenderStyle.Fill"/> icons.
    /// </summary>
    public double StrokeWidth { get; init; } = 2;

    /// <summary>
    /// Creates a stroke-style icon (outline families such as Lucide/Tabler) — the renderer emits
    /// <c>fill="none" stroke="currentColor"</c> and the given <paramref name="strokeWidth"/>.
    /// </summary>
    /// <param name="content">The inner SVG markup (see <see cref="Content"/>).</param>
    /// <param name="viewBox">The <c>viewBox</c> value (see <see cref="ViewBox"/>).</param>
    /// <param name="strokeWidth">The root <c>stroke-width</c> (see <see cref="StrokeWidth"/>).</param>
    public static IconSource Stroke(string content, string viewBox = "0 0 24 24", double strokeWidth = 2)
        => new() { Content = content, ViewBox = viewBox, RenderStyle = IconRenderStyle.Stroke, StrokeWidth = strokeWidth };

    /// <summary>
    /// Creates a fill-style icon (solid families such as Phosphor/Material/Bootstrap) — the
    /// renderer emits <c>fill="currentColor"</c> and no stroke.
    /// </summary>
    /// <param name="content">The inner SVG markup (see <see cref="Content"/>).</param>
    /// <param name="viewBox">The <c>viewBox</c> value (see <see cref="ViewBox"/>).</param>
    public static IconSource Fill(string content, string viewBox = "0 0 24 24")
        => new() { Content = content, ViewBox = viewBox, RenderStyle = IconRenderStyle.Fill };

    /// <summary>
    /// Creates an <see cref="IconSource"/> from a complete <c>&lt;svg&gt;…&lt;/svg&gt;</c> string
    /// (for example one copied from Iconify, Lucide or Heroicons).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the root start tag is inspected: <c>viewBox</c> (falling back to a numeric
    /// <c>width</c>/<c>height</c> in px or unitless, then <c>0 0 24 24</c>), and the paint style —
    /// <see cref="IconRenderStyle.Stroke"/> when the root has <c>fill="none"</c> and a
    /// <c>stroke</c> other than <c>none</c> (with <c>stroke-width</c>, default 2), otherwise
    /// <see cref="IconRenderStyle.Fill"/>. The root <c>width</c>, <c>height</c>, <c>class</c> and
    /// <c>style</c> are ignored; sizing comes from the renderer's <c>Size</c>/<c>Class</c>. The
    /// inner markup is sliced from the original string, not re-serialized. A leading XML
    /// declaration, doctype or comments are tolerated.
    /// </para>
    /// <para>
    /// <b>Warning:</b> the content is rendered as-is, without sanitization. Only pass trusted,
    /// static SVG you control — never user-supplied strings.
    /// </para>
    /// </remarks>
    /// <param name="svg">A complete SVG document element.</param>
    /// <exception cref="ArgumentNullException"><paramref name="svg"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="svg"/> is empty or has no root <c>&lt;svg&gt;</c> element.</exception>
    public static IconSource FromSvg(string svg)
    {
        ArgumentNullException.ThrowIfNull(svg);
        if (string.IsNullOrWhiteSpace(svg))
            throw new ArgumentException("The SVG string is empty.", nameof(svg));

        // Skip the prolog (whitespace, <?xml ?>, <!DOCTYPE>, comments) to the root <svg start tag.
        int pos = 0;
        while (true)
        {
            while (pos < svg.Length && char.IsWhiteSpace(svg[pos])) pos++;
            if (string.CompareOrdinal(svg, pos, "<?", 0, 2) == 0)
            {
                int end = svg.IndexOf("?>", pos + 2, StringComparison.Ordinal);
                if (end < 0) break;
                pos = end + 2;
            }
            else if (string.CompareOrdinal(svg, pos, "<!--", 0, 4) == 0)
            {
                int end = svg.IndexOf("-->", pos + 4, StringComparison.Ordinal);
                if (end < 0) break;
                pos = end + 3;
            }
            else if (string.CompareOrdinal(svg, pos, "<!", 0, 2) == 0)
            {
                int end = svg.IndexOf('>', pos + 2);
                if (end < 0) break;
                pos = end + 1;
            }
            else break;
        }

        const string tagName = "<svg";
        if (pos >= svg.Length
            || string.Compare(svg, pos, tagName, 0, tagName.Length, StringComparison.OrdinalIgnoreCase) != 0
            || pos + tagName.Length >= svg.Length
            || !(char.IsWhiteSpace(svg[pos + tagName.Length]) || svg[pos + tagName.Length] is '>' or '/'))
            throw new ArgumentException("The string does not start with a root <svg> element.", nameof(svg));

        // Find the end of the start tag, ignoring '>' inside quoted attribute values.
        char quote = '\0';
        int tagEnd = -1;
        for (int i = pos + tagName.Length; i < svg.Length; i++)
        {
            char c = svg[i];
            if (quote != '\0') { if (c == quote) quote = '\0'; }
            else if (c == '"' || c == '\'') quote = c;
            else if (c == '>') { tagEnd = i; break; }
        }
        if (tagEnd < 0)
            throw new ArgumentException("The root <svg> start tag is not closed.", nameof(svg));

        bool selfClosing = svg[tagEnd - 1] == '/';
        var attrs = new Dictionary<string, string>(StringComparer.Ordinal);
        string startTag = svg.Substring(pos + tagName.Length, tagEnd - (pos + tagName.Length));
        foreach (Match m in AttributeRegex.Matches(startTag))
            attrs[m.Groups[1].Value] = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;

        string content = "";
        if (!selfClosing)
        {
            int close = svg.LastIndexOf("</svg", StringComparison.OrdinalIgnoreCase);
            if (close < tagEnd)
                throw new ArgumentException("The root <svg> element has no closing </svg> tag.", nameof(svg));
            content = svg.Substring(tagEnd + 1, close - tagEnd - 1).Trim();
        }

        string viewBox = "0 0 24 24";
        if (attrs.TryGetValue("viewBox", out var vb) && !string.IsNullOrWhiteSpace(vb))
            viewBox = vb.Trim();
        else if (attrs.TryGetValue("width", out var w) && attrs.TryGetValue("height", out var h)
                 && TryParseLength(w, out var wn) && TryParseLength(h, out var hn))
            viewBox = $"0 0 {wn.ToString(CultureInfo.InvariantCulture)} {hn.ToString(CultureInfo.InvariantCulture)}";

        bool stroke = attrs.TryGetValue("fill", out var fill) && fill.Trim() == "none"
                      && attrs.TryGetValue("stroke", out var st) && !string.IsNullOrWhiteSpace(st) && st.Trim() != "none";
        if (!stroke)
            return Fill(content, viewBox);

        double strokeWidth = 2;
        if (attrs.TryGetValue("stroke-width", out var sw)
            && double.TryParse(sw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var swn) && swn > 0)
            strokeWidth = swn;
        return Stroke(content, viewBox, strokeWidth);
    }

    private static readonly Regex AttributeRegex =
        new("([^\\s=/>\"']+)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static bool TryParseLength(string value, out double result)
    {
        value = value.Trim();
        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase)) value = value[..^2].TrimEnd();
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && result > 0;
    }
}

/// <summary>
/// How a renderer paints an <see cref="IconSource"/>: as a stroked outline or a solid fill.
/// </summary>
public enum IconRenderStyle
{
    /// <summary>
    /// Outline style (Lucide/Tabler): the root <c>&lt;svg&gt;</c> uses <c>fill="none"</c>,
    /// <c>stroke="currentColor"</c> and a configurable <see cref="IconSource.StrokeWidth"/>.
    /// </summary>
    Stroke,

    /// <summary>
    /// Solid style (Phosphor/Material/Bootstrap): the root <c>&lt;svg&gt;</c> uses
    /// <c>fill="currentColor"</c> and no stroke.
    /// </summary>
    Fill,
}
