namespace Lumeo;

/// <summary>
/// Epoch-millisecond time scale (spec §2.1) — linear underneath, but keeps the
/// domain typed as <see cref="DateTimeOffset"/> so callers never hand-roll the
/// ms conversion. Tick *selection* (choosing a calendar-aware interval) lives in
/// <see cref="TimeTicks"/>; this type only does the domain↔pixel mapping.
/// </summary>
internal sealed class TimeScale
{
    private readonly LinearScale _inner;

    public DateTimeOffset DomainStart { get; }
    public DateTimeOffset DomainEnd { get; }
    public double RangeMin { get; }
    public double RangeMax { get; }

    public TimeScale(DateTimeOffset domainStart, DateTimeOffset domainEnd, double rangeMin, double rangeMax)
    {
        DomainStart = domainStart;
        DomainEnd = domainEnd;
        RangeMin = rangeMin;
        RangeMax = rangeMax;
        _inner = new LinearScale(
            domainStart.ToUnixTimeMilliseconds(), domainEnd.ToUnixTimeMilliseconds(), rangeMin, rangeMax);
    }

    public double Scale(DateTimeOffset value) => _inner.Scale(value.ToUnixTimeMilliseconds());

    public DateTimeOffset Invert(double pixel) =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(_inner.Invert(pixel)));
}
