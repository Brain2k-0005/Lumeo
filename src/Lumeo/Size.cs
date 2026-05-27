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
    // Backing values pinned explicitly so future additions cannot silently
    // re-number the existing members. Consumers that persist Lumeo.Size via
    // ints (e.g. as ASP.NET TempData, EF column, Razor query string) get a
    // stable mapping across upgrades. Append new values at the end with a
    // fresh number; never insert in the middle.
    /// <summary>Extra small. Used by <c>Icon</c>.</summary>
    Xs = 0,
    /// <summary>Small. Used by most sized components (Avatar, Chip, Input, etc.).</summary>
    Sm = 1,
    /// <summary>Medium / default. The neutral middle value for every sized component.</summary>
    Md = 2,
    /// <summary>Large. Used by most sized components.</summary>
    Lg = 3,
    /// <summary>Extra large. Used by <c>Avatar</c>, <c>Icon</c>.</summary>
    Xl = 4,
    /// <summary>2x extra large. Reserved for future use.</summary>
    Xxl = 5,
    /// <summary>Double extra small. For dense-mode <c>Avatar</c> / <c>Chip</c>
    /// where a smaller indicator is needed (e.g. inline-with-text presence
    /// dot, stacked-avatar list previews). Replaces consumer-side
    /// <c>text-[10px]</c> / <c>h-5 w-5</c> arbitrary-value Tailwind escape
    /// hatches with a first-class token. Added in 3.3; appended at value 6
    /// instead of inserted before <see cref="Xs"/> so existing serialised
    /// ints (Sm=1, Md=2, …) still resolve to the same enum member.</summary>
    Xxs = 6,
}
