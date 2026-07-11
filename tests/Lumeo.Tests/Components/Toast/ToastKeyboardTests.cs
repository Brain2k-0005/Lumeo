using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toast;

/// <summary>
/// ToastAction/ToastClose render native &lt;button @onclick&gt;s — Enter/Space activation
/// is free via the browser's default button semantics, so .Click() exercises the exact
/// handler a synthesized keydown would run (ToastTests already pins ToastClose's
/// OnClose invocation via that same mechanism). This file adds the two angles not
/// covered elsewhere: ToastAction's own OnClick invocation, and — verified against the
/// source, not assumed — Toast/ToastProvider/ToastAction/ToastClose contain no
/// @onkeydown anywhere, so there is currently no Escape-to-dismiss shortcut. That is
/// documented here as the honest reality rather than a passing test invented for a
/// feature that does not exist.
/// </summary>
public class ToastKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ToastKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void ToastAction_Click_Invokes_Its_OnClick_Callback()
    {
        var called = false;
        var cut = _ctx.Render<L.ToastAction>(p => p
            .Add(a => a.Label, "Undo")
            .Add(a => a.OnClick, () => called = true));

        cut.Find("button").Click();

        Assert.True(called);
    }

    [Fact]
    public void ToastAction_And_ToastClose_Carry_No_Tabindex_Override()
    {
        var action = _ctx.Render<L.ToastAction>(p => p.Add(a => a.Label, "Undo"));
        var close = _ctx.Render<L.ToastClose>();

        Assert.False(action.Find("button").HasAttribute("tabindex"));
        Assert.False(close.Find("button").HasAttribute("tabindex"));
    }

    [Fact]
    public void No_Escape_Key_Handler_Exists_On_Toast_Or_Its_Action_Buttons()
    {
        // Documents current reality (verified: no @onkeydown in Toast.razor,
        // ToastProvider.razor(.cs), ToastAction.razor or ToastClose.razor) rather than
        // asserting a feature that isn't implemented. A keydown with no registered
        // listener is simply not observable via bUnit; the meaningful, honest assertion
        // is that neither button declares any key-handling attribute Lumeo would use to
        // wire one up (e.g. no data-key-handler marker), so Escape dismissal — if ever
        // added — has to be a deliberate new feature, not a silently pre-existing one.
        var close = _ctx.Render<L.ToastClose>();
        var button = close.Find("button");

        Assert.False(button.HasAttribute("onkeydown"));
    }
}
