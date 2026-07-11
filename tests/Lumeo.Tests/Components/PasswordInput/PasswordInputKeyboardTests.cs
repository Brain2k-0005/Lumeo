using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PasswordInput;

/// <summary>
/// PasswordInput's typing surface is a native &lt;input&gt; and PasswordInputTests.cs /
/// PasswordInputToggleFocusTests.cs / PasswordInputA11yWiringTests.cs already pin the
/// toggle button's click-driven type-flip, its Tab reachability and its live-region
/// announcement. The one keyboard-order concern nothing else covers: the toggle button
/// must render AFTER the input in DOM order, because DOM order is what determines the
/// Tab sequence a keyboard user actually experiences (input first, then the reveal
/// toggle) — a purely visual reorder (e.g. via Class) would break that expectation
/// invisibly to a sighted mouse user but not to someone tabbing through the field.
/// </summary>
public class PasswordInputKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public PasswordInputKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Toggle_Button_Follows_The_Input_In_DOM_Order_So_Tab_Reaches_It_Second()
    {
        var cut = _ctx.Render<L.PasswordInput>(p => p.Add(c => c.ShowToggle, true));

        var input = cut.Find("input");
        var toggle = cut.Find("button");

        var inputIndex = cut.Markup.IndexOf(input.OuterHtml[..20], StringComparison.Ordinal);
        var toggleIndex = cut.Markup.IndexOf(toggle.OuterHtml[..20], StringComparison.Ordinal);

        Assert.True(inputIndex >= 0 && toggleIndex >= 0);
        Assert.True(inputIndex < toggleIndex);
    }

    [Fact]
    public void Toggle_Button_Is_A_Real_Button_Element_So_Native_Enter_And_Space_Both_Apply()
    {
        // A native <button> synthesizes a click for Enter AND Space with no extra
        // wiring needed — bUnit can't dispatch a keydown with no handler registered,
        // so this pins the mechanism (the element being a real <button>) that makes
        // keyboard activation work at all, complementing the click-driven type-flip
        // test already in PasswordInputTests.cs.
        var cut = _ctx.Render<L.PasswordInput>(p => p.Add(c => c.ShowToggle, true));

        Assert.Equal("button", cut.Find("button").TagName.ToLowerInvariant());
    }
}
