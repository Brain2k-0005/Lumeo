using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Switch;

/// <summary>
/// Switch renders <c>role="switch"</c> on a native &lt;button&gt; wired only with
/// @onclick — same WAI-ARIA toggle-button pattern as Checkbox, so the browser's Enter/
/// Space-synthesized click is the sole keyboard activation mechanism, and .Click()
/// exercises the exact handler that synthesized click runs (bUnit cannot dispatch a
/// keydown with no registered handler). These tests pin the toggle outcome via aria-
/// checked and the Loading gate (a real Toggle()-guard branch not covered by the
/// generic Switch tests) that must also block keyboard activation, not just clicks.
/// </summary>
public class SwitchKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SwitchKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Space_Or_Enter_Toggles_Checked_And_AriaChecked()
    {
        bool? result = null;
        var cut = _ctx.Render<L.Switch>(p => p
            .Add(s => s.Checked, false)
            .Add(s => s.CheckedChanged, v => result = v));

        cut.Find("button[role='switch']").Click();

        Assert.True(result);
        Assert.Equal("true", cut.Find("button[role='switch']").GetAttribute("aria-checked"));
    }

    [Fact]
    public void Loading_Switch_Ignores_Activation_Even_Though_It_Is_Not_Disabled()
    {
        // Loading gates Toggle() (`if (!Disabled && !Loading)`) independently of the
        // typed Disabled parameter — activation must be a no-op while a request is
        // in flight, exactly like a disabled control would be for a keyboard user.
        bool? result = null;
        var cut = _ctx.Render<L.Switch>(p => p
            .Add(s => s.Checked, false)
            .Add(s => s.Loading, true)
            .Add(s => s.CheckedChanged, v => result = v));

        var button = cut.Find("button[role='switch']");
        Assert.NotNull(button.GetAttribute("disabled")); // native button excludes it from Tab order too

        button.Click();

        Assert.Null(result);
    }
}
