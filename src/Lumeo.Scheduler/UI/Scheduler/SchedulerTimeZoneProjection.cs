using System.Globalization;

namespace Lumeo;

/// <summary>
/// Projects events into a display time zone and back again.
///
/// <para>
/// Every Scheduler view is deliberately a WALL-CLOCK renderer: it reads calendar fields off
/// the <see cref="DateTime"/> values it is handed and never consults
/// <see cref="TimeZoneInfo"/> (see SchedulerDateMath's own remarks for why). That property is
/// what makes the grid immune to DST and to the difference between a CI box and a dev
/// machine, and it is not given up here. Instead the wrapper converts events ON THE WAY IN
/// and converts edits back ON THE WAY OUT, so the views keep rendering exactly one thing —
/// wall-clock — and only the question of WHICH wall clock moves.
/// </para>
///
/// <para>
/// Which values move is decided by <see cref="DateTime.Kind"/>, because that is the only
/// thing that says whether a value denotes a real instant:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="DateTimeKind.Unspecified"/> — a naive wall-clock reading, already expressed in
/// whatever zone the caller means. Left ALONE. This is what virtually every existing caller
/// passes, which is why switching a time zone on cannot silently move their events.
/// </description></item>
/// <item><description>
/// <see cref="DateTimeKind.Utc"/> and <see cref="DateTimeKind.Local"/> — real instants, so
/// they are converted into the display zone for rendering and converted back to the same
/// kind when an edit is handed to the caller.
/// </description></item>
/// </list>
///
/// <para>
/// All-day events are dates, not instants, and are never converted: shifting one by a few
/// hours moves it onto a different DAY, which is the one thing an all-day event must never
/// do.
/// </para>
/// </summary>
public static class SchedulerTimeZoneProjection
{
    /// <summary>
    /// The zone for an IANA id (also accepts a Windows id), or <c>null</c> when unset or
    /// unknown. Never throws: an id the host has no data for degrades to "no projection",
    /// which renders the caller's own wall-clock values rather than failing the render.
    /// </summary>
    public static TimeZoneInfo? Resolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return null;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }

    /// <summary>Wall-clock reading of <paramref name="value"/> in <paramref name="zone"/>. See the class remarks for which kinds move.</summary>
    public static DateTime ToDisplay(DateTime value, TimeZoneInfo? zone)
    {
        if (zone is null) return value;
        return value.Kind switch
        {
            DateTimeKind.Utc => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(value, zone), DateTimeKind.Unspecified),
            DateTimeKind.Local => DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(value, TimeZoneInfo.Local, zone), DateTimeKind.Unspecified),
            _ => value,
        };
    }

    /// <summary>
    /// The inverse of <see cref="ToDisplay(DateTime, TimeZoneInfo?)"/>: turns a wall-clock reading in
    /// <paramref name="zone"/> back into a value of <paramref name="originalKind"/>, so an
    /// edit reaches the caller in the same frame of reference they handed us.
    /// </summary>
    public static DateTime FromDisplay(DateTime displayValue, DateTimeKind originalKind, TimeZoneInfo? zone)
    {
        if (zone is null || originalKind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(displayValue, originalKind);

        var naive = DateTime.SpecifyKind(displayValue, DateTimeKind.Unspecified);

        // A drag can land on a wall-clock reading that does not exist — the hour a spring-forward
        // transition skips. Converting it throws, which would turn a legal-looking drop into an
        // unhandled exception on the circuit, so the reading is nudged forward past the gap
        // instead. Ambiguous readings (the repeated autumn hour) do not throw; .NET resolves
        // them to standard time, and this deliberately does not second-guess that.
        if (zone.IsInvalidTime(naive)) naive = naive.Add(zone.GetAdjustmentRules().Length > 0 ? TimeSpan.FromHours(1) : TimeSpan.Zero);

        return originalKind == DateTimeKind.Utc
            ? DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(naive, zone), DateTimeKind.Utc)
            : DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(naive, zone, TimeZoneInfo.Local), DateTimeKind.Local);
    }

    /// <summary>An event with its instants read in <paramref name="zone"/>. All-day events are returned untouched.</summary>
    public static SchedulerEvent ToDisplay(SchedulerEvent ev, TimeZoneInfo? zone)
    {
        if (zone is null || ev.AllDay) return ev;
        return ev with
        {
            Start = ToDisplay(ev.Start, zone),
            End = ToDisplay(ev.End, zone),
        };
    }

    /// <summary>
    /// An edited event converted back into <paramref name="original"/>'s frame of reference.
    /// The kinds come from the ORIGINAL because the views hand back naive wall-clock values —
    /// they have no idea what frame the caller was using.
    /// </summary>
    public static SchedulerEvent FromDisplay(SchedulerEvent edited, SchedulerEvent? original, TimeZoneInfo? zone)
    {
        if (zone is null || edited.AllDay) return edited;
        var startKind = original?.Start.Kind ?? DateTimeKind.Unspecified;
        var endKind = original?.End.Kind ?? DateTimeKind.Unspecified;
        return edited with
        {
            Start = FromDisplay(edited.Start, startKind, zone),
            End = FromDisplay(edited.End, endKind, zone),
        };
    }

    /// <summary>Today's date in <paramref name="zone"/>, falling back to the host's own today when unset.</summary>
    public static DateTime TodayIn(TimeZoneInfo? zone) =>
        zone is null
            ? DateTime.Today
            : TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date;

    /// <summary>
    /// The zone id handed to JS, which resolves it with <c>Intl</c> rather than .NET. Only
    /// IANA ids travel: <c>Intl</c> does not understand Windows ids, and sending one across
    /// would silently leave the now-indicator on the browser's own clock.
    /// </summary>
    public static string? JsZoneId(TimeZoneInfo? zone)
    {
        if (zone is null) return null;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana)) return iana;
        return zone.Id.Contains('/', StringComparison.Ordinal)
            ? zone.Id
            : zone.Id.Equals("UTC", StringComparison.OrdinalIgnoreCase)
                ? "UTC"
                : null;
    }

    /// <summary>Culture-invariant IANA-ish rendering used in diagnostics and aria text.</summary>
    internal static string Describe(TimeZoneInfo? zone) =>
        zone is null ? string.Empty : zone.Id.ToString(CultureInfo.InvariantCulture);
}
