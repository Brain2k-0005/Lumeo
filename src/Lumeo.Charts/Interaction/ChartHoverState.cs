namespace Lumeo;

/// <summary>
/// Hover state shared by tooltip + crosshair (spec §3.2/§3.3). C# owns
/// hit-testing directly, so it can set this state on a normal Blazor render
/// instead of the old ECharts-formatter-callback-into-a-hidden-div indirection.
/// <see cref="TrySet"/> uses the exact dedup-signature pattern already proven in
/// <c>Chart.razor</c>'s ECharts tooltip bridge (<c>seriesIndex|dataIndex</c>),
/// so a caller only re-renders on an ACTUAL point change, not every pointermove.
/// </summary>
internal sealed class ChartHoverState
{
    public int? SeriesIndex { get; private set; }
    public int? DataIndex { get; private set; }
    public double PointerX { get; private set; }
    public double PointerY { get; private set; }
    public bool IsActive => DataIndex is not null;

    private string? _signature;

    /// <summary>Updates the hover point. Returns <c>true</c> only when the
    /// resolved (series,index) pair actually changed — pointer coordinates
    /// themselves are always refreshed (for tooltip positioning) but do not by
    /// themselves count as a "change" worth a re-render.</summary>
    public bool TrySet(int? seriesIndex, int dataIndex, double pointerX, double pointerY)
    {
        PointerX = pointerX;
        PointerY = pointerY;

        var sig = $"{seriesIndex}|{dataIndex}";
        if (sig == _signature) return false;

        _signature = sig;
        SeriesIndex = seriesIndex;
        DataIndex = dataIndex;
        return true;
    }

    /// <summary>Clears the hover (pointer left the plot area). Returns
    /// <c>true</c> when there was an active hover to clear.</summary>
    public bool Clear()
    {
        if (_signature is null) return false;
        _signature = null;
        SeriesIndex = null;
        DataIndex = null;
        return true;
    }
}
