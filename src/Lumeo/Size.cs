namespace Lumeo;

/// <summary>
/// Unified size scale used across Lumeo components. Replaces the per-component
/// <c>*Size</c> enums in 3.0 (BREAKING).
/// </summary>
/// <remarks>
/// Not every component implements every value. A component documents which
/// values it supports; values outside the supported set fall through to the
/// component's default. The previous per-component <c>Default</c> value maps
/// to <see cref="Md"/>.
///
/// Exception cases that intentionally keep their own enums:
/// <list type="bullet">
///   <item><c>Button.ButtonSize</c> — includes <c>Icon</c> (a shape, not a scale).</item>
///   <item><c>DialogContent.DialogSize</c> / <c>SheetContent.SheetSize</c> —
///   include <c>Full</c> (a layout intent, not a scale).</item>
/// </list>
/// </remarks>
public enum Size
{
    /// <summary>Extra small. Used by <c>Icon</c>.</summary>
    Xs,
    /// <summary>Small. Used by most sized components (Avatar, Chip, Input, etc.).</summary>
    Sm,
    /// <summary>Medium / default. The neutral middle value for every sized component.</summary>
    Md,
    /// <summary>Large. Used by most sized components.</summary>
    Lg,
    /// <summary>Extra large. Used by <c>Avatar</c>, <c>Icon</c>.</summary>
    Xl,
    /// <summary>2x extra large. Reserved for future use.</summary>
    Xxl,
}
