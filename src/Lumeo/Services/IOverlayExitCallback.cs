namespace Lumeo;

/// <summary>
/// Implemented by the overlay content components (Sheet / Drawer / Dialog /
/// AlertDialog) so the JS exit-animation-end helper
/// (<c>attachOverlayExitEnd</c>) can drive the unmount. The
/// <c>[JSInvokable] OnExitAnimationEnd</c> runs when the panel's OWN exit
/// keyframe finishes, letting the component drop backdrop + panel together on the
/// real animation end (Radix-Presence parity) instead of a blind timer. Exposed as
/// an interface so a test double can fire the callback generically.
/// </summary>
public interface IOverlayExitCallback
{
    /// <summary>Invoked from JS once the panel's exit animation has finished.</summary>
    System.Threading.Tasks.Task OnExitAnimationEnd();
}
