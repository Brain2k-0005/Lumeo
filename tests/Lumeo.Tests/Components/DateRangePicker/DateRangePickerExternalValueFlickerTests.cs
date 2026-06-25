using Bunit;
using Xunit;
using Lumeo.Services.Localization;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateRangePicker;

/// <summary>
/// Battle-test #56 (state-on-data-change), exercised through the public
/// <see cref="L.DateRangePicker"/> → DatePicker (Mode=Range) → Calendar path.
///
/// Repro: the bound StartDate flickers to null and back to the SAME date during
/// an async refresh. The user has manually browsed to a later month in the
/// meantime. The null→value round trip must be a no-op and leave the browsed
/// month put — only a genuinely-new anchor should re-navigate the displayed
/// month. The underlying guard lives in the shared Calendar.razor OnParametersSet
/// (`anchor.HasValue &amp;&amp; anchor != _lastSeenAnchor`); this test pins the
/// behaviour at the DateRangePicker consumer surface so a regression in the
/// wrapper chain (NumberOfMonths=2, RangeStart anchor) is caught too.
/// </summary>
public class DateRangePickerExternalValueFlickerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DateRangePickerExternalValueFlickerTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static string MonthHeader(DateOnly d) => $"{d.ToString("MMMM")} {d.Year}";

    [Fact]
    public void StartDate_Empty_Then_Refilled_Same_Anchor_Does_Not_Yank_Browsed_Month()
    {
        var start = new DateOnly(2024, 3, 10);
        var cut = _ctx.Render<L.DateRangePicker>(p => p.Add(c => c.StartDate, start));

        // Open the range popover via the trigger button — only then does the
        // inner two-month Calendar render.
        cut.Find("button[type='button']").Click();

        // The range picker shows two panels: the start month and the next month.
        Assert.Contains(MonthHeader(new DateOnly(2024, 3, 1)), cut.Markup); // March 2024
        Assert.Contains(MonthHeader(new DateOnly(2024, 4, 1)), cut.Markup); // April 2024

        // Browse forward one month using the Next-month control (resolve its
        // localized aria-label from the same localizer the component uses so the
        // assertion stays culture-agnostic).
        var localizer = _ctx.Services.GetRequiredService<ILumeoLocalizer>();
        var nextLabel = localizer["Calendar.NextMonth"];
        cut.FindAll($"button[aria-label='{nextLabel}']")[0].Click();

        // Now panels are April + May; March has scrolled out of view.
        Assert.Contains(MonthHeader(new DateOnly(2024, 5, 1)), cut.Markup);  // May 2024
        Assert.DoesNotContain(MonthHeader(new DateOnly(2024, 3, 1)), cut.Markup);

        // Async load flicker: StartDate transiently clears …
        cut.Render(p => p.Add(c => c.StartDate, (DateOnly?)null));
        // … then refills with the very same anchor it had before.
        cut.Render(p => p.Add(c => c.StartDate, start));

        // The browsed window (April + May) must survive the round trip — the
        // refill must NOT yank the display back to the March anchor.
        Assert.Contains(MonthHeader(new DateOnly(2024, 5, 1)), cut.Markup);
        Assert.DoesNotContain(MonthHeader(new DateOnly(2024, 3, 1)), cut.Markup);
    }
}
