using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Calendar;

public class CalendarTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CalendarTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderCalendar(
        DateOnly? value = null,
        EventCallback<DateOnly?>? valueChanged = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Calendar>(0);
            if (value.HasValue)
                builder.AddAttribute(1, "Value", value.Value);
            if (valueChanged.HasValue)
                builder.AddAttribute(2, "ValueChanged", valueChanged.Value);
            builder.CloseComponent();
        });
    }

    // --- Rendering ---

    [Fact]
    public void Calendar_Renders_Container()
    {
        var cut = RenderCalendar();
        var div = cut.Find("div");
        Assert.NotNull(div);
    }

    [Fact]
    public void Calendar_Shows_Current_Month_And_Year()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var cut = RenderCalendar();

        // Month and year are in separate buttons
        Assert.Contains(today.ToString("MMMM"), cut.Markup);
        Assert.Contains(today.Year.ToString(), cut.Markup);
    }

    [Fact]
    public void Calendar_Shows_Day_Abbreviations()
    {
        var cut = RenderCalendar();

        Assert.Contains("Mo", cut.Markup);
        Assert.Contains("Tu", cut.Markup);
        Assert.Contains("We", cut.Markup);
        Assert.Contains("Th", cut.Markup);
        Assert.Contains("Fr", cut.Markup);
        Assert.Contains("Sa", cut.Markup);
        Assert.Contains("Su", cut.Markup);
    }

    [Fact]
    public void Calendar_Renders_42_Day_Buttons()
    {
        var cut = RenderCalendar();
        // There are 2 nav buttons + 2 header buttons (month, year) + 42 day buttons
        var buttons = cut.FindAll("button[type='button']");
        Assert.Equal(46, buttons.Count); // 2 nav + 2 header + 42 days
    }

    [Fact]
    public void Calendar_Shows_Given_Month_When_Value_Set()
    {
        var date = new DateOnly(2024, 6, 15);
        var cut = RenderCalendar(value: date);

        Assert.Contains("June", cut.Markup);
        Assert.Contains("2024", cut.Markup);
    }

    // --- Navigation ---

    [Fact]
    public void Clicking_Previous_Month_Button_Changes_Month()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var cut = RenderCalendar();

        var prevButton = cut.FindAll("button[type='button']").First();
        prevButton.Click();

        var prevMonth = today.AddMonths(-1);
        Assert.Contains(prevMonth.ToString("MMMM"), cut.Markup);
        Assert.Contains(prevMonth.Year.ToString(), cut.Markup);
    }

    [Fact]
    public void Clicking_Next_Month_Button_Changes_Month()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var cut = RenderCalendar();

        // Nav buttons: first is prev (chevron-left), then month button, year button, then next (chevron-right)
        // The next button is the 4th button (index 3): prev, month-name, year, next
        var allButtons = cut.FindAll("button[type='button']");
        allButtons[3].Click();

        var nextMonth = today.AddMonths(1);
        Assert.Contains(nextMonth.ToString("MMMM"), cut.Markup);
        Assert.Contains(nextMonth.Year.ToString(), cut.Markup);
    }

    [Fact]
    public void Clicking_Previous_Then_Next_Returns_To_Current_Month()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var cut = RenderCalendar();

        // Click prev (index 0)
        cut.FindAll("button[type='button']")[0].Click();
        // Click next (index 3: prev, month-name, year, next)
        cut.FindAll("button[type='button']")[3].Click();

        Assert.Contains(today.ToString("MMMM"), cut.Markup);
        Assert.Contains(today.Year.ToString(), cut.Markup);
    }

    // --- Selection ---

    [Fact]
    public void Clicking_Day_Fires_ValueChanged()
    {
        DateOnly? selectedDate = null;
        var callback = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => selectedDate = d);
        var cut = RenderCalendar(valueChanged: callback);

        // Day buttons start after 4 non-day buttons: prev, month-name, year, next
        var dayButtons = cut.FindAll("button[type='button']").Skip(4).ToList();
        dayButtons[0].Click();

        Assert.NotNull(selectedDate);
    }

    [Fact]
    public void Selected_Day_Has_Primary_Background_Class()
    {
        var date = new DateOnly(2024, 3, 15);
        var cut = RenderCalendar(value: date);

        var buttons = cut.FindAll("button[type='button']");
        var selectedBtn = buttons.FirstOrDefault(b =>
            b.TextContent.Trim() == "15" &&
            (b.GetAttribute("class") ?? "").Contains("bg-primary"));

        Assert.NotNull(selectedBtn);
    }

    // --- Custom CSS ---

    [Fact]
    public void Custom_Class_Forwarded_On_Calendar()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Calendar>(0);
            builder.AddAttribute(1, "Class", "my-calendar-class");
            builder.CloseComponent();
        });

        var div = cut.Find("div");
        Assert.Contains("my-calendar-class", div.GetAttribute("class"));
    }

    [Fact]
    public void Calendar_Has_Default_Padding_Class()
    {
        var cut = RenderCalendar();
        var div = cut.Find("div");
        Assert.Contains("p-3", div.GetAttribute("class"));
    }

    // --- AdditionalAttributes ---

    [Fact]
    public void AdditionalAttributes_Forwarded_On_Calendar()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Calendar>(0);
            builder.AddAttribute(1, "data-testid", "my-calendar");
            builder.CloseComponent();
        });

        var div = cut.Find("div");
        Assert.Equal("my-calendar", div.GetAttribute("data-testid"));
    }
}
