using Microsoft.AspNetCore.Components;

namespace Lumeo;

/// <summary>
/// Registration record holding the active tooltip slot's render fragment + class. Only
/// one tooltip slot per chart is meaningful, so this collapses to a single slot rather
/// than a list (a second <see cref="ChartTooltip"/> overrides the first).
/// </summary>
public sealed record ChartTooltipSlotInfo(
    RenderFragment<ChartTooltipContext> ChildContent,
    string? Class);

/// <summary>
/// Cascaded by <see cref="Chart"/> to its declared <see cref="ChartTooltip"/> child.
/// The child sets the slot on <c>OnParametersSet</c> and clears it on dispose;
/// the chart picks up the active registration to render the hidden portal element
/// that ECharts' tooltip formatter pulls innerHTML from.
/// </summary>
public sealed class ChartTooltipSlotRegistration
{
    private readonly Action _onChanged;

    public ChartTooltipSlotRegistration(Action onChanged) => _onChanged = onChanged;

    public ChartTooltipSlotInfo? Current { get; private set; }

    public void Set(ChartTooltipSlotInfo info)
    {
        if (Current == info) return;
        Current = info;
        _onChanged();
    }

    public void Clear()
    {
        if (Current is null) return;
        Current = null;
        _onChanged();
    }
}
