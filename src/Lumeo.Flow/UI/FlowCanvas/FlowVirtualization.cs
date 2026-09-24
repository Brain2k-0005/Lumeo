namespace Lumeo;

/// <summary>
/// Phase 5 virtualization (<c>FlowCanvas.OnlyRenderVisibleNodes</c>) as pure functions, so the
/// window/hysteresis rules are unit-testable without a browser.
/// <para>
/// The RENDER WINDOW is the visible viewport (in flow coordinates) inflated by one viewport in each
/// direction — 3×3 viewports in total. A node is mounted when its absolute rect intersects the
/// window. The window is recomputed only when <see cref="NeedsRewindow"/> says so: when the visible
/// viewport has drifted more than half a viewport away from where the window was centred (so it no
/// longer sits inside the window deflated by half a viewport on every side), or when a zoom-in shrank
/// the visible viewport to less than half the size the window was built for (the window would keep far
/// more nodes mounted than needed). Anything smaller — a small pan back and forth, a slight zoom — keeps
/// the current window and costs no render at all: that is the hysteresis.
/// </para>
/// </summary>
internal static class FlowVirtualization
{
    /// <summary>The minimum time between two re-window renders while a gesture is still running (a final report always re-windows when needed).</summary>
    public const int RewindowIntervalMs = 80;

    /// <summary>The visible pane in flow coordinates, or null when the pane has no size yet.</summary>
    public static FlowRect? ViewportRect(FlowViewport viewport, double paneWidth, double paneHeight)
    {
        if (!(paneWidth > 0) || !(paneHeight > 0) || !(viewport.Zoom > 0)) return null;
        var topLeft = FlowGeometry.ScreenToFlow(0, 0, viewport);
        return new FlowRect(topLeft.X, topLeft.Y, paneWidth / viewport.Zoom, paneHeight / viewport.Zoom);
    }

    /// <summary><paramref name="rect"/> grown by <paramref name="factor"/> × its own width/height on every side.</summary>
    public static FlowRect Inflate(FlowRect rect, double factor)
    {
        var dx = rect.Width * factor;
        var dy = rect.Height * factor;
        return new FlowRect(rect.X - dx, rect.Y - dy, rect.Width + dx * 2, rect.Height + dy * 2);
    }

    /// <summary>The render window for <paramref name="viewportRect"/>: one viewport of margin in each direction.</summary>
    public static FlowRect ComputeWindow(FlowRect viewportRect) => Inflate(viewportRect, 1);

    /// <summary>Whether two rects overlap (touching edges do not count).</summary>
    public static bool Intersects(FlowRect a, FlowRect b)
        => a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;

    /// <summary>Whether <paramref name="inner"/> lies entirely inside <paramref name="outer"/>.</summary>
    public static bool Contains(FlowRect outer, FlowRect inner)
        => inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

    /// <summary>
    /// Whether the window built for <paramref name="windowViewport"/> (the visible rect at the time it
    /// was computed; null = never computed) must be rebuilt for the visible rect <paramref name="current"/>.
    /// </summary>
    public static bool NeedsRewindow(FlowRect? windowViewport, FlowRect current)
    {
        if (windowViewport is not { } built) return true;
        if (!(built.Width > 0) || !(built.Height > 0)) return true;
        // Zoomed in to less than half the size the window was built for: shrink it.
        if (current.Width < built.Width / 2 || current.Height < built.Height / 2) return true;
        // The window is `built` inflated by one viewport; the hysteresis band is half of that margin.
        var safe = Inflate(built, 0.5);
        return !Contains(safe, current);
    }
}
