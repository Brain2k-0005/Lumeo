namespace Lumeo;

/// <summary>Which edge of the plot rect an axis is drawn along.</summary>
public enum ChartAxisOrientation
{
    Bottom,
    Top,
    Left,
    Right,
}

/// <summary>One rendered tick: its pixel position along the axis and its
/// display label. Callers compute these from a scale + <c>NiceTicks</c>/
/// <c>TimeTicks</c> before handing them to <see cref="ChartAxis"/> — the
/// component itself does no scale math, only markup.</summary>
public sealed record ChartAxisTick(double Position, string Label);
