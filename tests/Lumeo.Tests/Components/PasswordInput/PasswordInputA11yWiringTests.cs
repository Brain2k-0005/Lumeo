using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;

namespace Lumeo.Tests.Components.PasswordInput;

/// <summary>
/// Triage #48 and #49 (medium, keyboard-a11y).
///
/// #48 — The visibility toggle &lt;button&gt; carried a single static
/// <c>aria-label="@L["Password.Toggle"]"</c> and no <c>aria-pressed</c>, so assistive
/// tech could never tell whether the password was currently shown or hidden — the
/// Eye/EyeOff icon swap was a purely visual cue. The fix makes the toggle a proper
/// toggle button: it exposes <c>aria-pressed</c> reflecting <c>_showPassword</c> and
/// swaps the <c>aria-label</c> between the localized Show/Hide strings on each state.
///
/// #49 — The strength-meter label updated silently: the &lt;p&gt; carried no
/// <c>role</c>/<c>aria-live</c>, so screen-reader users never heard the
/// Weak→Fair→Good→Strong transitions. The fix wraps the label in a polite live region
/// (<c>role="status" aria-live="polite"</c>), mirroring the existing error-text live
/// pattern further down the component.
///
/// These assert the directly observable rendered markup (aria-pressed / aria-label /
/// role / aria-live) after a state change — no reliance on real DOM focus, which bUnit
/// cannot move.
/// </summary>
public class PasswordInputA11yWiringTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PasswordInputA11yWiringTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- #48: toggle exposes aria-pressed + state-dependent aria-label ---

    [Fact]
    public void Toggle_Button_Exposes_Pressed_State_And_Updates_On_Click()
    {
        var cut = _ctx.Render<Lumeo.PasswordInput>(p => p
            .Add(c => c.Value, "secret"));

        var toggle = cut.Find("button");

        // Hidden by default: aria-pressed=false and the "show" affordance is announced.
        Assert.Equal("false", toggle.GetAttribute("aria-pressed"));
        var hiddenLabel = toggle.GetAttribute("aria-label");
        Assert.False(string.IsNullOrEmpty(hiddenLabel));

        // Reveal the password — the toggle's pressed state and label must flip so AT
        // can announce the new state (before the fix aria-pressed was absent and the
        // label was a single static string for both states).
        cut.Find("button").Click();

        var toggleAfter = cut.Find("button");
        Assert.Equal("true", toggleAfter.GetAttribute("aria-pressed"));
        var shownLabel = toggleAfter.GetAttribute("aria-label");
        Assert.False(string.IsNullOrEmpty(shownLabel));
        Assert.NotEqual(hiddenLabel, shownLabel);
    }

    // --- #49: strength meter label is a polite live region ---

    [Fact]
    public void Strength_Label_Is_A_Polite_Live_Region()
    {
        var cut = _ctx.Render<Lumeo.PasswordInput>(p => p
            .Add(c => c.ShowStrength, true)
            .Add(c => c.Value, "abc"));

        // The strength label <p> now carries a polite live region so each strength
        // transition is announced. Before the fix it had no role/aria-live and updated
        // silently. The error-text <p> uses aria-live="polite"/role="alert"; the
        // strength label mirrors the polite-announce idiom.
        var liveLabel = cut.FindAll("p")
            .First(p => p.GetAttribute("aria-live") == "polite"
                        && p.GetAttribute("role") == "status");

        Assert.Equal("status", liveLabel.GetAttribute("role"));
        Assert.Equal("polite", liveLabel.GetAttribute("aria-live"));
    }
}
