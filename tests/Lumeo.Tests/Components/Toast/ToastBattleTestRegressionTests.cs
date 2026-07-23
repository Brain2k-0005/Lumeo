using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toast;

/// <summary>
/// Battle-test (wave 3) regressions for the Toast component family:
///
/// #23 (medium, lifecycle) — <c>RemoveWithExitAsync</c> was not idempotent.
///   Dismissing a toast that was already playing its exit animation re-ran
///   <c>OnDismiss</c>, cancelled the in-flight exit timer (truncating the
///   animation) and restarted it. The fix bails when <c>toast.Leaving</c> is
///   already set.
///
/// #64 (low, keyboard-a11y) — <c>ToastClose</c> carried
///   <c>@onfocusin:stopPropagation="true"</c>, so focus landing on the close
///   button (the primary keyboard dismiss target) never bubbled to the toast
///   root's <c>@onfocusin</c> handler and pause-on-focus never engaged. The fix
///   removes the stopPropagation so focusin bubbles to the provider's PauseTimer.
///
/// #65 (low, other) — <c>ToastAction</c>'s &lt;button&gt; had no <c>type</c>, so
///   it defaulted to <c>type="submit"</c> and would submit an enclosing form.
///   The fix adds <c>type="button"</c>.
///
/// bUnit honors the <c>:stopPropagation</c> flag while bubbling: triggering an
/// event on an element that stops propagation but has no own handler raises a
/// <see cref="MissingEventHandlerException"/> instead of reaching the ancestor
/// handler — exactly the signal the #64 test keys off.
/// </summary>
public class ToastBattleTestRegressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToastBattleTestRegressionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private ToastService GetToastService() =>
        (ToastService)_ctx.Services.GetRequiredService(typeof(ToastService));

    // ── #23 — dismissal must be idempotent (no double OnDismiss) ──────────────

    [Fact]
    public void Dismissing_An_Already_Leaving_Toast_Does_Not_Re_Run_OnDismiss()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        var dismissCount = 0;
        // Duration=0 disables the auto-dismiss timer so ONLY our close clicks
        // drive dismissal; OnDismiss increments a counter we can assert on.
        toastService.Show(new ToastOptions
        {
            Title = "Dismiss me",
            Duration = 0,
            OnDismiss = () => dismissCount++,
        });
        cut.WaitForState(
            () => cut.FindAll("[role='alert'],[role='status']").Count > 0,
            TimeSpan.FromSeconds(5));

        // First dismissal: runs OnDismiss once and marks the toast "leaving",
        // keeping it in the DOM (with .animate-toast-out) for the exit animation.
        cut.Find("button").Click();
        // WaitForState blocks until the toast is confirmed still-mounted AND leaving
        // (.animate-toast-out present), so the second dismissal below is guaranteed to
        // land while the toast is in the "leaving" state — the exact branch under test.
        // The exit window is a real ~220 ms Task.Delay; the generous ceiling only guards
        // against a starved thread pool delaying the leaving re-render, and returns the
        // instant the class appears.
        cut.WaitForState(
            () => cut.FindAll(".animate-toast-out").Count == 1,
            TimeSpan.FromSeconds(5));

        // Deflake (CI-starvation incident, overlay-exit doctrine applied): this used to be a bare
        // `Assert.Equal(1, dismissCount)` immediately after the poll above. That races a real gap
        // inside RemoveWithExitAsync: `Leaving = true` is stamped and StateHasChanged() called
        // (making .animate-toast-out observable — what the poll above waits for) BEFORE
        // `await UnregisterSwipe(id)`, and only AFTER that await resolves does `OnDismiss()` actually
        // fire. Under a starved CI thread pool that continuation can be delayed well past the moment
        // the "leaving" render commits, so the bare assert could read dismissCount==0. dismissCount
        // reaching (and, per this test's own idempotency guarantee, staying at) 1 is a genuine
        // monotonic latch, so poll for it instead of asserting on the assumption it already settled.
        cut.WaitForAssertion(() => Assert.Equal(1, dismissCount), TimeSpan.FromSeconds(5));

        // Second dismissal while the toast is still leaving must be a no-op.
        // Without the fix it re-runs OnDismiss (count -> 2) and truncates the
        // exit animation; with the fix it bails on toast.Leaving. The toast is
        // confirmed mounted by the WaitForState immediately above, so Find("button")
        // cannot throw on an empty match (the historic FindAll[0] index race).
        cut.Find("button").Click();

        // The panel unmounts when the first exit timer completes; poll for it with a
        // generous ceiling (real 220 ms timer, starvation-tolerant) rather than a sleep.
        cut.WaitForState(
            () => cut.FindAll("[role='alert'],[role='status']").Count == 0,
            TimeSpan.FromSeconds(5));

        Assert.Equal(1, dismissCount);
    }

    // ── #64 — close-button focusin must bubble so pause-on-focus engages ──────

    // Render ToastClose inside a host element that carries an @onfocusin handler,
    // standing in for the real Toast root (Toast.razor wires @onfocusin to the
    // provider's PauseTimer). If the close button stops propagation, bUnit raises
    // MissingEventHandlerException and the host handler never fires; with the fix
    // the focusin bubbles to the host handler.
    private IRenderedComponent<IComponent> RenderCloseInHost(Action onHostFocusIn)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "toast-root");
            builder.AddAttribute(2, "onfocusin",
                EventCallback.Factory.Create<FocusEventArgs>(this, _ => onHostFocusIn()));
            builder.OpenComponent<L.ToastClose>(3);
            builder.AddAttribute(4, "OnClose", EventCallback.Factory.Create(this, () => { }));
            builder.CloseComponent();
            builder.CloseElement();
        });
    }

    [Fact]
    public void Close_Button_FocusIn_Bubbles_To_Toast_Root_For_Pause_On_Focus()
    {
        var hostFocusReceived = false;
        var cut = RenderCloseInHost(() => hostFocusReceived = true);

        var button = cut.Find("button");

        // With the fix (no @onfocusin:stopPropagation on ToastClose) the focusin
        // bubbles up to the host's @onfocusin handler. Without the fix bUnit
        // stops at the button (stopPropagation set, no own focusin handler) and
        // throws MissingEventHandlerException, so the host never sees it.
        button.FocusIn();

        Assert.True(hostFocusReceived,
            "focusin on the toast close button must bubble to the toast root so PauseTimer engages.");
    }

    // ── #65 — ToastAction button must be type=button (never submit) ───────────

    [Fact]
    public void ToastAction_Button_Has_Type_Button()
    {
        var cut = _ctx.Render<L.ToastAction>(p => p.Add(b => b.Label, "Undo"));

        var button = cut.Find("button");

        Assert.Equal("button", button.GetAttribute("type"));
    }
}
