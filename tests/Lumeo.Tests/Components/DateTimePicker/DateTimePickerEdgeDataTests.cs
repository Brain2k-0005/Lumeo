using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateTimePicker;

/// <summary>
/// Battle-test edge-data regressions for DateTimePicker:
///
/// n=31 — changing the date to a boundary day used to commit the carried-over
/// (stale) time even when it fell past MinDate/MaxDate's time-of-day for that
/// day. The time columns gate clicks, but the date-change path applied no clamp,
/// so the committed Value (and the highlighted column option) could be out of
/// range. UpdateValue now clamps the time against the per-day Min/Max bounds and
/// writes the clamped time back into _timeValue so columns and Value agree.
///
/// n=32 — a Value whose minute/second isn't aligned to MinuteStep/SecondStep
/// listed no exact match, so no option rendered aria-selected="true" and the
/// column's roving tab stop fell to the first option rather than the nearest
/// step. Selection now snaps to the nearest listed step value.
/// </summary>
public class DateTimePickerEdgeDataTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DateTimePickerEdgeDataTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DateTimePicker> Render(Action<ComponentParameterCollectionBuilder<L.DateTimePicker>> extra)
        => _ctx.Render<L.DateTimePicker>(p =>
        {
            p.Add(c => c.Use24Hour, true);
            extra(p);
        });

    private static AngleSharp.Dom.IElement Listbox(IRenderedComponent<L.DateTimePicker> cut, int index)
        => cut.FindAll("[role='listbox']")[index];

    private static List<AngleSharp.Dom.IElement> Options(AngleSharp.Dom.IElement listbox)
        => listbox.QuerySelectorAll("button[role='option']").ToList();

    private static void ClickCalendarDay(IRenderedComponent<L.DateTimePicker> cut, string day)
        => cut.FindAll("button")
            .First(b => b.TextContent.Trim() == day && (b.GetAttribute("class") ?? "").Contains("h-8 w-8"))
            .Click();

    // ---- n=31 : boundary-day date change clamps a stale out-of-range time ----

    [Fact]
    public void Changing_To_The_Max_Day_Clamps_A_Stale_Late_Time()
    {
        // Value 2026-06-05 23:00 — day 5 is strictly before the max day (10), so
        // 23:00 is in range there. MaxDate caps the time at 17:00 on day 10.
        DateTime? committed = null;
        var cb = EventCallback.Factory.Create<DateTime?>(this, (DateTime? d) => committed = d);
        var cut = Render(p =>
        {
            p.Add(c => c.MaxDate, new DateTime(2026, 6, 10, 17, 0, 0));
            p.Add(c => c.Value, new DateTime(2026, 6, 5, 23, 0, 0));
            p.Add(c => c.ValueChanged, cb);
        });
        cut.Find("button[type='button']").Click();

        // Pick the max boundary day in the calendar; the carried 23:00 is past 17:00.
        ClickCalendarDay(cut, "10");

        Assert.NotNull(committed);
        // Without the fix the committed time is the stale 23:00 (out of range).
        // With the fix it is clamped to the max time-of-day for that day, 17:00.
        Assert.Equal(new DateTime(2026, 6, 10, 17, 0, 0), committed!.Value);
    }

    [Fact]
    public void Changing_To_The_Min_Day_Clamps_A_Stale_Early_Time()
    {
        // Value 2026-06-15 02:00 — day 15 is strictly after the min day (10), so
        // 02:00 is in range there. MinDate floors the time at 09:00 on day 10.
        DateTime? committed = null;
        var cb = EventCallback.Factory.Create<DateTime?>(this, (DateTime? d) => committed = d);
        var cut = Render(p =>
        {
            p.Add(c => c.MinDate, new DateTime(2026, 6, 10, 9, 0, 0));
            p.Add(c => c.Value, new DateTime(2026, 6, 15, 2, 0, 0));
            p.Add(c => c.ValueChanged, cb);
        });
        cut.Find("button[type='button']").Click();

        ClickCalendarDay(cut, "10");

        Assert.NotNull(committed);
        // Stale 02:00 floored up to the min time-of-day, 09:00.
        Assert.Equal(new DateTime(2026, 6, 10, 9, 0, 0), committed!.Value);
    }

    [Fact]
    public void After_Clamping_The_Time_Column_Highlights_The_Clamped_Value()
    {
        // After clamping the committed Value to 17:00 on the max day, the hours
        // column must highlight 17 (not the stale 23) — columns and Value agree.
        var cut = Render(p =>
        {
            p.Add(c => c.MaxDate, new DateTime(2026, 6, 10, 17, 0, 0));
            p.Add(c => c.Value, new DateTime(2026, 6, 5, 23, 0, 0));
        });
        cut.Find("button[type='button']").Click();
        ClickCalendarDay(cut, "10");

        var selected = Options(Listbox(cut, 0))
            .Where(o => (o.GetAttribute("class") ?? "").Contains("bg-primary"))
            .Select(o => o.TextContent.Trim())
            .ToList();
        // Exactly the clamped hour is highlighted, and the stale 23 is gone.
        Assert.Contains("17", selected);
        Assert.DoesNotContain("23", selected);
    }

    [Fact]
    public void Date_Change_On_An_In_Range_Day_Leaves_The_Time_Untouched()
    {
        // Normal-path preservation: switching to a day strictly inside the range
        // must commit the carried time unchanged (no spurious clamp).
        DateTime? committed = null;
        var cb = EventCallback.Factory.Create<DateTime?>(this, (DateTime? d) => committed = d);
        var cut = Render(p =>
        {
            p.Add(c => c.MinDate, new DateTime(2026, 6, 1, 9, 0, 0));
            p.Add(c => c.MaxDate, new DateTime(2026, 6, 30, 17, 0, 0));
            p.Add(c => c.Value, new DateTime(2026, 6, 5, 13, 30, 0));
            p.Add(c => c.ValueChanged, cb);
        });
        cut.Find("button[type='button']").Click();

        ClickCalendarDay(cut, "15"); // strictly inside [01, 30]

        Assert.NotNull(committed);
        Assert.Equal(new DateTime(2026, 6, 15, 13, 30, 0), committed!.Value);
    }

    // ---- n=32 : unaligned Value snaps selection to the nearest listed step ----

    [Fact]
    public void Unaligned_Minute_Value_Highlights_The_Nearest_Step()
    {
        // 37 minutes with MinuteStep=15 lists no exact match (00/15/30/45). The
        // nearest step is 30 (|37-30|=7 < |37-45|=8) — it must be the highlighted
        // /selected option.
        var cut = Render(p =>
        {
            p.Add(c => c.MinuteStep, 15);
            p.Add(c => c.Value, new DateTime(2026, 6, 10, 8, 37, 0));
        });
        cut.Find("button[type='button']").Click();

        var minutesCol = Listbox(cut, 1);
        var selected = Options(minutesCol)
            .Where(o => o.GetAttribute("aria-selected") == "true")
            .Select(o => o.TextContent.Trim())
            .ToList();

        // Without the fix NO option is selected (exact equality found no 37).
        Assert.Single(selected);
        Assert.Equal("30", selected[0]);
    }

    [Fact]
    public void Unaligned_Minute_Value_Keeps_The_Column_Keyboard_Reachable()
    {
        // The seeded roving tab stop must land on the nearest step (30), not the
        // first option (00) — exactly one option is tabbable and it is the match.
        var cut = Render(p =>
        {
            p.Add(c => c.MinuteStep, 15);
            p.Add(c => c.Value, new DateTime(2026, 6, 10, 8, 37, 0));
        });
        cut.Find("button[type='button']").Click();

        var options = Options(Listbox(cut, 1));
        var tabbable = options.Where(o => o.GetAttribute("tabindex") == "0").ToList();

        Assert.Single(tabbable);
        Assert.Equal("30", tabbable[0].TextContent.Trim());
    }

    [Fact]
    public void Unaligned_Second_Value_Highlights_The_Nearest_Step()
    {
        // 52 seconds with SecondStep=10 lists 00/10/.../50 — nearest is 50.
        var cut = Render(p =>
        {
            p.Add(c => c.ShowSeconds, true);
            p.Add(c => c.SecondStep, 10);
            p.Add(c => c.Value, new DateTime(2026, 6, 10, 8, 0, 52));
        });
        cut.Find("button[type='button']").Click();

        var secondsCol = Listbox(cut, 2);
        var selected = Options(secondsCol)
            .Where(o => o.GetAttribute("aria-selected") == "true")
            .Select(o => o.TextContent.Trim())
            .ToList();

        Assert.Single(selected);
        Assert.Equal("50", selected[0]);
    }

    [Fact]
    public void Aligned_Minute_Value_Still_Selects_The_Exact_Option()
    {
        // Normal-path preservation: an aligned Value (30 with step 15) selects the
        // exact option, exactly once.
        var cut = Render(p =>
        {
            p.Add(c => c.MinuteStep, 15);
            p.Add(c => c.Value, new DateTime(2026, 6, 10, 8, 30, 0));
        });
        cut.Find("button[type='button']").Click();

        var selected = Options(Listbox(cut, 1))
            .Where(o => o.GetAttribute("aria-selected") == "true")
            .Select(o => o.TextContent.Trim())
            .ToList();

        Assert.Single(selected);
        Assert.Equal("30", selected[0]);
    }
}
