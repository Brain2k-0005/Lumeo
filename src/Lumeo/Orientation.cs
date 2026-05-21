namespace Lumeo;

/// <summary>
/// Layout axis for components that can lay their content out either left-to-right
/// or top-to-bottom (Tabs, Separator, Carousel, Stepper, Timeline, Splitter,
/// MegaMenu, ButtonGroup, ImageCompare, FormField, Steps, ToggleGroup, etc.).
/// Replaces per-component <c>*Orientation</c> / <c>*Direction</c> enums in 3.0
/// (BREAKING).
/// </summary>
public enum Orientation
{
    /// <summary>Lay out content horizontally (left-to-right).</summary>
    Horizontal,
    /// <summary>Lay out content vertically (top-to-bottom).</summary>
    Vertical,
}
