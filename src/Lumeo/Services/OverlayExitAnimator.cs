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
    /// </summary>
    public async Task WireExitAsync(Func<Task> wire)
    {
        if (!Exiting || _wired) return;
        _wired = true;
        await wire();
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

    public void Dispose() => _fallback.Dispose();
}
