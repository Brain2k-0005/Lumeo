using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// Battle-test regression (wave1 #5, high): when <c>ShowTimePicker</c> is enabled the
/// DatePicker must fold the current <c>Time</c> into the <c>DateTimeValue</c> commit.
/// Before the fix, <c>RaiseDateTimeValueChanged</c> always composed at
/// <see cref="TimeOnly.MinValue"/>, so:
///   1. picking a time never reached DateTimeValue (HandleTimeChanged never raised it), and
///   2. picking a date silently reset the bound DateTime back to midnight (lost the time).
///
/// Uses Inline mode so the Calendar + TimePicker render directly (no popover interop),
/// and drives the bug through the child components' ValueChanged callbacks — exactly the
/// callbacks the real markup wires to HandleDateSelected / HandleTimeChanged.
/// </summary>
public class DatePickerTimeValueCompositionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DatePickerTimeValueCompositionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task TimeOnly_Change_Propagates_To_DateTimeValue()
    {
        DateTime? emitted = null;
        var emittedCount = 0;
        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.Inline, true)
            .Add(c => c.ShowTimePicker, true)
            .Add(c => c.Value, new DateOnly(2026, 6, 15))
            .Add(c => c.DateTimeValueChanged, EventCallback.Factory.Create<DateTime?>(_ctx, (DateTime? v) =>
            {
                emitted = v;
                emittedCount++;
            })));

        // Pick a time only (no date change). The TimePicker is the bound time control.
        var timePicker = cut.FindComponent<L.TimePicker>();
        await cut.InvokeAsync(() =>
            timePicker.Instance.ValueChanged.InvokeAsync(new TimeSpan(14, 30, 0)));

        // Before the fix HandleTimeChanged never raised DateTimeValueChanged at all,
        // so emitted stayed null. After the fix it raises with the date + folded time.
        Assert.True(emittedCount > 0, "a time-only change must raise DateTimeValueChanged");
        Assert.NotNull(emitted);
        Assert.Equal(new TimeSpan(14, 30, 0), emitted!.Value.TimeOfDay);
        Assert.Equal(new DateTime(2026, 6, 15, 14, 30, 0), emitted.Value);
    }

    [Fact]
    public async Task Picking_A_Date_Keeps_The_Already_Selected_Time()
    {
        DateTime? emitted = null;
        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.Inline, true)
            .Add(c => c.ShowTimePicker, true)
            // A time is already selected before the date pick.
            .Add(c => c.Time, new TimeSpan(9, 15, 0))
            .Add(c => c.DateTimeValueChanged, EventCallback.Factory.Create<DateTime?>(_ctx, (DateTime? v) => emitted = v)));

        // Pick a date via the Calendar (HandleDateSelected -> RaiseDateTimeValueChanged).
        var calendar = cut.FindComponent<L.Calendar>();
        await cut.InvokeAsync(() =>
            calendar.Instance.ValueChanged.InvokeAsync(new DateOnly(2026, 6, 15)));

        // Before the fix the commit composed at midnight, discarding the 09:15 time.
        Assert.NotNull(emitted);
        Assert.Equal(new TimeSpan(9, 15, 0), emitted!.Value.TimeOfDay);
        Assert.Equal(new DateTime(2026, 6, 15, 9, 15, 0), emitted.Value);
    }

    [Fact]
    public async Task Binding_A_NonMidnight_DateTimeValue_Hydrates_Time_So_A_Later_Date_Pick_Keeps_It()
    {
        // Codex P2 — OnParametersSet's DateTimeValue -> Value sync only ever copied the DATE
        // portion, never hydrating Time from the incoming DateTimeValue. So a DatePicker bound
        // straight to a non-midnight DateTimeValue (no prior interaction ever set Time itself)
        // saw Time as null, and RaiseDateTimeValueChanged then folded in midnight the next time
        // a date was picked — silently losing the time that DateTimeValue originally carried.
        DateTime? emitted = null;
        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.Inline, true)
            .Add(c => c.ShowTimePicker, true)
            .Add(c => c.DateTimeValue, new DateTime(2026, 6, 10, 14, 30, 0))
            .Add(c => c.DateTimeValueChanged, EventCallback.Factory.Create<DateTime?>(_ctx, (DateTime? v) => emitted = v)));

        // Pick a DIFFERENT date via the Calendar — no time interaction at all.
        var calendar = cut.FindComponent<L.Calendar>();
        await cut.InvokeAsync(() =>
            calendar.Instance.ValueChanged.InvokeAsync(new DateOnly(2026, 6, 15)));

        // The 14:30 time originally carried by DateTimeValue must survive onto the new date.
        Assert.NotNull(emitted);
        Assert.Equal(new TimeSpan(14, 30, 0), emitted!.Value.TimeOfDay);
        Assert.Equal(new DateTime(2026, 6, 15, 14, 30, 0), emitted.Value);
    }

    [Fact]
    public async Task Without_Time_DateTimeValue_Still_Defaults_To_Midnight()
    {
        // Regression guard: the normal (no-time) path must be unchanged — still midnight.
        DateTime? emitted = null;
        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.Inline, true)
            .Add(c => c.DateTimeValueChanged, EventCallback.Factory.Create<DateTime?>(_ctx, (DateTime? v) => emitted = v)));

        var calendar = cut.FindComponent<L.Calendar>();
        await cut.InvokeAsync(() =>
            calendar.Instance.ValueChanged.InvokeAsync(new DateOnly(2026, 6, 15)));

        Assert.NotNull(emitted);
        Assert.Equal(TimeSpan.Zero, emitted!.Value.TimeOfDay);
        Assert.Equal(new DateTime(2026, 6, 15, 0, 0, 0), emitted.Value);
    }
}
