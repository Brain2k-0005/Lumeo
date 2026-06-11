using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// Regression tests: typed keyboard input must enforce MinDate / MaxDate /
/// IsDateDisabled exactly like the calendar grid does. Previously
/// CommitBufferAsync committed ANY parseable date, so typing bypassed the
/// constraints the day cells enforce.
/// </summary>
public class DatePickerTypedInputValidationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DatePickerTypedInputValidationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Pin format + culture so parsing in these tests is locale-independent.
    private const string Format = "yyyy-MM-dd";

    private IRenderedComponent<L.DatePicker> RenderPicker(
        DateOnly? value = null,
        DateOnly? minDate = null,
        DateOnly? maxDate = null,
        Func<DateTime, bool>? isDateDisabled = null,
        EventCallback<DateOnly?>? valueChanged = null,
        EventCallback<string>? onParseError = null)
    {
        return _ctx.Render<L.DatePicker>(p =>
        {
            p.Add(c => c.Format, Format);
            p.Add(c => c.Culture, CultureInfo.InvariantCulture);
            if (value.HasValue) p.Add(c => c.Value, value.Value);
            if (minDate.HasValue) p.Add(c => c.MinDate, minDate.Value);
            if (maxDate.HasValue) p.Add(c => c.MaxDate, maxDate.Value);
            if (isDateDisabled is not null) p.Add(c => c.IsDateDisabled, isDateDisabled);
            if (valueChanged.HasValue) p.Add(c => c.ValueChanged, valueChanged.Value);
            if (onParseError.HasValue) p.Add(c => c.OnParseError, onParseError.Value);
        });
    }

    private static void TypeAndBlur(IRenderedComponent<L.DatePicker> cut, string text)
    {
        var input = cut.Find("input");
        input.Input(text);
        input.Blur();
    }

    [Fact]
    public void Typing_Date_After_MaxDate_Is_Rejected_And_ValueChanged_Not_Fired()
    {
        var valueChangedCount = 0;
        var callback = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? _) => valueChangedCount++);
        var cut = RenderPicker(
            value: new DateOnly(2026, 6, 10),
            maxDate: new DateOnly(2026, 6, 15),
            valueChanged: callback);

        TypeAndBlur(cut, "2026-06-20");

        Assert.Equal(0, valueChangedCount);
        // Buffer must revert to the last valid value.
        Assert.Equal("2026-06-10", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Typing_Date_Before_MinDate_Is_Rejected_And_ValueChanged_Not_Fired()
    {
        var valueChangedCount = 0;
        var callback = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? _) => valueChangedCount++);
        var cut = RenderPicker(
            value: new DateOnly(2026, 6, 10),
            minDate: new DateOnly(2026, 6, 1),
            valueChanged: callback);

        TypeAndBlur(cut, "2026-05-20");

        Assert.Equal(0, valueChangedCount);
        Assert.Equal("2026-06-10", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Typing_Date_Disabled_By_Predicate_Is_Rejected()
    {
        DateOnly? committed = null;
        var valueChangedCount = 0;
        var callback = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => { committed = d; valueChangedCount++; });
        // Disable all Sundays — 2026-06-21 is a Sunday.
        var cut = RenderPicker(
            isDateDisabled: d => d.DayOfWeek == DayOfWeek.Sunday,
            valueChanged: callback);

        TypeAndBlur(cut, "2026-06-21");

        Assert.Equal(0, valueChangedCount);
        Assert.Null(committed);
        // No previous value — buffer reverts to empty.
        Assert.Equal(string.Empty, cut.Find("input").GetAttribute("value") ?? string.Empty);
    }

    [Fact]
    public void Rejected_Out_Of_Range_Input_Fires_OnParseError_With_Raw_Buffer()
    {
        string? reportedBuffer = null;
        var parseError = EventCallback.Factory.Create<string>(_ctx, (string s) => reportedBuffer = s);
        var cut = RenderPicker(
            maxDate: new DateOnly(2026, 6, 15),
            onParseError: parseError);

        TypeAndBlur(cut, "2026-06-20");

        Assert.Equal("2026-06-20", reportedBuffer);
    }

    [Fact]
    public void Typing_Date_Within_Range_Still_Commits()
    {
        DateOnly? committed = null;
        var callback = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => committed = d);
        var cut = RenderPicker(
            minDate: new DateOnly(2026, 6, 1),
            maxDate: new DateOnly(2026, 6, 30),
            valueChanged: callback);

        TypeAndBlur(cut, "2026-06-12");

        Assert.Equal(new DateOnly(2026, 6, 12), committed);
        Assert.Equal("2026-06-12", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Typing_Date_Exactly_On_MaxDate_Boundary_Commits()
    {
        // The grid treats day == MaxDate as selectable (only day > MaxDate is
        // disabled) — the typed path must use the same boundary semantics.
        DateOnly? committed = null;
        var callback = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => committed = d);
        var cut = RenderPicker(
            maxDate: new DateOnly(2026, 6, 15),
            valueChanged: callback);

        TypeAndBlur(cut, "2026-06-15");

        Assert.Equal(new DateOnly(2026, 6, 15), committed);
    }
}
