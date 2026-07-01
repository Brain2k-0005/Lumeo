using Microsoft.AspNetCore.Components;

namespace Lumeo;

/// <summary>
/// Registration record holding the active tooltip slot's render fragment, class, and
/// any unmatched attributes the consumer set on <c>ChartTooltip</c>. Only one tooltip
/// slot per chart is meaningful, so the chart collapses to a single slot rather than
/// a list (a second <c>ChartTooltip</c> overrides the first).
/// </summary>
public sealed record ChartTooltipSlotInfo(
    RenderFragment<ChartTooltipContext> ChildContent,
    string? Class,
    IReadOnlyDictionary<string, object>? AdditionalAttributes);

/// <summary>
/// Cascaded by <see cref="Chart"/> to its declared <see cref="ChartTooltip"/> child.
/// The child sets the slot on <c>OnParametersSet</c> and clears it on dispose; the
/// chart picks up the active registration to render the hidden portal element that
/// ECharts' tooltip formatter pulls innerHTML from.
///
/// <para>The owner-keyed <see cref="Set(object, ChartTooltipSlotInfo)"/> /
/// <see cref="Clear(object)"/> pair guards against a stale child disposing AFTER a
/// new one has already registered — without it, the disposal would wipe out
/// the newer registration and the tooltip portal would silently vanish.</para>
/// </summary>
public sealed class ChartTooltipSlotRegistration
{
    private readonly Action _onChanged;
    private object? _owner;

    public ChartTooltipSlotRegistration(Action onChanged) => _onChanged = onChanged;

    public ChartTooltipSlotInfo? Current { get; private set; }

    public void Set(object owner, ChartTooltipSlotInfo info)
    {
        // Skip the change-notification when the same owner re-registers identical
        // state — OnParametersSet fires on every render, so an unconditional notify
        // would spin the chart's render loop.
        if (ReferenceEquals(_owner, owner) && Current == info) return;
        _owner = owner;
        Current = info;
        _onChanged();
    }

    public void Clear(object owner)
    {
        // Only the registering owner can clear. Defensive against the disposal of an
        // older tooltip instance racing with a newer registration.
        if (!ReferenceEquals(_owner, owner) || Current is null) return;
        _owner = null;
        Current = null;
        _onChanged();
    }
}
