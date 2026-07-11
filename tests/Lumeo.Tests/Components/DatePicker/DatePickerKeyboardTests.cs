using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.DatePicker;

/// <summary>
/// Keyboard coverage for the typeable-input trigger's own HandleInputKeyDown
/// (independent of the popover Calendar's own arrow/Home/End grid navigation,
/// which is covered separately): Enter commits the typed buffer and closes
/// the popover; Escape reverts the buffer to the last committed Value and
/// closes WITHOUT committing.
///
/// PRODUCT FIX while writing these tests: the input's own @onkeydown had no
/// stopPropagation, unlike its sibling @onclick handlers (which already carry
/// @onclick:stopPropagation for exactly this reason, per the comment above the
/// wrapper div). The trigger this input renders inside — PopoverTrigger — has
/// its OWN @onkeydown that toggles the popover on Enter/Space. Without
/// stopPropagation, pressing Enter to commit a typed date bubbled past
/// HandleInputKeyDown's `_isOpen = false` straight into PopoverTrigger's
/// handler, which immediately toggled the (just-closed) popover back OPEN —
/// so committing a typed date reopened the calendar instead of closing it.
/// Fixed by adding @onkeydown:stopPropagation="true" to the input, mirroring
/// the existing @onclick:stopPropagation pattern.
///
/// Two real-world scopes this key handling does NOT cover, verified by
/// reading DatePicker.razor / DateWheelPicker.razor rather than assumed:
///   - Range mode (`DatePickerMode.Range`, which is what DateRangePicker
///     always sets) never renders the typeable input — `UsesTypeableInput`
///     excludes Range — so it always uses the plain button trigger and has
///     no HandleInputKeyDown surface of its own; only the Calendar's range
///     grid navigation applies there.
///   - DateWheelPicker has no `@onkeydown` anywhere; it is scroll/pointer-only
///     today (a real, undocumented a11y gap, not fixed here — out of the
///     assigned SPECIAL list for this pass).
/// </summary>
public class DatePickerKeyboardTests : IAsyncLifetime
{
    private const string Format = "yyyy-MM-dd";
    private readonly BunitContext _ctx = new();

    public DatePickerKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.DatePicker> RenderPicker(
        DateOnly? value = null,
        EventCallback<DateOnly?>? valueChanged = null)
        => _ctx.Render<L.DatePicker>(p =>
        {
            p.Add(c => c.Format, Format);
            p.Add(c => c.Culture, CultureInfo.InvariantCulture);
            if (value.HasValue) p.Add(c => c.Value, value.Value);
            if (valueChanged.HasValue) p.Add(c => c.ValueChanged, valueChanged.Value);
        });

    private static bool IsOpen(IRenderedComponent<L.DatePicker> cut)
        => cut.FindAll("div[role='dialog']").Count > 0;

    [Fact]
    public void Enter_Commits_The_Typed_Buffer_And_Closes_The_Popover()
    {
        DateOnly? committed = null;
        var callback = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => committed = d);
        var cut = RenderPicker(value: new DateOnly(2026, 6, 10), valueChanged: callback);

        var input = cut.Find("input");
        input.Click(); // open, mirroring real usage (typing while closed still works too)
        Assert.True(IsOpen(cut));

        input.Input("2026-06-15");
        input.KeyDown("Enter");

        Assert.Equal(new DateOnly(2026, 6, 15), committed);
        Assert.Equal("2026-06-15", cut.Find("input").GetAttribute("value"));
        Assert.False(IsOpen(cut));
    }

    [Fact]
    public void Escape_Reverts_The_Buffer_To_The_Last_Committed_Value_Without_Committing()
    {
        var valueChangedCount = 0;
        var callback = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? _) => valueChangedCount++);
        var cut = RenderPicker(value: new DateOnly(2026, 6, 10), valueChanged: callback);

        var input = cut.Find("input");
        input.Click();
        Assert.True(IsOpen(cut));

        input.Input("garbage-not-a-date");
        input.KeyDown("Escape");

        Assert.Equal(0, valueChangedCount);
        Assert.Equal("2026-06-10", cut.Find("input").GetAttribute("value"));
        Assert.False(IsOpen(cut));
    }

    [Fact]
    public void Escape_Closes_Even_When_The_Buffer_Was_Never_Touched()
    {
        var cut = RenderPicker();
        var input = cut.Find("input");
        input.Click();
        Assert.True(IsOpen(cut));

        input.KeyDown("Escape");

        Assert.False(IsOpen(cut));
    }

    // --- Range mode has no typeable-input HandleInputKeyDown surface ---

    [Fact]
    public void Range_Mode_Renders_The_Button_Trigger_Not_A_Typeable_Input()
    {
        // UsesTypeableInput excludes Range — DateRangePicker (which always sets
        // Mode=Range) therefore never gets HandleInputKeyDown's Enter/Escape;
        // navigation inside the popover is entirely the Calendar's own grid
        // keyboard handling (covered by the Calendar test suite).
        var cut = _ctx.Render<L.DatePicker>(p => p.Add(c => c.Mode, L.DatePicker.DatePickerMode.Range));

        Assert.Empty(cut.FindAll("input"));
        Assert.NotEmpty(cut.FindAll("button[type='button']"));
    }

    // --- DateWheelPicker: no keyboard support of its own (documented gap) ---

    [Fact]
    public void DateWheelPicker_Columns_Have_No_Keydown_Handler_Or_Tabindex()
    {
        // Verified against DateWheelPicker.razor: purely scroll/pointer-driven
        // (@onscroll on each column, no @onkeydown anywhere). This test pins
        // down that reality rather than a keyboard interaction that doesn't
        // exist — it is a real a11y gap, out of scope to fix in this pass.
        var cut = _ctx.Render<L.DateWheelPicker>();

        var columns = cut.FindAll("div.overflow-y-scroll");
        Assert.NotEmpty(columns); // guard against a vacuous pass if the markup class ever changes

        foreach (var col in columns)
        {
            Assert.Null(col.GetAttribute("tabindex"));
        }
    }

    // --- Escape must only be contained while the picker itself has something to
    //     close (Codex P2 round-2, PR #356): @onkeydown:stopPropagation used to be
    //     an unconditional "true", so a typeable DatePicker nested in a Dialog/Sheet
    //     swallowed Escape even when the calendar was ALREADY closed, leaving the
    //     ancestor modal unable to dismiss on it. Bunit doesn't execute real DOM
    //     bubbling/stopPropagation (no browser), so the regression is pinned at the
    //     render-tree level: the directive is present (stops propagation) only while
    //     _isOpen is true, and absent (lets Escape bubble to the modal) once closed —
    //     temp-reverting to an unconditional "true" fails BOTH assertions below.

    [Fact]
    public void Input_Keydown_Stops_Propagation_While_The_Calendar_Is_Open()
    {
        var cut = RenderPicker(value: new DateOnly(2026, 6, 10));
        var input = cut.Find("input");
        input.Click();
        Assert.True(IsOpen(cut));

        Assert.Contains("onkeydown:stoppropagation", cut.Find("input").OuterHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Input_Keydown_Does_Not_Stop_Propagation_While_The_Calendar_Is_Closed()
    {
        // Closed from the start — nothing for HandleInputKeyDown to revert/close,
        // so Escape (or any other key) must be free to bubble to an ancestor
        // Dialog/Sheet's own Escape-to-dismiss handler.
        var cut = RenderPicker(value: new DateOnly(2026, 6, 10));
        Assert.False(IsOpen(cut));

        Assert.DoesNotContain("onkeydown:stoppropagation", cut.Find("input").OuterHtml, StringComparison.OrdinalIgnoreCase);
    }

    // --- Trigger activation keys must not fire from the typeable input, even
    //     while the calendar is CLOSED (Codex/CodeRabbit round-2, PR #356):
    //     @onkeydown:stopPropagation above is gated on _isOpen, so while closed
    //     it lets everything bubble (by design — see the "does not stop
    //     propagation while closed" test above, needed for Escape/Dialog). That
    //     meant Enter/Space typed into the closed input ALSO reached
    //     PopoverTrigger's own role=button Enter/Space handler and silently
    //     reopened the calendar. Bunit can't execute real DOM bubbling to prove
    //     that directly (see the comment block above), so this is pinned one
    //     level down at the mechanism PopoverTrigger actually uses to suppress
    //     it: SuppressActivationKeys must reach the typeable-input trigger,
    //     proven via the same JS-registration signal
    //     PopoverTriggerSpaceSuppressionTests/PopoverKeyboardA11yTests use:
    //     with SuppressActivationKeys set, PopoverTrigger skips its Space-
    //     default-suppression registration entirely (nothing left to protect
    //     once the toggle itself is suppressed).
    //
    //     Round-6 (Codex P2) corrected an assumption baked into the ORIGINAL
    //     version of this test block: the plain button-trigger branch (Range/
    //     Multiple/custom-content) was believed to have "no competing Enter/
    //     Space handler of its own". That's false — its ChildContent IS a
    //     native <button>, and a native button's own Enter/Space default action
    //     synthesizes a click, which bubbles to the SAME wrapper and toggles it
    //     a second time (the manual keydown-Toggle plus the click-Toggle cancel
    //     out, so the calendar never opened from the keyboard). SuppressActivationKeys
    //     is therefore unconditional now — both branches need it, for different
    //     reasons — so the button trigger ALSO skips the wrapper's Space
    //     registration below (see DatePicker.razor's SuppressActivationKeys
    //     doc comment for the full mechanism).

    private static bool AnyRegisterPreventDefaultKeysFor(BunitContext ctx, string idPrefix) =>
        ctx.JSInterop.Invocations.Any(i =>
            i.Identifier == "registerPreventDefaultKeys"
            && i.Arguments[0] is string id && id.StartsWith(idPrefix, StringComparison.Ordinal));

    [Fact]
    public void Typeable_Input_Trigger_Suppresses_The_Wrapper_Space_Registration()
    {
        // Single/Month/Year modes render the typeable input inside PopoverTrigger
        // — SuppressActivationKeys must reach it, so the wrapper's own Space-
        // default suppression (which exists only to protect ITS OWN toggle) never
        // registers.
        var cut = RenderPicker(value: new DateOnly(2026, 6, 10));
        cut.Find("input").Click(); // open — OnAfterRenderAsync runs for PopoverTrigger

        Assert.False(AnyRegisterPreventDefaultKeysFor(_ctx, "popover-trigger-"));
    }

    [Fact]
    public void Button_Trigger_Also_Suppresses_The_Wrapper_Space_Registration()
    {
        // Round-6 (Codex P2): Range mode's plain button trigger renders a native
        // <button> as PopoverTrigger's ChildContent — that button's own Enter/
        // Space default action synthesizes a click that bubbles to the SAME
        // wrapper, so the wrapper's manual keydown-Toggle must be suppressed
        // (else it double-toggles, see Button_Trigger_Wrapper_Ignores_Enter_
        // Because_The_Native_Button_Owns_Activation below). With the toggle
        // suppressed there's nothing left for the Space-default JS rule to
        // protect, so it must NOT register either — mirrors the typeable-input
        // branch above.
        var cut = _ctx.Render<L.DatePicker>(p => p.Add(c => c.Mode, L.DatePicker.DatePickerMode.Range));
        cut.Find("button[type='button']").Click();

        Assert.False(AnyRegisterPreventDefaultKeysFor(_ctx, "popover-trigger-"));
    }

    [Fact]
    public void Button_Trigger_Wrapper_Ignores_Enter_Because_The_Native_Button_Owns_Activation()
    {
        // Round-6 (Codex P2): before this fix, PopoverTrigger's wrapper div
        // (role=button, @onkeydown="HandleKeyDown") toggled the popover itself
        // on Enter/Space bubbling from ANY child — including Range mode's plain
        // button trigger. But that child is a native <button>: the browser's
        // own Enter/Space default action ALSO synthesizes a click, which bubbles
        // to the same wrapper and toggles it again via @onclick="Toggle" — two
        // toggles cancel out and the calendar never opened from the keyboard.
        // Pinned directly on the wrapper's own keydown handler (bUnit doesn't
        // simulate real DOM bubbling, so this can't be proven by pressing Enter
        // on the inner button — see the file-level comment on why bubbling
        // assertions are pinned at the mechanism instead): with
        // SuppressActivationKeys now unconditional, the wrapper must stay
        // closed when Enter reaches IT directly, leaving the native button's
        // own click (fired once, for real, by the browser) as the sole toggle.
        var cut = _ctx.Render<L.DatePicker>(p => p.Add(c => c.Mode, L.DatePicker.DatePickerMode.Range));

        cut.Find("[role='button']").KeyDown("Enter");

        Assert.False(IsOpen(cut));
    }
}
