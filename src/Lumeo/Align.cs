namespace Lumeo;

/// <summary>
/// Alignment along an axis (e.g. how a popover content aligns relative to its
/// trigger along the perpendicular axis). Replaces the per-component
/// <c>*Align</c> enums (PopoverAlign, DropdownMenuAlign, HoverCardAlign) in 3.0
/// (BREAKING).
/// </summary>
public enum Align
{
    /// <summary>Align to the start (left in LTR, top vertically).</summary>
    Start,
    /// <summary>Center.</summary>
    Center,
    /// <summary>Align to the end (right in LTR, bottom vertically).</summary>
    End,
}
