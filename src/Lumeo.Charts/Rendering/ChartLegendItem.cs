namespace Lumeo;

/// <summary>One legend entry: a series key (used for toggle/hover identity),
/// its display name, swatch color (any valid CSS color, typically a
/// <c>var(--color-chart-N)</c> token), and optional trailing value text.</summary>
public sealed record ChartLegendItem(string Key, string Name, string Color, string? Value = null);
