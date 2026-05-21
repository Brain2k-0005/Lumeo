namespace Lumeo;

/// <summary>
/// Cardinal side used for overlay placement and edge anchoring. Replaces the
/// per-component <c>*Side</c> enums (PopoverSide, TooltipSide, DropdownMenuSide,
/// HoverCardSide, DrawerSide, SheetSide, SidebarSide, TourPlacement) in 3.0
/// (BREAKING).
/// </summary>
/// <remarks>
/// Components that only meaningfully support a subset (e.g. <c>Sidebar</c> only
/// uses <see cref="Left"/> / <see cref="Right"/>) document the supported subset
/// in XML and fall back to their default for unsupported values.
/// </remarks>
public enum Side
{
    /// <summary>The top edge.</summary>
    Top,
    /// <summary>The right edge.</summary>
    Right,
    /// <summary>The bottom edge.</summary>
    Bottom,
    /// <summary>The left edge.</summary>
    Left,
}
