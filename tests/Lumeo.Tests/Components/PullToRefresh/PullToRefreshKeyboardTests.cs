using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.PullToRefresh;

// [keyboard-gap] — registry-gen scanner marker (Program.cs ComputeTestCoverage):
// this file is a documented NEGATIVE keyboard audit, not a positive one. It
// proves PullToRefresh has NO keyboard equivalent, so it must not count toward
// "hasKeyboard" in the generated a11y matrix — see the marker's own doc comment
// in Program.cs for why the content regex would otherwise misfire here (PR #356
// round-2, Codex/CodeRabbit). Keep this token present as long as the gap is real.
/// <summary>
/// Special-case a11y audit (not a wave-4 composition) — PullToRefresh is a
/// touch/pointer-drag gesture wrapper with NO keyboard equivalent. Verified by
/// source inspection: the root &lt;div&gt; carries no tabindex and no
/// @onkeydown; the only public surface is <c>OnRefresh</c> (a callback the
/// CONSUMER wires, invoked exclusively from <c>HandlePointerUp</c>) — there is
/// no public method (no <c>RefreshAsync()</c> counterpart to FileViewer's) a
/// keyboard-only consumer could call, and no sensible built-in button surface
/// exists in the component itself to wire one onto. Per the wave-4 brief: when
/// the scout's target gap has no sensible fix surface, PIN the honest reality
/// instead of inventing one. This is a genuine WCAG 2.1.1 gap — a keyboard-only
/// or switch-device user cannot trigger a refresh through this component alone
/// and the consuming app must provide its own refresh affordance (e.g. a
/// visible "Refresh" button elsewhere in the page) if that matters for their
/// audience.
/// </summary>
public class PullToRefreshKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PullToRefreshKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Root_Carries_No_Tabindex_Documenting_It_Is_Not_A_Keyboard_Target()
    {
        var cut = _ctx.Render<Lumeo.PullToRefresh>(p => p.AddChildContent("<p>content</p>"));

        var root = cut.Find("div[id^='ptr']");
        Assert.False(root.HasAttribute("tabindex"));
    }

    [Fact]
    public void Root_Has_No_Keydown_Handler_At_All()
    {
        // Stronger proof than "a keydown fires but does nothing": bUnit refuses
        // to dispatch an event the render tree never wired a handler for, which
        // is the load-bearing assertion here — the root has no @onkeydown of
        // its own, full stop, not merely one that happens to no-op on Enter.
        var cut = _ctx.Render<Lumeo.PullToRefresh>(p => p
            .Add(c => c.OnRefresh, () => { })
            .AddChildContent("<p>content</p>"));

        var root = cut.Find("div[id^='ptr']");
        var ex = Assert.Throws<Bunit.MissingEventHandlerException>(
            () => root.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" }));

        Assert.Contains("onkeydown", ex.Message);
    }
}
