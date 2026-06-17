using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateTimePicker;

/// <summary>
/// #208 — the DateTimePicker time columns now honour MinDate/MaxDate
/// (date-aware: the bound only bites on the boundary day) and a MinuteStep /
/// SecondStep increment. Out-of-range options render disabled and refuse to
/// commit.
/// </summary>
public class DateTimePickerMinMaxStepTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DateTimePickerMinMaxStepTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DateTimePicker> Render(Action<ComponentParameterCollectionBuilder<L.DateTimePicker>> extra)
        => _ctx.Render<L.DateTimePicker>(p =>
        {
            p.Add(c => c.Use24Hour, true);
            extra(p);
        });

    private static AngleSharp.Dom.IElement HoursColumn(IRenderedComponent<L.DateTimePicker> cut)
        => cut.FindAll("[role='listbox']")[0];

    private static AngleSharp.Dom.IElement HourOption(IRenderedComponent<L.DateTimePicker> cut, int hour)
        => HoursColumn(cut).QuerySelectorAll("button[role='option']")
            .First(b => b.TextContent.Trim() == hour.ToString("D2"));

    [Fact]
    public void MinuteStep_Lists_Only_Multiples()
    {
        var cut = Render(p => p.Add(c => c.MinuteStep, 15));
        cut.Find("button").Click();

        var minutesCol = cut.FindAll("[role='listbox']")[1];
        var labels = minutesCol.QuerySelectorAll("button[role='option']").Select(b => b.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "00", "15", "30", "45" }, labels);
    }

    [Fact]
    public void On_The_Min_Day_Hours_Before_Min_Time_Are_Disabled()
    {
        // Value is on the same day as MinDate (2026-06-10 09:00); hours < 9 disabled.
        var cut = Render(p =>
        {
            p.Add(c => c.MinDate, new DateTime(2026, 6, 10, 9, 0, 0));
            p.Add(c => c.Value, new DateTime(2026, 6, 10, 12, 0, 0));
        });
        cut.Find("button").Click();

        Assert.True(HourOption(cut, 8).HasAttribute("disabled"));
        Assert.False(HourOption(cut, 9).HasAttribute("disabled"));
        Assert.False(HourOption(cut, 23).HasAttribute("disabled"));
    }

    [Fact]
    public void On_The_Max_Day_Hours_After_Max_Time_Are_Disabled()
    {
        var cut = Render(p =>
        {
            p.Add(c => c.MaxDate, new DateTime(2026, 6, 10, 17, 0, 0));
            p.Add(c => c.Value, new DateTime(2026, 6, 10, 12, 0, 0));
        });
        cut.Find("button").Click();

        Assert.False(HourOption(cut, 17).HasAttribute("disabled"));
        Assert.True(HourOption(cut, 18).HasAttribute("disabled"));
    }

    [Fact]
    public void Clicking_A_Disabled_Hour_Does_Not_Commit()
    {
        DateTime? captured = null;
        var cb = EventCallback.Factory.Create<DateTime?>(this, (DateTime? d) => captured = d);
        var cut = Render(p =>
        {
            p.Add(c => c.MinDate, new DateTime(2026, 6, 10, 9, 0, 0));
            p.Add(c => c.Value, new DateTime(2026, 6, 10, 12, 0, 0));
            p.Add(c => c.ValueChanged, cb);
        });
        cut.Find("button").Click();

        HourOption(cut, 3).Click(); // disabled (before 09:00 on the min day)
        Assert.Null(captured);

        HourOption(cut, 14).Click(); // valid
        Assert.NotNull(captured);
        Assert.Equal(14, captured!.Value.Hour);
    }

    [Fact]
    public void Time_Columns_Are_Unconstrained_On_Days_Inside_The_Range()
    {
        // Value (2026-06-15) is strictly between Min day (06-10) and Max day
        // (06-20), so the whole clock is selectable.
        var cut = Render(p =>
        {
            p.Add(c => c.MinDate, new DateTime(2026, 6, 10, 9, 0, 0));
            p.Add(c => c.MaxDate, new DateTime(2026, 6, 20, 17, 0, 0));
            p.Add(c => c.Value, new DateTime(2026, 6, 15, 12, 0, 0));
        });
        cut.Find("button").Click();

        Assert.False(HourOption(cut, 0).HasAttribute("disabled"));
        Assert.False(HourOption(cut, 23).HasAttribute("disabled"));
    }
}
