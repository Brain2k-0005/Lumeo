using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Checkbox;

/// <summary>
/// Checkbox renders <c>role="checkbox"</c> on a native &lt;button&gt; wired only with
/// @onclick (no @onkeydown). Per HTML semantics a native &lt;button&gt; synthesizes a
/// click for BOTH Enter and Space, and there is no code here that filters one out — so,
/// unlike a strict WAI-ARIA checkbox built on a non-button element (which should react to
/// Space only), Enter also toggles this control. bUnit cannot dispatch a keydown that
/// isn't wired to a handler, so .Click() exercises the exact code path the browser's
/// synthesized click runs for both keys — the only mechanism keyboard activation has here.
/// These tests pin the toggle/indeterminate-cycle outcome that activation produces and
/// confirm the underlying element really is a &lt;button&gt; (so native Enter/Space
/// semantics apply at all, unlike a div-based checkbox which would need explicit wiring).
/// </summary>
public class CheckboxKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public CheckboxKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Space_Or_Enter_Toggles_Unchecked_To_Checked()
    {
        bool? result = null;
        var cut = _ctx.Render<L.Checkbox>(p => p
            .Add(c => c.Checked, false)
            .Add(c => c.CheckedChanged, v => result = v));

        cut.Find("button[role='checkbox']").Click();

        Assert.True(result);
    }

    [Fact]
    public void Space_Or_Enter_Cycles_Indeterminate_To_Checked_Not_Back_To_Unchecked()
    {
        // Toggle() special-cases IsIndeterminate: activation must resolve straight to
        // Checked=true (never bounce to Unchecked), matching Radix's tri-state cycle.
        bool? checkedResult = null;
        bool? indeterminateResult = null;
        var cut = _ctx.Render<L.Checkbox>(p => p
            .Add(c => c.IsIndeterminate, true)
            .Add(c => c.CheckedChanged, v => checkedResult = v)
            .Add(c => c.IsIndeterminateChanged, v => indeterminateResult = v));

        cut.Find("button[role='checkbox']").Click();

        Assert.False(indeterminateResult);
        Assert.True(checkedResult);
    }

    [Fact]
    public void Control_Is_A_Real_Button_Element_So_Native_Enter_And_Space_Both_Apply()
    {
        var cut = _ctx.Render<L.Checkbox>();

        Assert.Equal("button", cut.Find("[role='checkbox']").TagName.ToLowerInvariant());
    }
}
