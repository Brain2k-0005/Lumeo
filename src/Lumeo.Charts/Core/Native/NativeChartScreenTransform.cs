using Lumeo.Services;

namespace Lumeo;

/// <summary>
/// Converts a LOGICAL SVG-viewBox-space point into real viewport pixels, for
/// <see cref="ChartTooltipHost"/>'s <c>position:fixed</c> coordinates. Shared
/// by every native chart host that positions its own tooltip
/// (<c>CartesianChartHost</c>, <c>XyChartHost</c>, <c>NativeBoxPlotChart</c>,
/// <c>NativeCandlestickChart</c>) — see remarks on why this is ONE class
/// instead of four copies.
/// </summary>
/// <remarks>
/// Every native SVG is drawn in a fixed LOGICAL viewBox and CSS-scaled to its
/// real rendered box (<c>preserveAspectRatio="none"</c> for
/// CartesianChartHost/XyChartHost, <c>"xMidYMid meet"</c> for
/// BoxPlot/Candlestick/etc.), so logical viewBox coordinates are NOT screen
/// pixels whenever the box and viewBox differ — common (<c>Width="100%"</c>,
/// a resized container, letterboxing).
///
/// CartesianChartHost was the first native host to fix a resulting tooltip-
/// drift bug; XyChartHost copied the fix verbatim as a second identical
/// implementation. BoxPlot and Candlestick shipped with their OWN,
/// still-broken raw-viewBox tooltip positioning because those files were
/// dirty mid-edit when the original fix landed elsewhere. Rather than hand-
/// copying the same calculation a THIRD and FOURTH time — three-plus
/// independent implementations of one calculation is exactly how this bug
/// survived as long as it did — this type is the ONE implementation; all four
/// hosts now share it.
///
/// Callers must call <see cref="RefreshAsync"/> FRESH on every resolved
/// hover-index/point change, never cache it from first render:
/// <see cref="IComponentInteropService.GetElementRect"/>
/// (<c>getBoundingClientRect</c> under the hood) is VIEWPORT-relative, so a
/// cached value goes stale the moment the page scrolls after mount — a real,
/// previously-caught bug (a 2368px drift). Each host's own pointer-
/// tracking/hit-testing already collapses a 60fps pointermove/hover down to
/// one call per resolved index/point change, so refreshing here adds at most
/// one interop round-trip per index change, never per frame.
/// </remarks>
internal sealed class NativeChartScreenTransform
{
    public double RectX { get; private set; }
    public double RectY { get; private set; }
    public double ScaleX { get; private set; } = 1;
    public double ScaleY { get; private set; } = 1;

    public async Task RefreshAsync(IComponentInteropService interop, string elementId, double viewW, double viewH)
    {
        try
        {
            var rect = await interop.GetElementRect(elementId);
            if (rect is { Width: > 0, Height: > 0 } && viewW > 0 && viewH > 0)
            {
                RectX = rect.X;
                RectY = rect.Y;
                ScaleX = rect.Width / viewW;
                ScaleY = rect.Height / viewH;
            }
        }
        catch (Microsoft.JSInterop.JSDisconnectedException) { }
    }

    /// <summary>Converts a logical (viewBox-space) point to real viewport pixels.</summary>
    public (double X, double Y) ToScreen(double logicalX, double logicalY) =>
        (RectX + logicalX * ScaleX, RectY + logicalY * ScaleY);
}
