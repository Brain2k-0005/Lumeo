namespace Lumeo;

/// <summary>
/// Implemented by <see cref="Toast"/> so the JS entrance-animation-end helper
/// (<c>attachToastEnterEnd</c>) can tell it when <c>.animate-toast-in</c> has
/// finished. <c>[JSInvokable] OnEnterAnimationEnd</c> runs once the toast's
/// OWN entrance keyframe finishes (filtered by animation name in JS — see
/// <c>attachToastEnterEnd</c> — so a one-shot animation on arbitrary
/// CustomContent bubbling out of the toast can never fire this early), so the
/// component can drop the entrance class instead of leaving it parked
/// (<c>animation-fill-mode: both</c> would otherwise pin opacity/transform
/// forever — see the "Toast stacking" section in lumeo.css). Exposed as an
/// interface, mirroring <see cref="IOverlayExitCallback"/>, so a test double
/// can capture and fire the callback generically without a real DOM.
/// </summary>
public interface IToastEnterCallback
{
    /// <summary>Invoked from JS once the toast's entrance animation has finished.</summary>
    System.Threading.Tasks.Task OnEnterAnimationEnd();
}
