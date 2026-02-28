using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

public class DatePickerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DatePickerTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderDatePicker(
        DateOnly? value = null,
        string? placeholder = null,
        string? format = null,
        EventCallback<DateOnly?>? valueChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DatePicker>(0);
            if (value.HasValue)
                builder.AddAttribute(1, "Value", value.Value);
            if (placeholder != null)
                builder.AddAttribute(2, "Placeholder", placeholder);
            if (format != null)
                builder.AddAttribute(3, "Format", format);
            if (valueChanged.HasValue)
                builder.AddAttribute(4, "ValueChanged", valueChanged.Value);
            builder.CloseComponent();
        });
    }

    // --- Rendering ---

    [Fact]
    public void DatePicker_Renders_Trigger_Button()
    {
        var cut = RenderDatePicker();
        var button = cut.Find("button[type='button']");
        Assert.NotNull(button);
    }

    [Fact]
    public void DatePicker_Shows_Placeholder_When_No_Value()
    {
        var cut = RenderDatePicker();
        Assert.Contains("Pick a date", cut.Markup);
    }

    [Fact]
    public void DatePicker_Shows_Custom_Placeholder()
    {
        var cut = RenderDatePicker(placeholder: "Select your date");
        Assert.Contains("Select your date", cut.Markup);
    }

    [Fact]
    public void DatePicker_Shows_Formatted_Date_When_Value_Set()
    {
        var date = new DateOnly(2024, 3, 15);
        var cut = RenderDatePicker(value: date);

        Assert.Contains("15.03.2024", cut.Markup);
    }

    [Fact]
    public void DatePicker_Shows_Custom_Format()
    {
        var date = new DateOnly(2024, 3, 15);
        var cut = RenderDatePicker(value: date, format: "yyyy/MM/dd");

        Assert.Contains("2024/03/15", cut.Markup);
    }

    [Fact]
    public void DatePicker_Has_Calendar_Icon()
    {
        var cut = RenderDatePicker();
        var svgs = cut.FindAll("svg");
        Assert.NotEmpty(svgs);
    }

    // --- Calendar Integration ---

    [Fact]
    public void Calendar_Not_Visible_Initially()
    {
        var cut = RenderDatePicker();
        // Calendar grid (with day abbreviations) should not be visible when closed
        Assert.DoesNotContain("Mo", cut.Markup);
    }

    [Fact]
    public void Clicking_Trigger_Opens_Calendar()
    {
        var cut = RenderDatePicker();

        cut.Find("button[type='button']").Click();
        // Calendar with day abbreviations should appear
        Assert.Contains("Mo", cut.Markup);
        Assert.Contains("Tu", cut.Markup);
    }

    [Fact]
    public void Clicking_Trigger_Again_Closes_Calendar()
    {
        var cut = RenderDatePicker();

        // Open
        cut.Find("button[type='button']").Click();
        Assert.Contains("Mo", cut.Markup);

        // Close (Popover toggle)
        cut.Find("button[type='button']").Click();
        Assert.DoesNotContain("Mo", cut.Markup);
    }

    // --- Date Selection ---

    [Fact]
    public void Selecting_A_Day_Fires_ValueChanged()
    {
        DateOnly? selectedDate = null;
        var callback = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => selectedDate = d);
        var cut = RenderDatePicker(valueChanged: callback);

        // Open calendar
        cut.Find("button[type='button']").Click();

        // Click a day button (skip the trigger button, nav buttons, then get a day)
        var allButtons = cut.FindAll("button[type='button']");
        // First is the trigger, then in the popover: prev-month, then days
        var dayButtons = allButtons.Skip(1).ToList(); // skip trigger
        var navSkipped = dayButtons.Skip(2).ToList(); // skip prev/next nav in calendar

        if (navSkipped.Any())
        {
            try { navSkipped[10].Click(); } catch (ArgumentException) { }
        }

        Assert.NotNull(selectedDate);
    }

    // --- Custom CSS ---

    [Fact]
    public void Custom_Class_Forwarded_On_DatePicker_Trigger()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.DatePicker>(0);
            builder.AddAttribute(1, "Class", "my-picker-class");
            builder.CloseComponent();
        });

        var button = cut.Find("button[type='button']");
        Assert.Contains("my-picker-class", button.GetAttribute("class"));
    }

    [Fact]
    public void DatePicker_Trigger_Has_Default_Classes()
    {
        var cut = RenderDatePicker();
        var button = cut.Find("button[type='button']");
        var cls = button.GetAttribute("class") ?? "";
        Assert.Contains("border-input", cls);
    }
}
