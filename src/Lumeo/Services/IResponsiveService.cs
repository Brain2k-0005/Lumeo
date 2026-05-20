namespace Lumeo.Services;

/// <summary>
/// Reactive viewport/breakpoint information. Use this to make components respond
/// to viewport size changes (e.g. switching a <c>Sheet</c> from side panel on
/// desktop to fullscreen on mobile, or hiding sidebars below md). Subscribe to
/// <see cref="ViewportChanged"/> to react to live resize / orientation change.
/// </summary>
public interface IResponsiveService : IAsyncDisposable
{
    /// <summary>Current viewport width in CSS pixels (window.innerWidth). 0 until
    /// the first interop sync — read inside <c>OnAfterRenderAsync</c> or subscribe
    /// to <see cref="ViewportChanged"/> to get the populated value.</summary>
    double Width { get; }

    /// <summary>Current viewport height in CSS pixels (window.innerHeight). 0 until
    /// first interop sync. See <see cref="Width"/> for the timing caveat.</summary>
    double Height { get; }

    /// <summary>Current Tailwind-aligned breakpoint based on <see cref="Width"/>.
    /// Maps to the same boundaries Tailwind v4 uses: Sm=640, Md=768, Lg=1024,
    /// Xl=1280, Xxl=1536. Use the convenience flags below for the common case.</summary>
    Breakpoint Current { get; }

    /// <summary>True when <see cref="Width"/> is below <see cref="Breakpoint.Md"/>
    /// (768px) — the conventional mobile boundary. Same value the
    /// <c>OverlayOptions.MobileBreakpoint</c> default checks against.</summary>
    bool IsMobile { get; }

    /// <summary>True when <see cref="Width"/> is in <c>[Md, Lg)</c> — 768-1023px.
    /// Tablets in portrait, large phones in landscape.</summary>
    bool IsTablet { get; }

    /// <summary>True when <see cref="Width"/> is >= <see cref="Breakpoint.Lg"/>
    /// (1024px). Includes typical laptop / desktop layouts.</summary>
    bool IsDesktop { get; }

    /// <summary>Fires whenever the viewport size crosses a meaningful boundary.
    /// Implementations debounce raw resize events (~100ms) so frequent listeners
    /// don't fire per scroll-bar nudge. The event arg carries the new width,
    /// height, and breakpoint.</summary>
    event Action<ViewportInfo>? ViewportChanged;

    /// <summary>Lazily registers the JS resize listener and pulls the initial
    /// viewport size. Called automatically the first time a property is read,
    /// but consumers can call it eagerly from <c>OnAfterRenderAsync(firstRender: true)</c>
    /// to avoid the first-render zero-value read.</summary>
    ValueTask EnsureInitialisedAsync();
}

/// <summary>Tailwind v4–aligned breakpoint name.</summary>
public enum Breakpoint
{
    /// <summary>Width &lt; 640px — the mobile-first floor.</summary>
    Xs,
    /// <summary>Width in [640, 768) — large phones / small tablets in portrait.</summary>
    Sm,
    /// <summary>Width in [768, 1024) — tablets, phones in landscape.</summary>
    Md,
    /// <summary>Width in [1024, 1280) — laptops, narrow desktops.</summary>
    Lg,
    /// <summary>Width in [1280, 1536) — typical desktop monitors.</summary>
    Xl,
    /// <summary>Width >= 1536px — wide desktop monitors.</summary>
    Xxl
}

/// <summary>Immutable snapshot of the viewport at a point in time. Passed to
/// <see cref="IResponsiveService.ViewportChanged"/> subscribers.</summary>
/// <param name="Width">Viewport width in CSS pixels.</param>
/// <param name="Height">Viewport height in CSS pixels.</param>
/// <param name="Current">Current breakpoint for <paramref name="Width"/>.</param>
public record ViewportInfo(double Width, double Height, Breakpoint Current)
{
    /// <summary>Tailwind breakpoint pixel values, exposed as constants so call
    /// sites don't repeat the magic numbers. Aligned with Tailwind v4 defaults.</summary>
    public static class Breakpoints
    {
        public const int Sm = 640;
        public const int Md = 768;
        public const int Lg = 1024;
        public const int Xl = 1280;
        public const int Xxl = 1536;
    }

    /// <summary>Maps a width to a <see cref="Breakpoint"/> using the Tailwind
    /// thresholds. Pure function; consumers can call it directly with an
    /// arbitrary width (e.g. to pre-compute a breakpoint from a server-rendered
    /// User-Agent hint).</summary>
    public static Breakpoint FromWidth(double width) => width switch
    {
        < Breakpoints.Sm => Breakpoint.Xs,
        < Breakpoints.Md => Breakpoint.Sm,
        < Breakpoints.Lg => Breakpoint.Md,
        < Breakpoints.Xl => Breakpoint.Lg,
        < Breakpoints.Xxl => Breakpoint.Xl,
        _ => Breakpoint.Xxl
    };
}
