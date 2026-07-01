using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TimePicker;

/// <summary>
/// Battle-wave2 #8 and #9 (both HIGH) for the List-variant TimePicker, exercised
/// with <c>Value == null</c>:
///
/// #8 (edge-data) — in 12-hour mode the per-hour AM/PM inference that ENABLES an
/// hour (DisplayHourTo24) must also drive the COMMIT. Before the fix, an hour that
/// was enabled only under its PM interpretation (because AM was out of bounds) still
/// committed an AM time, landing out of [Min, Max].
///
/// #9 (keyboard-a11y) — when nothing is selected every option was tabindex=-1, so
/// each listbox column had no keyboard entry point. The first ENABLED option of each
/// column must now be the roving tab stop (tabindex=0).
/// </summary>
public class TimePickerNullValueBoundsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TimePickerNullValueBoundsTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static AngleSharp.Dom.IElement HoursColumn(IRenderedComponent<L.TimePicker> cut)
        => cut.FindAll("[role='listbox']")[0];

    private static AngleSharp.Dom.IElement HourOption(IRenderedComponent<L.TimePicker> cut, int hour)
        => HoursColumn(cut).QuerySelectorAll("button[role='option']")
            .First(b => b.TextContent.Trim() == hour.ToString("D2"));

    // ---- #8: AM/PM inference must agree between enable-check and commit ----

    [Fact]
    public void TwelveHour_NullValue_PmOnly_Hour_Commits_The_PM_Time_Not_AM()
    {
        // 12-hour, Value null, Min 14:00 (2 PM). Display-hour 2 is enabled because
        // its PM interpretation (14:00) is in bounds; its AM interpretation (02:00)
        // is below Min. Selecting it must commit 14:00, NOT a stale 02:00.
        TimeSpan? captured = null;
        var cb = EventCallback.Factory.Create<TimeSpan?>(this, (TimeSpan? t) => captured = t);
        var cut = _ctx.Render<L.TimePicker>(p =>
        {
            p.Add(c => c.Use24Hour, false);
            p.Add(c => c.Min, new TimeSpan(14, 0, 0));
            p.Add(c => c.ValueChanged, cb);
        });
        cut.Find("button").Click(); // open the popover

        // Display-hour 2 must be selectable (enabled) under the PM interpretation.
        Assert.False(HourOption(cut, 2).HasAttribute("disabled"));

        HourOption(cut, 2).Click();

        // Before the fix this committed 02:00 (out of bounds). After the fix the
        // commit resolves AM/PM the same way the enable check did -> 14:00.
        Assert.NotNull(captured);
        Assert.Equal(14, captured!.Value.Hours);
        Assert.True(captured.Value >= new TimeSpan(14, 0, 0),
            $"Committed {captured} is before Min 14:00 — out-of-bounds time was committed.");
    }

    // ---- #9: a roving keyboard entry point exists with no selection ----

    [Fact]
    public void NullValue_FirstEnabledOption_Of_Each_Column_Is_Tabbable()
    {
        // 24-hour, Value null. Every column must expose exactly one tabindex=0
        // option (the first enabled one) so the listbox is keyboard-reachable.
        var cut = _ctx.Render<L.TimePicker>(p => p.Add(c => c.Use24Hour, true));
        cut.Find("button").Click();

        var listboxes = cut.FindAll("[role='listbox']");
        Assert.True(listboxes.Count >= 2);

        foreach (var listbox in listboxes)
        {
            var options = listbox.QuerySelectorAll("button[role='option']").ToList();
            var tabbable = options.Where(o => o.GetAttribute("tabindex") == "0").ToList();

            // Exactly one roving tab stop per column.
            Assert.Single(tabbable);
            // It is the FIRST enabled option (hours/minutes start at the first item).
            Assert.False(tabbable[0].HasAttribute("disabled"));
            var firstEnabled = options.First(o => !o.HasAttribute("disabled"));
            Assert.Same(firstEnabled, tabbable[0]);
        }
    }

    [Fact]
    public void NullValue_With_Min_RovingRoot_Skips_Disabled_Leading_Hours()
    {
        // Min 09:00 disables hours 00..08; the roving tab stop must be hour 09,
        // not the (disabled) leading 00.
        var cut = _ctx.Render<L.TimePicker>(p =>
        {
            p.Add(c => c.Use24Hour, true);
            p.Add(c => c.Min, new TimeSpan(9, 0, 0));
        });
        cut.Find("button").Click();

        Assert.Equal("-1", HourOption(cut, 0).GetAttribute("tabindex"));
        Assert.True(HourOption(cut, 0).HasAttribute("disabled"));
        Assert.Equal("0", HourOption(cut, 9).GetAttribute("tabindex"));
    }
}
