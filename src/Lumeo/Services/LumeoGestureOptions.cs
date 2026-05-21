namespace Lumeo.Services;

/// <summary>
/// Global thresholds for touch gestures used across Lumeo components
/// (Tabs, Carousel, Calendar, ImageGallery, Drawer, Sheet). Centralizes
/// values that were previously hardcoded inside <c>components.js</c> so
/// consumers can tune feel app-wide without forking the JS.
///
/// Override defaults via <c>builder.Services.Configure&lt;LumeoGestureOptions&gt;(...)</c>.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddLumeo();
/// builder.Services.Configure&lt;LumeoGestureOptions&gt;(opts =&gt;
/// {
///     opts.SwipeThresholdPx = 70;          // stricter horizontal swipe
///     opts.SwipeDismissFirePx = 120;       // require a bigger pull-down on Drawer/Sheet
/// });
/// </code>
/// </example>
public sealed class LumeoGestureOptions
{
    /// <summary>Horizontal swipe distance (px) needed to register a Tabs / Carousel / Calendar / Gallery directional swipe. Default 50.</summary>
    public int SwipeThresholdPx { get; set; } = 50;

    /// <summary>Vertical movement (px) above which a horizontal swipe is treated as a scroll instead, suppressing the swipe. Default 40.</summary>
    public int VerticalDeadZonePx { get; set; } = 40;

    /// <summary>Drawer / Sheet swipe-to-close activation distance (px). Default 60. Once exceeded, the overlay locks into dismiss mode and animates with the finger.</summary>
    public int SwipeDismissThresholdPx { get; set; } = 60;

    /// <summary>Pull-down distance (px) above which a swipe-to-close fires after release. Default 100. Below this value the overlay snaps back.</summary>
    public int SwipeDismissFirePx { get; set; } = 100;
}
