namespace Lumeo;

/// <summary>
/// Domain-fraction zoom/pan state shared by the DataZoom slider and
/// inside-plot wheel/drag zoom (spec §3.4). C# owns all domain recomputation;
/// JS never decides anything here — wheel deltas arrive via Blazor's native
/// <c>@onwheel</c> event and drag deltas via the existing pointer-capture
/// interop (<c>ComponentInteropService.SetPointerCaptureOnElement</c>), so this
/// needs no bespoke JS call of its own (spec §3.4: "reuse
/// <c>ComponentInteropService</c> pointer-capture, not a bespoke new mechanism").
/// </summary>
internal sealed class ChartZoomState
{
    /// <summary>Start of the visible window, as a fraction (0..1) of the full domain.</summary>
    public double Start { get; private set; }

    /// <summary>End of the visible window, as a fraction (0..1) of the full domain.</summary>
    public double End { get; private set; } = 1;

    public double Span => End - Start;

    public void SetRange(double start, double end)
    {
        start = Math.Clamp(start, 0, 1);
        end = Math.Clamp(end, 0, 1);
        if (end < start) (start, end) = (end, start);
        Start = start;
        End = end;
    }

    public void Reset() => SetRange(0, 1);

    /// <summary>
    /// Zooms around a pivot fraction (0..1 within the CURRENT window — e.g. the
    /// pointer position under the wheel) by <paramref name="factor"/> (&gt;1
    /// zooms in, &lt;1 zooms out), clamped so the window can't collapse below
    /// <paramref name="minSpan"/> or exceed the full domain.
    /// </summary>
    public void ZoomAt(double pivot, double factor, double minSpan = 0.01)
    {
        if (factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor));
        pivot = Math.Clamp(pivot, 0, 1);
        var newSpan = Math.Clamp(Span / factor, minSpan, 1);
        var pivotValue = Start + pivot * Span;
        var newStart = pivotValue - pivot * newSpan;
        var newEnd = newStart + newSpan;

        if (newStart < 0) { newEnd -= newStart; newStart = 0; }
        if (newEnd > 1) { newStart -= newEnd - 1; newEnd = 1; }

        SetRange(Math.Max(0, newStart), Math.Min(1, newEnd));
    }

    /// <summary>Pans the current window by <paramref name="deltaFraction"/>
    /// (positive = forward), clamped so the window stays inside <c>[0,1]</c>.</summary>
    public void Pan(double deltaFraction)
    {
        var span = Span;
        var newStart = Math.Clamp(Start + deltaFraction, 0, 1 - span);
        SetRange(newStart, newStart + span);
    }

    /// <summary>Maps the current <c>[Start,End]</c> domain-fraction window onto
    /// absolute domain values given the FULL (unzoomed) domain bounds.</summary>
    public (double Min, double Max) ToDomain(double fullMin, double fullMax)
    {
        var span = fullMax - fullMin;
        return (fullMin + Start * span, fullMin + End * span);
    }
}
