using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// DatePicker CalendarSystem="Jalali" (issue #545): display text and typed input follow
/// the Persian calendar; Value stays a Gregorian DateOnly. 2024-10-03 is 1403/07/12.
/// </summary>
public class DatePickerJalaliTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DatePickerJalaliTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DatePicker> RenderPicker(
        DateOnly? value = null,
        string? format = null,
        L.DatePicker.DatePickerMode mode = L.DatePicker.DatePickerMode.Single,
        Action<DateOnly?>? onChanged = null,
        CultureInfo? culture = null)
        => _ctx.Render<L.DatePicker>(p =>
        {
            p.Add(c => c.CalendarSystem, L.CalendarSystem.Jalali);
            p.Add(c => c.Culture, culture ?? new CultureInfo("en-US"));
            p.Add(c => c.Mode, mode);
            if (format is not null) p.Add(c => c.Format, format);
            if (value.HasValue) p.Add(c => c.Value, value.Value);
            if (onChanged is not null)
                p.Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, onChanged));
        });

    private static void TypeAndBlur(IRenderedComponent<L.DatePicker> cut, string text)
    {
        var input = cut.Find("input");
        input.Input(text);
        input.Blur();
    }

    [Fact]
    public void Displays_Value_In_Jalali_Default_Format()
    {
        var cut = RenderPicker(new DateOnly(2024, 10, 3));
        Assert.Equal("1403/07/12", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Displays_Persian_Digits_Under_Fa_Culture()
    {
        var cut = RenderPicker(new DateOnly(2024, 10, 3), culture: new CultureInfo("fa-IR"));
        Assert.Equal("۱۴۰۳/۰۷/۱۲", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Custom_Format_Resolves_Against_Jalali_Calendar()
    {
        var cut = RenderPicker(new DateOnly(2024, 10, 3), format: "d MMMM yyyy");
        Assert.Equal("12 Mehr 1403", cut.Find("input").GetAttribute("value"));
    }

    [Theory]
    [InlineData("1403/07/12")]
    [InlineData("۱۴۰۳/۰۷/۱۲")]
    public void Typed_Jalali_Date_Commits_Gregorian_Value(string text)
    {
        DateOnly? committed = null;
        var cut = RenderPicker(onChanged: v => committed = v);

        TypeAndBlur(cut, text);

        Assert.Equal(new DateOnly(2024, 10, 3), committed);
        Assert.Equal("1403/07/12", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Invalid_Jalali_Date_Is_Rejected()
    {
        var calls = 0;
        var cut = RenderPicker(new DateOnly(2024, 10, 3), onChanged: _ => calls++);

        TypeAndBlur(cut, "1404/12/30"); // 1404 is not a leap year

        Assert.Equal(0, calls);
        Assert.Equal("1403/07/12", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Switching_CalendarSystem_Reformats_The_Buffer()
    {
        var calls = 0;
        var value = new DateOnly(2024, 10, 3);
        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.Culture, CultureInfo.InvariantCulture)
            .Add(c => c.Format, "yyyy/MM/dd")
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, _ => calls++)));
        Assert.Equal("2024/10/03", cut.Find("input").GetAttribute("value"));

        cut.Render(p => p.Add(c => c.CalendarSystem, L.CalendarSystem.Jalali));
        Assert.Equal("1403/07/12", cut.Find("input").GetAttribute("value"));

        // Blurring the re-rendered text must not move the date.
        cut.Find("input").Blur();
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Wheel_Variant_Stays_Gregorian()
    {
        var cut = _ctx.Render<L.DatePicker>(p => p
            .Add(c => c.CalendarSystem, L.CalendarSystem.Jalali)
            .Add(c => c.Variant, L.DatePicker.DatePickerVariant.Wheel)
            .Add(c => c.Culture, CultureInfo.InvariantCulture)
            .Add(c => c.Format, "yyyy-MM-dd")
            .Add(c => c.Value, new DateOnly(2024, 10, 3)));
        // The trigger text matches the (Gregorian-only) wheel, not a Jalali rendering.
        var texts = cut.FindAll("span").Select(e => e.TextContent.Trim()).ToList();
        Assert.Contains("2024-10-03", texts);
        Assert.DoesNotContain(texts, t => t.Contains("1403-07-12") || t.Contains("1403/07/12"));
    }

    [Fact]
    public void Month_Mode_Displays_And_Parses_Jalali_Month()
    {
        DateOnly? committed = null;
        var cut = RenderPicker(new DateOnly(2024, 10, 3), mode: L.DatePicker.DatePickerMode.Month, onChanged: v => committed = v);
        Assert.Equal("Mehr 1403", cut.Find("input").GetAttribute("value"));

        TypeAndBlur(cut, "Aban 1403");

        Assert.Equal(new DateOnly(2024, 10, 22), committed);
    }
}
