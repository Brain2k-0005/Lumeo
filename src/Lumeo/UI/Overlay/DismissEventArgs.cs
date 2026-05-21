namespace Lumeo;

/// <summary>
/// Arguments supplied to the <c>OnBeforeClose</c> callback exposed by every
/// dismissible overlay root (<c>Dialog</c>, <c>Sheet</c>, <c>Drawer</c>,
/// <c>AlertDialog</c>). Consumers can inspect <see cref="Reason"/> to find
/// out which user gesture is requesting the dismiss and set
/// <see cref="Cancel"/> to <c>true</c> to keep the overlay open — useful for
/// "are you sure?" guards over unsaved form state.
/// </summary>
public sealed class DismissEventArgs
{
    /// <summary>Set to <c>true</c> from a handler to block the dismiss.</summary>
    public bool Cancel { get; set; }

    /// <summary>
    /// Identifies which dismiss path is invoking the callback. One of:
    /// <c>"escape"</c> (Escape key), <c>"outside"</c> (backdrop / click
    /// outside), <c>"swipe"</c> (touch swipe on Drawer / Sheet),
    /// <c>"close"</c> (the X button or a <c>DialogClose</c> /
    /// <c>SheetClose</c> / <c>DrawerClose</c> trigger),
    /// <c>"action"</c> (AlertDialog confirm button) or
    /// <c>"cancel"</c> (AlertDialog cancel button). Empty for programmatic
    /// dismisses that do not flow through the gate.
    /// </summary>
    public string Reason { get; init; } = "";
}
