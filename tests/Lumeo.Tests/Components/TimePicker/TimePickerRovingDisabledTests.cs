using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TimePicker;

/// <summary>
/// Codex P2 — roving tabindex must never seed a DISABLED option.
///
/// RovingValue returned the selected value before checking whether it was disabled.
/// If Min/Max moved so the current selection became disabled, that disabled button
/// got tabindex="0" while every enabled option stayed -1 — and a disabled button can
/// never hold focus, so the column lost its keyboard tab stop. RovingValue now
/// requires the selected option to also be enabled, otherwise it falls back to the
/// first enabled option.
/// </summary>
public class TimePickerRovingDisabledTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TimePickerRovingDisabledTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // The hours column is the first listbox.
    private static AngleSharp.Dom.IElement HoursColumn(IRenderedComponent<L.TimePicker> cut)
        => cut.FindAll("[role='listbox']")[0];

    private static AngleSharp.Dom.IElement HourOption(IRenderedComponent<L.TimePicker> cut, int hour)
        => HoursColumn(cut).QuerySelectorAll("button[role='option']")
            .First(b => b.TextContent.Trim() == hour.ToString("D2"));

    [Fact]
    public void Disabled_Selected_Hour_Does_Not_Hold_The_Roving_Tab_Stop()
    {
        // 05:30 selected, then Min=08:00 disables hour 5 (the current selection).
        var cut = _ctx.Render<L.TimePicker>(p => p
            .Add(c => c.Use24Hour, true)
            .Add(c => c.Value, new TimeSpan(5, 30, 0))
            .Add(c => c.Min, new TimeSpan(8, 0, 0)));
        cut.Find("button").Click(); // open the popover

        var selectedDisabled = HourOption(cut, 5);
        Assert.True(selectedDisabled.HasAttribute("disabled"));
        // The disabled selection must NOT be the column's tab stop...
        Assert.Equal("-1", selectedDisabled.GetAttribute("tabindex"));
        // ...the first ENABLED hour (8) takes it instead, so the column stays reachable.
        Assert.Equal("0", HourOption(cut, 8).GetAttribute("tabindex"));

        // Invariant the fix guarantees for every column: a disabled option can never be
        // the roving tab stop (it could not receive focus anyway).
        foreach (var opt in cut.FindAll("button[role='option']"))
            if (opt.HasAttribute("disabled"))
                Assert.NotEqual("0", opt.GetAttribute("tabindex"));
    }
}
