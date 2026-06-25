using System.Globalization;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TimePicker;

/// <summary>
/// Battle-test keyboard/a11y regression for TimePicker (List variant):
///
/// n=168 — the 12-hour AM/PM toggle buttons exposed their active half-of-day
/// only via colour (the <see cref="L.TimePicker"/> hour/minute/second columns use
/// role="option" + aria-selected, but the AM/PM buttons carried no selected-state
/// ARIA at all). The toggle buttons must now carry aria-pressed so assistive tech
/// can read which half is active. Mirrors the equivalent DateTimePicker fix
/// (DateTimePickerRovingSeedTests n=149).
/// </summary>
public class TimePickerAmPmAriaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TimePickerAmPmAriaTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.TimePicker> RenderOpen(Action<ComponentParameterCollectionBuilder<L.TimePicker>>? extra = null)
    {
        var cut = _ctx.Render<L.TimePicker>(p =>
        {
            // Force 12-hour mode so the AM/PM column renders regardless of the
            // ambient culture's short-time pattern.
            p.Add(c => c.Use24Hour, false);
            extra?.Invoke(p);
        });
        cut.Find("button[type='button']").Click(); // open the popover
        return cut;
    }

    // The component derives the AM/PM captions from the effective culture; mirror
    // that so the test is culture-independent (it just defaults to CurrentCulture).
    private static string AmLabel
        => string.IsNullOrEmpty(CultureInfo.CurrentCulture.DateTimeFormat.AMDesignator)
            ? "AM" : CultureInfo.CurrentCulture.DateTimeFormat.AMDesignator;
    private static string PmLabel
        => string.IsNullOrEmpty(CultureInfo.CurrentCulture.DateTimeFormat.PMDesignator)
            ? "PM" : CultureInfo.CurrentCulture.DateTimeFormat.PMDesignator;

    [Fact]
    public void AmPm_Toggle_Exposes_Aria_Pressed()
    {
        // With no time selected the default is AM, so AM is pressed and PM is not.
        var cut = RenderOpen(); // Value stays null

        var am = cut.FindAll("button").First(b => b.TextContent.Trim() == AmLabel);
        var pm = cut.FindAll("button").First(b => b.TextContent.Trim() == PmLabel);

        // Without the fix aria-pressed is absent (null) on both buttons.
        Assert.Equal("true", am.GetAttribute("aria-pressed"));
        Assert.Equal("false", pm.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void AmPm_Aria_Pressed_Tracks_The_Active_Half()
    {
        // A PM value -> PM pressed, AM not.
        var cut = RenderOpen(p => p.Add(c => c.Value, new TimeSpan(15, 0, 0))); // 3 PM

        var am = cut.FindAll("button").First(b => b.TextContent.Trim() == AmLabel);
        var pm = cut.FindAll("button").First(b => b.TextContent.Trim() == PmLabel);

        Assert.Equal("false", am.GetAttribute("aria-pressed"));
        Assert.Equal("true", pm.GetAttribute("aria-pressed"));
    }
}
