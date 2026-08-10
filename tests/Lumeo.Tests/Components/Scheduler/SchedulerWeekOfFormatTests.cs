using System.Globalization;
using Microsoft.Extensions.Options;
using Xunit;
using Lumeo.Services.Localization;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Regression test for the wave-0 Scheduler i18n bug: en/de's
/// <c>"Scheduler.WeekOf"</c> format string had no <c>{0}</c> placeholder
/// (<c>LumeoDefaultStrings.cs</c> — <c>["Scheduler.WeekOf"] = "Week of"</c> / <c>"Woche
/// vom"</c>), so <c>Scheduler.razor</c>'s <c>ComputeTitle</c> — which calls
/// <c>string.Format(L["Scheduler.WeekOf"], StartOfWeek(date).ToString("MMM d, yyyy"))</c>
/// for the Week view — silently dropped the date argument: <c>string.Format</c> ignores
/// extra arguments when the format string contains no placeholder, so it returns the
/// literal template unchanged instead of throwing.
///
/// A "does not throw" test would have passed on the buggy code (string.Format doesn't
/// throw here — it just quietly drops the argument). This asserts the actual date
/// substring is present in the formatted title, mirroring exactly the format call
/// Scheduler.razor's Week-view title computes, against the shared localizer/string
/// table rather than through a full component render — the full-render path can't
/// observe this locally, since Scheduler.razor immediately overwrites its pre-init
/// title with FullCalendar's own <c>view.title</c> once init completes (bug #2's own
/// mock returns "" in the bUnit harness, which would mask this bug either way).
/// </summary>
public class SchedulerWeekOfFormatTests
{
    // Same StartOfWeek(date).ToString("MMM d, yyyy") shape Scheduler.razor's
    // ComputeTitle produces for the Week view.
    private static readonly DateTime WeekStart = new(2026, 8, 10); // a Monday

    private static ILumeoLocalizer BuildLocalizer()
    {
        var options = new LumeoLocalizationOptions();
        LumeoDefaultStrings.ApplyDefaults(options);
        return new LumeoLocalizer(Options.Create(options));
    }

    private static void WithUICulture(string culture, Action body)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            body();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void WeekOf_English_Title_Contains_The_Date()
    {
        var localizer = BuildLocalizer();
        WithUICulture("en-US", () =>
        {
            var dateString = WeekStart.ToString("MMM d, yyyy");
            // Exactly Scheduler.razor's ComputeTitle Week-view branch.
            var title = string.Format(localizer["Scheduler.WeekOf"], dateString);

            // Predicted WRONG value on the buggy code: title == "Week of" (the date
            // argument silently dropped) — this Contains assertion would fail.
            Assert.Contains(dateString, title);
            Assert.Equal($"Week of {dateString}", title);
        });
    }

    [Fact]
    public void WeekOf_German_Title_Contains_The_Date()
    {
        var localizer = BuildLocalizer();
        WithUICulture("de-DE", () =>
        {
            var dateString = WeekStart.ToString("MMM d, yyyy");
            var title = string.Format(localizer["Scheduler.WeekOf"], dateString);

            // Predicted WRONG value on the buggy code: title == "Woche vom" (no date).
            Assert.Contains(dateString, title);
            Assert.Equal($"Woche vom {dateString}", title);
        });
    }
}
