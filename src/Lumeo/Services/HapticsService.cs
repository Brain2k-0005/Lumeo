namespace Lumeo.Services;

/// <summary>
/// Service for triggering haptic feedback via <c>navigator.vibrate()</c>.
/// Browsers that don't support the Vibration API (e.g. iOS Safari) silently no-op.
/// </summary>
public sealed class HapticsService
{
    private readonly IComponentInteropService _interop;

    public HapticsService(IComponentInteropService interop) => _interop = interop;

    /// <summary>Trigger a single haptic vibration for the given number of milliseconds.</summary>
    public ValueTask Vibrate(int milliseconds) => _interop.Vibrate(milliseconds);

    /// <summary>Short, subtle tap — matches iOS "light" impact feedback.</summary>
    public ValueTask Light() => _interop.Vibrate(10);

    /// <summary>Medium tap — matches iOS "medium" impact feedback.</summary>
    public ValueTask Medium() => _interop.Vibrate(25);

    /// <summary>Strong tap — matches iOS "heavy" impact feedback.</summary>
    public ValueTask Heavy() => _interop.Vibrate(50);

    /// <summary>Double-tap pattern conveying success.</summary>
    public ValueTask Success() => _interop.Vibrate(15);

    /// <summary>Long buzz conveying an error or destructive action.</summary>
    public ValueTask Error() => _interop.Vibrate(75);
}
