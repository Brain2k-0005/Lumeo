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
