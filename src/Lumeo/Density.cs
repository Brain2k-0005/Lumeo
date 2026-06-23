namespace Lumeo;

/// <summary>
/// Orthogonal "tightness" axis applied across sized components. Sits next to
/// the size scale (<see cref="Size"/>) rather than replacing it: a
/// <c>Size.Md</c> button can render at <c>Density.Compact</c> for a dense
/// admin grid or <c>Density.Spacious</c> for a marketing hero, without
/// scaling the type ramp itself.
/// </summary>
/// <remarks>
/// Components opt in per-component by reading <c>DensityScope.Current</c>
/// from a cascading <see cref="DensityScope"/> wrapper, or by accepting an
/// explicit <c>Density</c> parameter that overrides the inherited value. Only
/// padding / gap / row-height tokens shift between values — text size, icon
/// size, and border radius are unaffected (use <see cref="Size"/> for those).
/// </remarks>
public enum Density
{
    /// <summary>Minimum padding. For data grids, dense admin tables, sidebar
    /// pickers. Visual hit area is ~24-28px.</summary>
    Compact = 0,

    /// <summary>Default. Standard app density. Hit area ~36-40px.</summary>
    Comfortable = 1,

    /// <summary>Generous padding. Marketing pages, onboarding wizards,
    /// thumb-first mobile UIs. Hit area ~44-48px (Apple HIG minimum).</summary>
    Spacious = 2,
}
