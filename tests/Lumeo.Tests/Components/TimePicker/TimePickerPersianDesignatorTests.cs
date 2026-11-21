using System.Globalization;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.TimePicker;

/// <summary>#593: AM/PM follow a Persian culture even without ICU data (invariant designators).</summary>
public class TimePickerPersianDesignatorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public TimePickerPersianDesignatorTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static CultureInfo FaWith(string am, string pm)
    {
        var c = new CultureInfo("fa-IR");
        c.DateTimeFormat.AMDesignator = am;
        c.DateTimeFormat.PMDesignator = pm;
        return c;
    }

    private IReadOnlyList<string> ButtonTexts(CultureInfo culture, bool use24 = false)
    {
        var cut = _ctx.Render<L.TimePicker>(p =>
        {
            p.Add(c => c.Use24Hour, use24);
            p.Add(c => c.Culture, culture);
        });
        cut.Find("button[type='button']").Click();
        return cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
    }

    [Theory]
    [InlineData("AM", "PM")]
    [InlineData("", "")]
    public void Persian_Culture_Without_Data_Shows_Persian_Designators(string am, string pm)
    {
        var texts = ButtonTexts(FaWith(am, pm));
        Assert.Contains("ق.ظ", texts);
        Assert.Contains("ب.ظ", texts);
        Assert.DoesNotContain("AM", texts);
    }

    [Fact]
    public void Persian_Culture_With_Own_Designators_Keeps_Them()
    {
        var texts = ButtonTexts(FaWith("صبح", "عصر"));
        Assert.Contains("صبح", texts);
        Assert.Contains("عصر", texts);
    }

    [Fact]
    public void EnUs_Unchanged()
    {
        var texts = ButtonTexts(new CultureInfo("en-US"));
        Assert.Contains("AM", texts);
        Assert.Contains("PM", texts);
    }

    [Fact]
    public void DeDe_24h_Has_No_Designators()
    {
        var texts = ButtonTexts(new CultureInfo("de-DE"), use24: true);
        Assert.DoesNotContain("AM", texts);
        Assert.DoesNotContain("ق.ظ", texts);
        var (am, pm) = L.CalendarSystemMath.TimeDesignators(new CultureInfo("de-DE"));
        Assert.Equal("AM", am);
        Assert.Equal("PM", pm);
    }

    [Fact]
    public void Wheel_Variant_Uses_Persian_Designators()
    {
        var cut = _ctx.Render<L.TimeWheelPicker>(p =>
        {
            p.Add(c => c.Use24Hour, false);
            p.Add(c => c.Culture, FaWith("AM", "PM"));
        });
        Assert.Contains("ق.ظ", cut.Markup);
        Assert.Contains("ب.ظ", cut.Markup);
    }

    private IRenderedComponent<L.DateTimePicker> RenderDtp() => _ctx.Render<L.DateTimePicker>(p =>
    {
        p.Add(c => c.Use24Hour, false);
        p.Add(c => c.Culture, FaWith("AM", "PM"));
        p.Add(c => c.Value, new DateTime(2024, 10, 3, 14, 30, 0));
    });

    [Fact]
    public void DateTimePicker_Popover_Uses_Persian_Designators()
    {
        var cut = RenderDtp();
        cut.Find("button[type=button]").Click();
        var texts = cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains("ق.ظ", texts);
        Assert.Contains("ب.ظ", texts);
    }

    [Fact]
    public void DateTimePicker_Trigger_Uses_Persian_Designator()
    {
        var trigger = RenderDtp().Find("button[type=button]").TextContent;
        Assert.Contains("ب.ظ", trigger);
        Assert.DoesNotContain("PM", trigger);
    }
}
