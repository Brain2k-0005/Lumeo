using Microsoft.AspNetCore.Components;

namespace Lumeo;

/// <summary>
/// Partial extension to <see cref="ToastProvider"/> that adds the
/// <see cref="MaxVisible"/> stacking cap.
/// When <see cref="MaxVisible"/> &gt; 0 the provider syncs
/// <see cref="MaxToasts"/> so at most that many toasts are displayed;
/// older toasts are evicted and resurface when newer ones are dismissed.
/// </summary>
public partial class ToastProvider
{
    /// <summary>
    /// Maximum number of toasts to display simultaneously. 0 = unlimited.
    /// Older toasts beyond this cap are evicted (same behaviour as
    /// <see cref="MaxToasts"/>) and resurface when newer toasts are dismissed.
    /// </summary>
    [Parameter] public int MaxVisible { get; set; } = 0;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (MaxVisible > 0)
            MaxToasts = MaxVisible;
    }
}
