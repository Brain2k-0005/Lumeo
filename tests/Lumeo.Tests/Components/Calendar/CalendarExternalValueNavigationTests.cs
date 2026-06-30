using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Calendar;

/// <summary>
/// Regression tests: DisplayDate was only seeded in OnInitialized, so changing
/// the bound Value (or RangeStart) externally never navigated the visible
/// month — bad for inline calendars driven programmatically. The fix reacts in
/// OnParametersSet but only to *actual* anchor changes, so unrelated re-renders
/// never stomp the user's manual month browsing.
/// </summary>
public class CalendarExternalValueNavigationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CalendarExternalValueNavigationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Match the month-panel grid's aria-label ("MMMM yyyy") rather than a bare
    // "March 2024" substring of the whole markup: an adjacent-month day cell —
    // e.g. "31 March 2024" under the invariant culture's "dd MMMM yyyy" long-date
    // format (CI runs without a locale → invariant) — would otherwise trip
    // Assert.DoesNotContain. The aria-label="…" attribute is unique per rendered
    // panel and uses the same ToString the component does, so it stays culture-robust.
    private static string MonthHeader(DateOnly d) => $"aria-label=\"{d.ToString("MMMM yyyy")}\"";

    [Fact]
    public void External_Value_Change_Navigates_Displayed_Month()
    {
        var cut = _ctx.Render<L.Calendar>(p => p.Add(c => c.Value, new DateOnly(2024, 3, 15)));
        Assert.Contains(MonthHeader(new DateOnly(2024, 3, 1)), cut.Markup);

        cut.Render(p => p.Add(c => c.Value, new DateOnly(2024, 6, 20)));

        Assert.Contains(MonthHeader(new DateOnly(2024, 6, 1)), cut.Markup);
        Assert.DoesNotContain(MonthHeader(new DateOnly(2024, 3, 1)), cut.Markup);
    }

    [Fact]
    public void External_RangeStart_Change_Navigates_Displayed_Month()
    {
        var cut = _ctx.Render<L.Calendar>(p => p
            .Add(c => c.IsRange, true)
            .Add(c => c.RangeStart, new DateOnly(2024, 3, 10)));
        Assert.Contains(MonthHeader(new DateOnly(2024, 3, 1)), cut.Markup);

        cut.Render(p => p.Add(c => c.RangeStart, new DateOnly(2024, 8, 5)));

        Assert.Contains(MonthHeader(new DateOnly(2024, 8, 1)), cut.Markup);
    }

    [Fact]
    public void Unrelated_ReRender_Does_Not_Stomp_Manual_Month_Browsing()
    {
        var value = new DateOnly(2024, 3, 15);
        var cut = _ctx.Render<L.Calendar>(p => p.Add(c => c.Value, value));

        // Browse to the next month manually (buttons: prev, header, next).
        cut.FindAll("button[type='button']")[2].Click();
        Assert.Contains(MonthHeader(new DateOnly(2024, 4, 1)), cut.Markup);

        // Re-render with the SAME bound Value — the user's browsing must survive.
        cut.Render(p => p.Add(c => c.Value, value));

        Assert.Contains(MonthHeader(new DateOnly(2024, 4, 1)), cut.Markup);
    }

    [Fact]
    public void Clearing_Value_Keeps_Current_Displayed_Month()
    {
        var cut = _ctx.Render<L.Calendar>(p => p.Add(c => c.Value, new DateOnly(2024, 3, 15)));

        cut.Render(p => p.Add(c => c.Value, (DateOnly?)null));

        // No anchor to navigate to — stay where we were instead of jumping.
        Assert.Contains(MonthHeader(new DateOnly(2024, 3, 1)), cut.Markup);
    }

    [Fact]
    public void Value_Empty_Then_Refilled_Same_Anchor_Does_Not_Yank_Browsed_Month()
    {
        // #52 — async load flicker: the bound Value momentarily goes null then
        // refills with the SAME date it had before. The user has browsed away in
        // the meantime; the value→null→value round trip must be a no-op and leave
        // the browsed month put (only a genuinely-new anchor should navigate).
        var anchor = new DateOnly(2024, 3, 15);
        var cut = _ctx.Render<L.Calendar>(p => p.Add(c => c.Value, anchor));

        // Browse forward one month (prev, header, next → index 2 is next).
        cut.FindAll("button[type='button']")[2].Click();
        Assert.Contains(MonthHeader(new DateOnly(2024, 4, 1)), cut.Markup);

        // Transient clear (data reloading) …
        cut.Render(p => p.Add(c => c.Value, (DateOnly?)null));
        // … then refill with the very same anchor.
        cut.Render(p => p.Add(c => c.Value, anchor));

        // The browsed month must survive the flicker — NOT jump back to March.
        Assert.Contains(MonthHeader(new DateOnly(2024, 4, 1)), cut.Markup);
        Assert.DoesNotContain(MonthHeader(new DateOnly(2024, 3, 1)), cut.Markup);
    }

    [Fact]
    public void RangeStart_Empty_Then_Refilled_Same_Anchor_Does_Not_Yank_Browsed_Month()
    {
        // #56 (same OnParametersSet mechanism, range mode): RangeStart flickers to
        // null and back to the same value during an async refresh; the user's
        // manual month browsing must survive the round trip.
        var start = new DateOnly(2024, 3, 10);
        var cut = _ctx.Render<L.Calendar>(p => p
            .Add(c => c.IsRange, true)
            .Add(c => c.RangeStart, start));

        cut.FindAll("button[type='button']")[2].Click(); // browse to April
        Assert.Contains(MonthHeader(new DateOnly(2024, 4, 1)), cut.Markup);

        cut.Render(p => p.Add(c => c.RangeStart, (DateOnly?)null));
        cut.Render(p => p.Add(c => c.RangeStart, start));

        Assert.Contains(MonthHeader(new DateOnly(2024, 4, 1)), cut.Markup);
        Assert.DoesNotContain(MonthHeader(new DateOnly(2024, 3, 1)), cut.Markup);
    }

    [Fact]
    public void Day_Selection_Then_ReRender_Does_Not_Yank_Display_Back()
    {
        // Uncontrolled usage: select a day (internal Value mutation), browse to
        // another month, then force a re-render — the display must not jump
        // back to the selected day's month.
        var cut = _ctx.Render<L.Calendar>(p => p.Add(c => c.Value, new DateOnly(2024, 3, 15)));

        // Click a day of the displayed month (button text "10" with day-cell classes).
        cut.FindAll("button[type='button']")
            .First(b => b.TextContent.Trim() == "10" && (b.GetAttribute("class") ?? "").Contains("h-8 w-8"))
            .Click();

        // Browse forward one month.
        cut.FindAll("button[type='button']")[2].Click();
        Assert.Contains(MonthHeader(new DateOnly(2024, 4, 1)), cut.Markup);

        cut.Render();

        Assert.Contains(MonthHeader(new DateOnly(2024, 4, 1)), cut.Markup);
    }
}
