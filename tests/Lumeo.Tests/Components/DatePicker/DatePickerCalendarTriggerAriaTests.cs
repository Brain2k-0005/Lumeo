using System.Globalization;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// PR #356 round-7 (Codex P2) — the Calendar-variant triggers (typeable input AND plain
/// button, both under <c>SuppressActivationKeys</c> since round-2/round-6) drop
/// PopoverTrigger's wrapper out of the Tab order because the input/button is the real
/// Tab stop. Before this fix, <c>aria-haspopup</c>/<c>aria-expanded</c>/<c>aria-controls</c>
/// stayed only on that now-unfocusable wrapper. DatePicker now consumes PopoverTrigger's
/// <c>ChildContentSlot</c> so the actual focusable element carries the popup state too.
/// </summary>
public class DatePickerCalendarTriggerAriaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DatePickerCalendarTriggerAriaTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Typeable_Input_Trigger_Carries_The_Popup_Aria_State()
    {
        var cut = _ctx.Render<L.DatePicker>(p =>
        {
            p.Add(c => c.Format, "yyyy-MM-dd");
            p.Add(c => c.Culture, CultureInfo.InvariantCulture);
        });

        var input = cut.Find("input");
        Assert.Equal("dialog", input.GetAttribute("aria-haspopup"));
        Assert.Equal("false", input.GetAttribute("aria-expanded"));

        input.Click();
        Assert.Equal("true", cut.Find("input").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Button_Trigger_Carries_The_Popup_Aria_State()
    {
        // Range mode always renders the plain-button trigger (UsesTypeableInput
        // excludes Range).
        var cut = _ctx.Render<L.DatePicker>(p => p.Add(c => c.Mode, L.DatePicker.DatePickerMode.Range));

        var button = cut.Find("button[type='button']");
        Assert.Equal("dialog", button.GetAttribute("aria-haspopup"));
        Assert.Equal("false", button.GetAttribute("aria-expanded"));

        button.Click();
        Assert.Equal("true", cut.Find("button[type='button']").GetAttribute("aria-expanded"));
    }
}
