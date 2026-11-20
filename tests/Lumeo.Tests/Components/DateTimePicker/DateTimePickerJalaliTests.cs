using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateTimePicker;

/// <summary>
/// DateTimePicker CalendarSystem="Jalali" (#547): trigger text and the embedded calendar follow
/// the Persian calendar; Value stays a Gregorian DateTime. 2024-10-03 is 1403/07/12.
/// </summary>
public class DateTimePickerJalaliTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DateTimePickerJalaliTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DateTimePicker> Render(
        L.CalendarSystem system, string culture, DateTime? value = null, bool use24 = true,
        string? format = null, Action<DateTime?>? onChanged = null)
        => _ctx.Render<L.DateTimePicker>(p =>
        {
            p.Add(c => c.CalendarSystem, system);
            p.Add(c => c.Culture, new CultureInfo(culture));
            p.Add(c => c.Use24Hour, use24);
            if (value.HasValue) p.Add(c => c.Value, value.Value);
            if (format is not null) p.Add(c => c.DateFormat, format);
            if (onChanged is not null)
                p.Add(c => c.ValueChanged, EventCallback.Factory.Create<DateTime?>(this, onChanged));
        });

    private static string Trigger(IRenderedComponent<L.DateTimePicker> cut) =>
        cut.Find("button[type=button] span").TextContent.Trim();

    [Fact]
    public void Trigger_Shows_Jalali_Date_And_24h_Time_By_Default()
    {
        var cut = Render(L.CalendarSystem.Jalali, "en-US", new DateTime(2024, 10, 3, 14, 5, 0));
        Assert.Equal("1403/07/12 14:05", Trigger(cut));
    }

    [Fact]
    public void Trigger_Shows_Persian_Digits_And_Culture_AmPm_Under_Fa()
    {
        var fa = new CultureInfo("fa-IR");
        var cut = Render(L.CalendarSystem.Jalali, "fa-IR", new DateTime(2024, 10, 3, 14, 5, 0), use24: false);
        var text = Trigger(cut);
        Assert.StartsWith("۱۴۰۳/۰۷/۱۲ ", text);
        Assert.Contains("۰۲:۰۵", text);
        Assert.Contains(fa.DateTimeFormat.PMDesignator, text);
    }

    [Fact]
    public void Custom_DateFormat_Resolves_Against_Jalali()
    {
        var cut = Render(L.CalendarSystem.Jalali, "en-US", new DateTime(2024, 10, 3, 9, 0, 0), format: "d MMMM yyyy");
        Assert.Equal("12 Mehr 1403 09:00", Trigger(cut));
    }

    [Fact]
    public void Gregorian_Is_Unchanged()
    {
        var cut = Render(L.CalendarSystem.Gregorian, "en-US", new DateTime(2024, 10, 3, 14, 5, 0));
        Assert.Equal("10/3/2024 14:05", Trigger(cut));
    }

    [Fact]
    public void Embedded_Calendar_Is_Jalali_With_Persian_Header_Under_Fa_Culture_Parameter()
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        try
        {
            var cut = Render(L.CalendarSystem.Jalali, "fa-IR", new DateTime(2024, 10, 3, 14, 5, 0));
            cut.Find("button[type=button]").Click();
            Assert.Equal("مهر ۱۴۰۳", cut.Find("[role=grid]").GetAttribute("aria-label"));
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Fact]
    public void Embedded_Calendar_Uses_Latin_Month_Names_Under_EnUs()
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        try
        {
            var cut = Render(L.CalendarSystem.Jalali, "en-US", new DateTime(2024, 10, 3, 14, 5, 0));
            cut.Find("button[type=button]").Click();
            Assert.Equal("Mehr 1403", cut.Find("[role=grid]").GetAttribute("aria-label"));
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Fact]
    public void Selecting_A_Jalali_Day_Emits_Gregorian_Value_And_Keeps_Time()
    {
        DateTime? got = null;
        var cut = Render(L.CalendarSystem.Jalali, "en-US", new DateTime(2024, 10, 3, 14, 5, 0), onChanged: v => got = v);
        cut.Find("button[type=button]").Click();
        var day = cut.FindAll("[role=grid] button").First(b => b.TextContent.Trim() == "1");
        day.Click();
        Assert.Equal(new DateTime(2024, 9, 22, 14, 5, 0), got); // 1 Mehr 1403 = 2024-09-22
    }
}
