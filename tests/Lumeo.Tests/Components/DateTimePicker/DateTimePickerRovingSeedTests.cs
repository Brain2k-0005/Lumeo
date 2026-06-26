using System.Globalization;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateTimePicker;

/// <summary>
/// Battle-test keyboard/a11y regressions for DateTimePicker:
///
/// n=33 — when no time is selected (Value is null) every time-column option used
/// to render tabindex="-1", leaving each listbox with NO tabbable option, so the
/// columns were entirely keyboard-unreachable. Each column must now seed a single
/// roving tab stop on its first ENABLED option.
///
/// n=149 — the 12-hour AM/PM toggle exposed its active state only via colour. The
/// toggle buttons must now carry aria-pressed so assistive tech can read which
/// half-of-day is active.
/// </summary>
public class DateTimePickerRovingSeedTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DateTimePickerRovingSeedTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DateTimePicker> RenderOpen(bool use24Hour = true, Action<ComponentParameterCollectionBuilder<L.DateTimePicker>>? extra = null)
    {
        var cut = _ctx.Render<L.DateTimePicker>(p =>
        {
            p.Add(c => c.Use24Hour, use24Hour);
            extra?.Invoke(p);
        });
        cut.Find("button[type='button']").Click(); // open the popover
        return cut;
    }

    private static List<AngleSharp.Dom.IElement> Options(AngleSharp.Dom.IElement listbox)
        => listbox.QuerySelectorAll("button[role='option']").ToList();

    // The component derives the AM/PM captions from the effective culture; mirror
    // that so the test is culture-independent (it just defaults to CurrentCulture).
    private static string AmLabel
        => string.IsNullOrEmpty(CultureInfo.CurrentCulture.DateTimeFormat.AMDesignator)
            ? "AM" : CultureInfo.CurrentCulture.DateTimeFormat.AMDesignator;
    private static string PmLabel
        => string.IsNullOrEmpty(CultureInfo.CurrentCulture.DateTimeFormat.PMDesignator)
            ? "PM" : CultureInfo.CurrentCulture.DateTimeFormat.PMDesignator;

    // ---- n=33 : roving tab stop seeded when nothing is selected ----

    [Fact]
    public void With_No_Value_Each_Time_Column_Has_Exactly_One_Tab_Stop()
    {
        var cut = RenderOpen(extra: p => p.Add(c => c.ShowSeconds, true)); // Value stays null

        foreach (var listbox in cut.FindAll("[role='listbox']"))
        {
            var tabbable = Options(listbox).Count(o => o.GetAttribute("tabindex") == "0");
            // Without the fix this is 0 (every option roved out) — the column is
            // keyboard-unreachable. With the fix it is exactly 1.
            Assert.Equal(1, tabbable);
        }
    }

    [Fact]
    public void With_No_Value_The_Seeded_Tab_Stop_Is_The_First_Option()
    {
        var cut = RenderOpen(); // Value null, 24h: hours start at 00

        var hoursCol = cut.FindAll("[role='listbox']")[0];
        var options = Options(hoursCol);

        Assert.Equal("0", options[0].GetAttribute("tabindex"));   // first option is tabbable
        Assert.Equal("-1", options[1].GetAttribute("tabindex"));  // the rest roved out
    }

    [Fact]
    public void With_No_Value_The_Seed_Skips_Disabled_Leading_Options()
    {
        // MinDate 09:00 on the selected day disables hours 00-08. With no time
        // selected the seeded tab stop must land on the first ENABLED hour (09),
        // not the disabled 00.
        var cut = RenderOpen(extra: p =>
        {
            p.Add(c => c.MinDate, new DateTime(2026, 6, 10, 9, 0, 0));
            // A date (but no usable time match) so MinTimeForSelectedDate bites.
            // _dateValue is seeded from Value's date; pick a Value on the min day
            // whose time is null-equivalent by clearing it afterwards is awkward,
            // so instead drive the date through the calendar below.
        });

        // Pick the min day (10) in the calendar so the hour bounds apply, but the
        // time itself is still effectively unselected for the seed (00:00 default
        // is disabled, so no enabled option equals the live time -> seed kicks in).
        cut.FindAll("button")
            .First(b => b.TextContent.Trim() == "10" && (b.GetAttribute("class") ?? "").Contains("h-8 w-8"))
            .Click();

        var hoursCol = cut.FindAll("[role='listbox']")[0];
        var options = Options(hoursCol);

        var hour00 = options.First(o => o.TextContent.Trim() == "00");
        var hour09 = options.First(o => o.TextContent.Trim() == "09");

        Assert.True(hour00.HasAttribute("disabled"));
        Assert.Equal("-1", hour00.GetAttribute("tabindex"));   // disabled leading option is NOT the seed
        Assert.Equal("0", hour09.GetAttribute("tabindex"));    // first enabled option is the seed
    }

    // ---- n=149 : AM/PM toggle exposes aria-pressed ----

    [Fact]
    public void AmPm_Toggle_Exposes_Aria_Pressed()
    {
        // 12-hour mode renders the AM/PM column. With no time selected the default
        // is AM, so AM is pressed and PM is not.
        var cut = RenderOpen(use24Hour: false);

        var am = cut.FindAll("button").First(b => b.TextContent.Trim() == AmLabel);
        var pm = cut.FindAll("button").First(b => b.TextContent.Trim() == PmLabel);

        // Without the fix aria-pressed is absent (null) on both.
        Assert.Equal("true", am.GetAttribute("aria-pressed"));
        Assert.Equal("false", pm.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void AmPm_Aria_Pressed_Tracks_The_Active_Half()
    {
        // A PM value -> PM pressed, AM not.
        var cut = RenderOpen(use24Hour: false, extra: p => p.Add(c => c.Value, new DateTime(2026, 6, 10, 15, 0, 0))); // 3 PM

        var am = cut.FindAll("button").First(b => b.TextContent.Trim() == AmLabel);
        var pm = cut.FindAll("button").First(b => b.TextContent.Trim() == PmLabel);

        Assert.Equal("false", am.GetAttribute("aria-pressed"));
        Assert.Equal("true", pm.GetAttribute("aria-pressed"));
    }
}
