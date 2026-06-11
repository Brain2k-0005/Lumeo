using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// Regression tests: range presets were broken end-to-end — HandlePresetSelected
/// only ever set Value (never RangeStart/RangeEnd), and DateRangePicker dropped
/// the preset's End when mapping to DatePickerPreset, so a "Last 7 days"-style
/// preset just closed the popover without selecting anything.
/// </summary>
public class DatePickerRangePresetTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DatePickerRangePresetTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Range_Preset_Sets_Both_RangeStart_And_RangeEnd()
    {
        DateOnly? start = null;
        DateOnly? end = null;
        var presetStart = new DateOnly(2026, 6, 5);
        var presetEnd = new DateOnly(2026, 6, 11);

        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.Mode, L.DatePicker.DatePickerMode.Range)
            .Add(c => c.Inline, true)
            .Add(c => c.Presets, new List<L.DatePicker.DatePickerPreset>
            {
                new("Last 7 days", presetStart, presetEnd),
            })
            .Add(c => c.RangeStartChanged, EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => start = d))
            .Add(c => c.RangeEndChanged, EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => end = d)));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Last 7 days").Click();

        Assert.Equal(presetStart, start);
        Assert.Equal(presetEnd, end);
    }

    [Fact]
    public void Range_Preset_Without_End_Selects_Single_Day_Range()
    {
        // Back-compat: the historic two-argument DatePickerPreset construction
        // must keep compiling and now selects a single-day range in Range mode.
        DateOnly? start = null;
        DateOnly? end = null;
        var date = new DateOnly(2026, 6, 11);

        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.Mode, L.DatePicker.DatePickerMode.Range)
            .Add(c => c.Inline, true)
            .Add(c => c.Presets, new List<L.DatePicker.DatePickerPreset> { new("Today", date) })
            .Add(c => c.RangeStartChanged, EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => start = d))
            .Add(c => c.RangeEndChanged, EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => end = d)));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Today").Click();

        Assert.Equal(date, start);
        Assert.Equal(date, end);
    }

    [Fact]
    public void Range_Preset_With_Inverted_Bounds_Is_Normalised()
    {
        DateOnly? start = null;
        DateOnly? end = null;

        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.Mode, L.DatePicker.DatePickerMode.Range)
            .Add(c => c.Inline, true)
            .Add(c => c.Presets, new List<L.DatePicker.DatePickerPreset>
            {
                new("Backwards", new DateOnly(2026, 6, 11), new DateOnly(2026, 6, 5)),
            })
            .Add(c => c.RangeStartChanged, EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => start = d))
            .Add(c => c.RangeEndChanged, EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => end = d)));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Backwards").Click();

        Assert.Equal(new DateOnly(2026, 6, 5), start);
        Assert.Equal(new DateOnly(2026, 6, 11), end);
    }

    [Fact]
    public void Single_Mode_Preset_Still_Sets_Value()
    {
        DateOnly? committed = null;
        var date = new DateOnly(2026, 3, 1);

        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.Inline, true)
            .Add(c => c.Presets, new List<L.DatePicker.DatePickerPreset> { new("Start of March", date) })
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => committed = d)));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Start of March").Click();

        Assert.Equal(date, committed);
    }

    [Fact]
    public void DateRangePicker_Preset_Forwards_End_Date_And_Fires_Both_Callbacks()
    {
        DateOnly? start = null;
        DateOnly? end = null;
        var presetStart = new DateOnly(2026, 6, 5);
        var presetEnd = new DateOnly(2026, 6, 11);

        var cut = _ctx.Render<L.DateRangePicker>(p => p
            .Add(c => c.Presets, new List<L.DateRangePicker.DateRangePreset>
            {
                new() { Label = "Last 7 days", Start = presetStart, End = presetEnd },
            })
            .Add(c => c.StartDateChanged, EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => start = d))
            .Add(c => c.EndDateChanged, EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => end = d)));

        // Open the popover via the trigger, then click the preset.
        cut.Find("button[type='button']").Click();
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Last 7 days").Click();

        Assert.Equal(presetStart, start);
        Assert.Equal(presetEnd, end);
        // Preset selection closes the popover.
        Assert.DoesNotContain("Last 7 days", cut.Markup);
    }
}
