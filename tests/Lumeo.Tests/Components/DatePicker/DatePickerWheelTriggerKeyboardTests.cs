using System.Globalization;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// PR #356 round-7 (Codex P2) — <c>Variant="Wheel"</c> with <c>Inline="false"</c> renders
/// its own <c>PopoverTrigger</c>/native &lt;button&gt; pair, separate from the Calendar-
/// variant branch round-6 fixed. It was missed by that fix: without
/// <c>SuppressActivationKeys</c>, an Enter/Space keydown on the native button bubbles to
/// PopoverTrigger's wrapper and toggles via its own keydown handler, and THEN the
/// browser's click synthesis for that same key ALSO bubbles up and toggles again via
/// <c>@onclick="Toggle"</c> — two toggles cancel out and the wheel picker never opens from
/// the keyboard. Mirrors <see cref="DatePickerKeyboardTests"/>'s coverage of the
/// Calendar-variant button trigger for the same class of bug.
/// </summary>
public class DatePickerWheelTriggerKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DatePickerWheelTriggerKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DatePicker> RenderWheelPicker(DateOnly? value = null)
        => _ctx.Render<L.DatePicker>(p =>
        {
            p.Add(c => c.Variant, L.DatePicker.DatePickerVariant.Wheel);
            p.Add(c => c.Culture, CultureInfo.InvariantCulture);
            if (value.HasValue) p.Add(c => c.Value, value.Value);
        });

    private static bool IsOpen(IRenderedComponent<L.DatePicker> cut)
        => cut.FindAll("div[role='dialog']").Count > 0;

    [Fact]
    public void Wheel_Trigger_Wrapper_Suppresses_Its_Own_Space_Registration()
    {
        // Mirrors DatePickerKeyboardTests' coverage of the Calendar branches: with
        // SuppressActivationKeys reaching the wrapper, there is nothing left for the
        // Space-default-suppression JS rule to protect, so it must not register.
        var cut = RenderWheelPicker();
        cut.Find("button[type='button']").Click();

        Assert.DoesNotContain(_ctx.JSInterop.Invocations, i =>
            i.Identifier == "registerPreventDefaultKeys"
            && i.Arguments[0] is string id && id.StartsWith("popover-trigger-", StringComparison.Ordinal));
    }

    [Fact]
    public void Wheel_Trigger_Wrapper_Ignores_Enter_Because_The_Native_Button_Owns_Activation()
    {
        // bUnit doesn't simulate real DOM bubbling (see DatePickerKeyboardTests' file-level
        // comment on why bubbling assertions are pinned at the mechanism instead): with
        // SuppressActivationKeys set, the wrapper must stay closed when Enter reaches IT
        // directly, leaving the native button's own click (fired once by the real browser)
        // as the sole toggle.
        var cut = RenderWheelPicker();

        cut.Find("[role='button']").KeyDown("Enter");

        Assert.False(IsOpen(cut));
    }

    [Fact]
    public void Wheel_Trigger_Wrapper_Is_Removed_From_The_Tab_Order()
    {
        var cut = RenderWheelPicker();
        Assert.Equal("-1", cut.Find("div[role='button']").GetAttribute("tabindex"));
    }

    [Fact]
    public void Wheel_Trigger_Button_Carries_The_Popup_Aria_State()
    {
        // Round-7 (Codex P2): SuppressActivationKeys removes the wrapper from the Tab
        // order, so the real Tab stop is the native button — it must carry
        // aria-haspopup/aria-expanded itself, not only the now-unfocusable wrapper.
        var cut = RenderWheelPicker();
        var button = cut.Find("button[type='button']");

        Assert.Equal("dialog", button.GetAttribute("aria-haspopup"));
        Assert.Equal("false", button.GetAttribute("aria-expanded"));

        button.Click();
        Assert.Equal("true", cut.Find("button[type='button']").GetAttribute("aria-expanded"));
    }
}
