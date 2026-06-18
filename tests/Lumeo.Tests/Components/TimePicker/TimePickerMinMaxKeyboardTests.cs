using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TimePicker;

/// <summary>
/// #209 — the List-variant TimePicker now (1) disables hour/minute/second
/// options outside [Min, Max] and refuses to commit them, and (2) supports
/// arrow-key navigation within each column (ArrowUp/Down move to the next
/// enabled option, Home/End jump to the ends, Enter/Space select).
/// </summary>
public class TimePickerMinMaxKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TimePickerMinMaxKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.TimePicker> Render(Action<ComponentParameterCollectionBuilder<L.TimePicker>>? extra = null)
        => _ctx.Render<L.TimePicker>(p =>
        {
            p.Add(c => c.Use24Hour, true);
            extra?.Invoke(p);
        });

    private static void Open(IRenderedComponent<L.TimePicker> cut)
        => cut.Find("button[aria-haspopup], button").Click();

    // The hours column is the first listbox.
    private static AngleSharp.Dom.IElement HoursColumn(IRenderedComponent<L.TimePicker> cut)
        => cut.FindAll("[role='listbox']")[0];

    private static AngleSharp.Dom.IElement HourOption(IRenderedComponent<L.TimePicker> cut, int hour)
        => HoursColumn(cut).QuerySelectorAll("button[role='option']")
            .First(b => b.TextContent.Trim() == hour.ToString("D2"));

    // --- Min/Max disabling ---

    [Fact]
    public void Hours_Before_Min_Are_Disabled()
    {
        var cut = Render(p =>
        {
            p.Add(c => c.Min, new TimeSpan(9, 0, 0));
            p.Add(c => c.Max, new TimeSpan(17, 0, 0));
        });
        cut.Find("button").Click();

        Assert.True(HourOption(cut, 8).HasAttribute("disabled"));
        Assert.False(HourOption(cut, 9).HasAttribute("disabled"));
        Assert.False(HourOption(cut, 17).HasAttribute("disabled"));
        Assert.True(HourOption(cut, 18).HasAttribute("disabled"));
    }

    [Fact]
    public void Clicking_A_Disabled_Hour_Does_Not_Commit()
    {
        TimeSpan? captured = null;
        var cb = EventCallback.Factory.Create<TimeSpan?>(this, (TimeSpan? t) => captured = t);
        var cut = Render(p =>
        {
            p.Add(c => c.Min, new TimeSpan(9, 0, 0));
            p.Add(c => c.ValueChanged, cb);
        });
        cut.Find("button").Click();

        HourOption(cut, 3).Click(); // before Min — disabled
        Assert.Null(captured);

        HourOption(cut, 10).Click(); // valid
        Assert.NotNull(captured);
        Assert.Equal(10, captured!.Value.Hours);
    }

    [Fact]
    public void Minutes_On_Min_Hour_Respect_The_Minute_Bound()
    {
        // Min 09:30 — with hour 9 selected, minutes < 30 are disabled.
        var cut = Render(p =>
        {
            p.Add(c => c.Min, new TimeSpan(9, 30, 0));
            p.Add(c => c.Value, new TimeSpan(9, 45, 0));
        });
        cut.Find("button").Click();

        var minutesCol = cut.FindAll("[role='listbox']")[1];
        AngleSharp.Dom.IElement Minute(int m) => minutesCol.QuerySelectorAll("button[role='option']")
            .First(b => b.TextContent.Trim() == m.ToString("D2"));

        Assert.True(Minute(15).HasAttribute("disabled"));
        Assert.False(Minute(30).HasAttribute("disabled"));
        Assert.False(Minute(45).HasAttribute("disabled"));
    }

    // --- MinuteStep already supported; assert it still lists multiples ---

    [Fact]
    public void MinuteStep_Lists_Only_Multiples()
    {
        var cut = Render(p => p.Add(c => c.MinuteStep, 15));
        cut.Find("button").Click();

        var minutesCol = cut.FindAll("[role='listbox']")[1];
        var labels = minutesCol.QuerySelectorAll("button[role='option']").Select(b => b.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "00", "15", "30", "45" }, labels);
    }

    // --- Keyboard navigation ---

    [Fact]
    public void ArrowDown_Moves_Focus_To_Next_Hour_Via_Interop()
    {
        var tracking = new TrackingInteropService();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(tracking);
        var cut = Render(p => p.Add(c => c.Value, new TimeSpan(9, 0, 0)));
        cut.Find("button").Click();

        HourOption(cut, 9).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        // Focus moved to hour 10's option id.
        var ten = HourOption(cut, 10).GetAttribute("id");
        Assert.Contains(ten, tracking.FocusedElementIds);
    }

    [Fact]
    public void ArrowDown_Skips_Disabled_Hours()
    {
        var tracking = new TrackingInteropService();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(tracking);
        // Max 09:00 disables hours 10+. From hour 8, ArrowDown lands on 9 (last enabled);
        // a second ArrowDown finds nothing enabled below and stays put.
        var cut = Render(p =>
        {
            p.Add(c => c.Max, new TimeSpan(9, 0, 0));
            p.Add(c => c.Value, new TimeSpan(8, 0, 0));
        });
        cut.Find("button").Click();

        HourOption(cut, 9).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        // No enabled hour after 9 → no focus call to a disabled hour 10.
        var ten = HourOption(cut, 10).GetAttribute("id");
        Assert.DoesNotContain(ten, tracking.FocusedElementIds);
    }

    [Fact]
    public void Enter_On_Hour_Option_Selects_It()
    {
        TimeSpan? captured = null;
        var cb = EventCallback.Factory.Create<TimeSpan?>(this, (TimeSpan? t) => captured = t);
        var cut = Render(p => p.Add(c => c.ValueChanged, cb));
        cut.Find("button").Click();

        HourOption(cut, 14).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.NotNull(captured);
        Assert.Equal(14, captured!.Value.Hours);
    }

    [Fact]
    public void Options_Expose_Listbox_And_Option_Roles()
    {
        var cut = Render();
        cut.Find("button").Click();

        var listboxes = cut.FindAll("[role='listbox']");
        Assert.True(listboxes.Count >= 2); // hours + minutes
        Assert.NotEmpty(cut.FindAll("button[role='option']"));
        // The selected option carries aria-selected=true after a pick.
        HourOption(cut, 7).Click();
        Assert.Equal("true", HourOption(cut, 7).GetAttribute("aria-selected"));
    }
}
