using System;
using System.Threading.Tasks;

namespace Lumeo.Services;

/// <summary>
/// Shared exit-animation latch for the popover-positioned menu overlays
/// (DropdownMenu / HoverCard / Menubar / NavigationMenu / Tooltip and their
/// sub-content panels). Brings them to the same B11 Radix-Presence exit parity
/// the fixed overlays (Dialog / Sheet / Drawer / AlertDialog / Toast) already
/// have: on close the panel stays mounted with its exit keyframe and unmounts on
/// the panel's OWN <c>animationend</c> (via
/// <see cref="IComponentInteropService.AttachOverlayExitEnd{T}"/>), with a timer
/// fallback for the JS-dead / lost-event case.
///
/// <para>Unlike the fixed overlays, these panels position through the
/// transform-free <c>positionFixed</c> interop (it writes <c>top</c>/<c>left</c>
/// only and never stamps an inline <c>animation</c>/<c>transform</c> guard), and
/// <c>unpositionFixed</c> leaves those inline coordinates in place. So the host
/// can run its normal close-time cleanup (focus restore, click-outside teardown,
/// unposition) IMMEDIATELY on close — the box stays pinned at its last
/// coordinates while the opacity/scale exit keyframe plays — and this animator
/// only owns keeping the element mounted for the exit window. No inline-guard
/// strip is needed (that was the Sheet/Dialog slide path's B11 trap); the shared
/// <c>attachOverlayExitEnd</c> JS is reused as-is.</para>
/// </summary>
internal sealed class OverlayExitAnimator : IDisposable
{
    private readonly DelayedDispatch _fallback = new();
    // True once we have rendered the panel open at least once, so a later close
    // is a real open->closed transition (not the initial closed render).
    private bool _shownOpen;
    // Guards single JS-wiring per exit; reset on every new exit and on re-open.
    private bool _wired;
    // Set by Dispose(). Belt-and-suspenders alongside each component's own
    // _disposed field: even if a caller's OnAfterRenderAsync reaches WireExitAsync
    // right after its component was disposed, this stops it from invoking `wire`
    // (and therefore from touching a disposed DotNetObjectReference) at all.
    private bool _disposed;

    /// <summary>True while the panel is mounted purely to play its exit animation.</summary>
    public bool Exiting { get; private set; }

    /// <summary>Mount gate: render the panel while it is open OR exiting.</summary>
    public bool Present(bool isOpen) => isOpen || Exiting;

    /// <summary>
    /// Call from <c>OnParametersSet</c>. Latches the exit on the open→closed
    /// transition — scheduling <paramref name="finishFallback"/> after
    /// <paramref name="exitDurationMs"/> as the lost-event fallback — and cancels a
    /// pending exit when the panel re-opens mid-exit (rapid open/close/reopen), so
    /// the next render paints the ENTER class rather than a frame of the exit.
    /// </summary>
    public void OnParameters(bool isOpen, int exitDurationMs, Func<Task> finishFallback)
    {
        if (isOpen)
        {
            if (Exiting)
            {
                _fallback.Cancel();
                Exiting = false;
                _wired = false;
            }
            _shownOpen = true;
        }
        else if (_shownOpen)
        {
            _shownOpen = false;
            if (exitDurationMs > 0)
            {
                Exiting = true;
                _wired = false;
                _fallback.Schedule(exitDurationMs, finishFallback);
            }
        }
    }

    /// <summary>
    /// Call from <c>OnAfterRender</c> once the exit render has committed the exit
    /// class. Invokes <paramref name="wire"/> (the <c>attachOverlayExitEnd</c>
    /// interop that awaits the panel's exit animation and calls back
    /// <see cref="IOverlayExitCallback.OnExitAnimationEnd"/>) exactly once per exit.
    ///
    /// <para>Production circuit-kill fix: the caller's component can be disposed
    /// WHILE it was awaiting an EARLIER interop call in the same OnAfterRenderAsync
    /// (PositionFixed / UnpositionFixed / RegisterClickOutside, ...) and only
    /// reaches this call afterwards, with its DotNetObjectReference already
    /// disposed. <c>wire</c> handing that disposed reference to JS threw
    /// <see cref="ObjectDisposedException"/> straight out of OnAfterRenderAsync and
    /// killed the whole Blazor Server circuit (production report: a menu button
    /// navigates away while its tooltip/submenu is still closing).
    /// <see cref="OperationCanceledException"/> covers the analogous race on a
    /// circuit that is mid-teardown. Neither is actionable once the panel is gone,
    /// so both are swallowed here alongside the pre-existing
    /// <see cref="Microsoft.JSInterop.JSDisconnectedException"/> handling — one guard for every one of
    /// the ten components that call through this animator, regardless of which
    /// <see cref="IComponentInteropService"/> implementation they were given.</para>
    /// </summary>
    public async Task WireExitAsync(Func<Task> wire)
    {
        if (!Exiting || _wired || _disposed) return;
        _wired = true;
        try
        {
            await wire();
        }
        catch (Microsoft.JSInterop.JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Ends the exit phase (from the JS <c>animationend</c> callback or the
    /// fallback timer, whichever lands first — the other no-ops). Returns
    /// <c>true</c> when an exit was actually active, so the caller can
    /// <c>StateHasChanged</c> and let the mount gate drop the element.
    /// </summary>
    public bool Finish()
    {
        if (!Exiting) return false;
        _fallback.Cancel();
        Exiting = false;
        _wired = false;
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        _fallback.Dispose();
    }
}
