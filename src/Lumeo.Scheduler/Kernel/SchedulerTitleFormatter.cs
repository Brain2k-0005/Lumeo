using System.Globalization;
using Lumeo.Services.Localization;

namespace Lumeo.SchedulerKernel;

/// <summary>
/// View-title formatting for the Scheduler toolbar, routed through <see cref="ILumeoLocalizer"/>
/// for every string that needs one, and through an explicit <see cref="CultureInfo"/> for plain
/// date formatting. Spec §1.1/§4.6/§7.1.
///
/// <para>
/// <b>Why an explicit <see cref="CultureInfo"/> parameter, not ambient
/// <see cref="CultureInfo.CurrentCulture"/>:</b> Blazor Server shares threads/requests across
/// possibly-different-locale users; reading the ambient culture deep inside a pure kernel
/// function makes the result depend on whatever the CALLING request happened to set, which is
/// exactly the kind of implicit, hard-to-test dependency the rest of this kernel avoids (compare
/// <c>SchedulerDateMath</c>/<c>SchedulerRecurrenceExpander</c>'s explicit "never touch ambient
/// state" TZ/DST design). Callers (the eventual Razor view layer) resolve the request's culture
/// once and pass it down explicitly.
/// </para>
///
/// <para>
/// <b>The <c>Scheduler.WeekOf</c> bug this exists to not repeat (spec §4.6):</b> the
/// FullCalendar-era <c>Scheduler.razor</c>'s title logic calls
/// <c>string.Format(L["Scheduler.WeekOf"], ...)</c> — a MANUAL <c>string.Format</c> wrapped
/// around the localizer's plain (no-args) indexer. If the underlying localized string has no
/// <c>{0}</c> placeholder (as en/de's currently do, a data bug being fixed on a sibling branch,
/// out of this class's scope), <c>string.Format</c> silently ignores the extra argument and the
/// date is dropped with no error. This class instead always goes through
/// <see cref="ILumeoLocalizer"/>'s OWN args-aware indexer (<c>this[key, args]</c>) rather than a
/// second, hand-rolled <c>string.Format</c> call site — not because that indexer is immune to the
/// SAME missing-placeholder data bug (it isn't; a missing <c>{0}</c> in the string data will
/// still drop the argument no matter which call shape reaches it), but because routing every
/// localized, argument-carrying title through the ONE sanctioned formatting entry point is what
/// keeps this class itself from re-introducing a DIFFERENT variant of the same class of bug later
/// (e.g. forgetting to pass the date argument at all). <c>SchedulerTitleFormatterTests</c> proves
/// this by asserting the formatted Week title actually CONTAINS the date substring, using a
/// locally-constructed test localizer whose <c>Scheduler.WeekOf</c> value has a <c>{0}</c>
/// (i.e. what the sibling branch's fix will make en/de look like) — a test that only checked
/// "does not throw" would have passed on the actual WeekOf bug.
/// </para>
/// </summary>
internal static class SchedulerTitleFormatter
{
    /// <summary>
    /// Formats the toolbar title for <paramref name="view"/> anchored at <paramref name="date"/>.
    /// </summary>
    /// <param name="view">Which view's title convention to use.</param>
    /// <param name="date">The view's anchor date (e.g. the currently-focused day/week/month).</param>
    /// <param name="firstDayOfWeek">Used only by <see cref="SchedulerView.Week"/> to resolve the week's start via <see cref="SchedulerDateMath.StartOfWeek"/>.</param>
    /// <param name="localizer">Source of localized strings (only <c>Scheduler.WeekOf</c> is looked up today).</param>
    /// <param name="culture">Culture used for plain date/month/weekday formatting.</param>
    internal static string Format(SchedulerView view, DateTime date, DayOfWeek firstDayOfWeek, ILumeoLocalizer localizer, CultureInfo culture) => view switch
    {
        SchedulerView.Month => date.ToString("MMMM yyyy", culture),
        SchedulerView.Week => localizer["Scheduler.WeekOf", SchedulerDateMath.StartOfWeek(date, firstDayOfWeek).ToString("MMM d, yyyy", culture)],
        SchedulerView.Day => date.ToString("dddd, MMM d, yyyy", culture),
        SchedulerView.List => date.ToString("MMMM yyyy", culture),
        _ => date.ToString("MMMM yyyy", culture),
    };
}
