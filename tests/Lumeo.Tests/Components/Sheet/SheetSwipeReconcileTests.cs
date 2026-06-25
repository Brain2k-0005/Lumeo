using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sheet;

/// <summary>
/// Sheet state-on-data-change regressions (battle-wave2 #89 + #184):
///
/// #89 — SwipeToClose / PreventClose / SwipeDirection changed WHILE the Sheet
///   stays open must (un)register the swipe-to-dismiss gesture immediately
///   (mirroring DrawerContent.OnParametersSetAsync). Before the fix the gesture
///   was wired once on the open transition in OnAfterRenderAsync and ignored
///   every later toggle until a full close + reopen.
///
/// #184 — Re-opening the Sheet during its exit animation must paint the ENTER
///   animation class on the first re-open render, not one frame of the slide-OUT
///   (exit) class. Before the fix _exiting was cleared only later in
///   OnAfterRenderAsync, so the re-open render still saw the exit latch.
/// </summary>
public class SheetSwipeReconcileTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public SheetSwipeReconcileTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment SheetContentFragment(bool swipeToClose, bool preventClose) => b =>
    {
        b.OpenComponent<L.SheetContent>(0);
        b.AddAttribute(1, "Side", L.Side.Right);
        b.AddAttribute(2, "SwipeToClose", swipeToClose);
        b.AddAttribute(3, "PreventClose", preventClose);
        b.AddAttribute(4, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Body")));
        b.CloseComponent();
    };

    private IRenderedComponent<L.Sheet> RenderSheet(bool open, bool swipeToClose, bool preventClose = false)
    {
        return _ctx.Render<L.Sheet>(p => p
            .Add(s => s.Open, open)
            .Add(s => s.ChildContent, SheetContentFragment(swipeToClose, preventClose)));
    }

    // ---- #89: swipe (un)registers on a param toggle while the sheet stays open ----

    [Fact]
    public void Enabling_SwipeToClose_While_Open_Registers_The_Gesture()
    {
        // Open with swipe OFF — no gesture wired yet.
        var cut = RenderSheet(open: true, swipeToClose: false);
        Assert.Empty(_interop.DrawerSwipeRegistrations);

        // Toggle SwipeToClose true while the sheet STAYS open (Open is still true).
        cut.Render(p => p
            .Add(s => s.Open, true)
            .Add(s => s.ChildContent, SheetContentFragment(swipeToClose: true, preventClose: false)));

        // The fix re-evaluates eligibility in OnParametersSetAsync and registers
        // the gesture now, instead of waiting for a close + reopen.
        var reg = Assert.Single(_interop.DrawerSwipeRegistrations);
        Assert.Equal("right", reg.Direction);
    }

    [Fact]
    public void Disabling_SwipeToClose_While_Open_Unregisters_The_Gesture()
    {
        // Open with swipe ON — gesture wired on the open transition.
        var cut = RenderSheet(open: true, swipeToClose: true);
        Assert.Single(_interop.DrawerSwipeRegistrations);
        Assert.Empty(_interop.DrawerSwipeUnregistrations);

        // Toggle SwipeToClose false while still open.
        cut.Render(p => p
            .Add(s => s.Open, true)
            .Add(s => s.ChildContent, SheetContentFragment(swipeToClose: false, preventClose: false)));

        // The fix tears the now-ineligible gesture down immediately.
        Assert.NotEmpty(_interop.DrawerSwipeUnregistrations);
    }

    // ---- #184: re-open mid-exit shows the ENTER class, not the exit class ----

    [Fact]
    public void Reopening_During_Exit_Shows_Enter_Animation_Not_Exit()
    {
        // Open, then close — the panel lingers with the slide-OUT class while the
        // exit animation plays (it stays mounted via the _exiting latch).
        var cut = RenderSheet(open: true, swipeToClose: false);
        cut.Render(p => p
            .Add(s => s.Open, false)
            .Add(s => s.ChildContent, SheetContentFragment(swipeToClose: false, preventClose: false)));

        cut.WaitForAssertion(() =>
        {
            var d = cut.Find("[role='dialog']");
            Assert.Contains("animate-slide-out-to-right", d.GetAttribute("class") ?? "");
        });

        // Re-open BEFORE the exit window elapses (the panel is still mounted).
        cut.Render(p => p
            .Add(s => s.Open, true)
            .Add(s => s.ChildContent, SheetContentFragment(swipeToClose: false, preventClose: false)));

        // The fix clears the exit latch in OnParametersSetAsync before the render
        // commits, so the first re-open frame carries the enter class and never
        // the exit class.
        var dialog = cut.Find("[role='dialog']");
        var css = dialog.GetAttribute("class") ?? "";
        Assert.Contains("animate-slide-in-from-right", css);
        Assert.DoesNotContain("animate-slide-out-to-right", css);
    }
}
