using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DateTimePicker;

/// <summary>
/// Regression (triage #30, state-on-data-change): a TimeZone change must
/// re-project the bound OffsetValue. The wall-clock display lives in Value
/// (derived from OffsetValue at OnParametersSet), while the zone label reads
/// TimeZone live. OnParametersSet used to re-derive Value ONLY when OffsetValue
/// changed, so swapping just the zone left the displayed wall clock from the
/// old zone while the label showed the new one — they disagreed.
/// </summary>
public class DateTimePickerTimeZoneReprojectTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DateTimePickerTimeZoneReprojectTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Host-OS-independent fixed-offset zones so the projected wall clock is
    // deterministic regardless of the machine's installed tz database.
    private static TimeZoneInfo FixedZone(string id, int offsetHours) =>
        TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(offsetHours), id, id);

    // The hours column renders first; selected options carry bg-primary.
    private static AngleSharp.Dom.IElement HourOption(IRenderedComponent<L.DateTimePicker> cut, int hour)
        => cut.FindAll("[role='listbox']")[0]
            .QuerySelectorAll("button[role='option']")
            .First(b => b.TextContent.Trim() == hour.ToString("D2"));

    private static bool IsSelected(AngleSharp.Dom.IElement option) =>
        (option.GetAttribute("class") ?? "").Contains("bg-primary");

    [Fact]
    public void TimeZone_Change_Reprojects_Bound_OffsetValue()
    {
        // A fixed instant: 12:00 UTC.
        var instant = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        var cut = _ctx.Render<L.DateTimePicker>(p =>
        {
            p.Add(c => c.Use24Hour, true);
            p.Add(c => c.OffsetValue, (DateTimeOffset?)instant);
            p.Add(c => c.TimeZone, FixedZone("Fixed+0", 0));
        });

        // In UTC+0 the wall clock is 12:00 — hour 12 is the selected option.
        cut.Find("button[type='button']").Click();
        Assert.True(IsSelected(HourOption(cut, 12)));

        // Swap ONLY the zone (same OffsetValue). UTC+5 → wall clock 17:00.
        cut.Render(p => p.Add(c => c.TimeZone, FixedZone("Fixed+5", 5)));

        // The re-projected wall clock must drive the time column: 17 selected,
        // 12 no longer selected. Without the fix the column stays on 12.
        Assert.True(IsSelected(HourOption(cut, 17)));
        Assert.False(IsSelected(HourOption(cut, 12)));
    }
}
