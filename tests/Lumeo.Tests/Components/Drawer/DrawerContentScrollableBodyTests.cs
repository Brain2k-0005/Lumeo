using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Drawer;

/// <summary>
/// Regression tests for a reported bug: a consumer's form (EditForm, or any
/// sufficiently tall block content) inside DrawerContent became unreachable —
/// the panel caps its own height (max-h-[96vh] Top/Bottom, max-h-screen
/// Left/Right) but never gave itself a scroll fallback, so once content
/// exceeded that cap the excess simply rendered past the panel's bottom edge
/// with no way to scroll to it. Verified with a real headless Chromium render:
/// the header stayed visible, but everything below a certain point — including
/// the footer/submit button — rendered completely off-screen and was
/// permanently unreachable. Fix: overflow-y-auto on the panel turns that
/// overflow into an internally-scrollable region; DrawerHeader/DrawerFooter
/// get shrink-0 defensively so they're never squeezed by the flex-shrink
/// algorithm either.
/// </summary>
public class DrawerContentScrollableBodyTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DrawerContentScrollableBodyTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderDrawer(RenderFragment content)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                b.AddAttribute(1, "ChildContent", content);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Panel_Root_Has_Overflow_Y_Auto_So_Tall_Content_Stays_Reachable()
    {
        var cut = RenderDrawer(b => b.AddContent(0, "Content"));

        var panel = cut.Find("[role='dialog']");
        Assert.Contains("overflow-y-auto", panel.GetAttribute("class"));
    }

    [Fact]
    public void Panel_Root_Still_Applies_Its_Height_Cap_Alongside_The_Scroll_Fallback()
    {
        // The fix adds a scroll fallback — it must not remove the existing
        // height cap that makes vaul-style bottom drawers behave like a sheet
        // rather than growing to fill the whole viewport.
        var cut = RenderDrawer(b => b.AddContent(0, "Content"));

        var panel = cut.Find("[role='dialog']").GetAttribute("class");
        Assert.Contains("max-h-[96vh]", panel); // default Side.Bottom
        Assert.Contains("overflow-y-auto", panel);
    }

    [Fact]
    public void DrawerHeader_Is_Pinned_Against_Flex_Shrink()
    {
        var cut = RenderDrawer(b =>
        {
            b.OpenComponent<L.DrawerHeader>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Title")));
            b.CloseComponent();
        });

        var headers = cut.FindAll("div").Where(d => d.TextContent.Contains("Title")).ToList();
        Assert.Contains(headers, h => (h.GetAttribute("class") ?? "").Contains("shrink-0"));
    }

    [Fact]
    public void DrawerFooter_Is_Pinned_Against_Flex_Shrink()
    {
        var cut = RenderDrawer(b =>
        {
            b.OpenComponent<L.DrawerFooter>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Actions")));
            b.CloseComponent();
        });

        var footers = cut.FindAll("div").Where(d => d.TextContent.Contains("Actions")).ToList();
        Assert.Contains(footers, f => (f.GetAttribute("class") ?? "").Contains("shrink-0"));
    }

    // #381 Codex P2: overflow-y-auto alone computes overflow-x to "auto" too
    // (the same CSS quirk class documented on GanttTimeline's RootClass, the
    // other axis) — clipping anything that visually extends past the panel's
    // horizontal edges, including a focus ring on flush-edge content. p-1.5
    // reserves just enough inset to contain a standard ring-offset-2 ring;
    // verified with a real headless Chromium render (a flush button's ring
    // clips at zero padding, is fully visible at 6px/p-1.5).
    [Fact]
    public void Panel_Root_Reserves_Padding_So_Flush_Edge_Focus_Rings_Are_Not_Clipped()
    {
        var cut = RenderDrawer(b => b.AddContent(0, "Content"));

        Assert.Contains("p-1.5", cut.Find("[role='dialog']").GetAttribute("class"));
    }

    // #381 Codex round 2, P2: the visual drag handle is the stable DOM hook
    // components.js's gesture handlers use to identify a touch that started
    // on it (those always arm dismiss regardless of the panel's scroll
    // position). Asserting the structural hook is what's bUnit-able —
    // the actual touch-gesture discrimination is JS-only behavior; see the
    // task report for the manual/E2E verification steps.
    [Fact]
    public void Drag_Handle_Carries_The_Stable_Gesture_Hook_For_A_Top_Or_Bottom_Drawer()
    {
        var cut = RenderDrawer(b => b.AddContent(0, "Content")); // default Side.Bottom -> handle shown

        var handle = cut.Find("[data-drawer-handle]");
        Assert.Equal("true", handle.GetAttribute("data-drawer-handle"));
    }

    // #381 round 6 (P2) / Codex finding "Keep the drag handle outside the
    // scrolling element": the handle is a sibling of ChildContent inside the
    // SAME overflow-y-auto flex column the JS gesture reads scrollTop from
    // (moving it to a separate inner scroller would break that contract), so
    // it stays reachable via "sticky top-0" instead — pinned to the scroll
    // container's own top edge rather than scrolling away with tall content.
    [Fact]
    public void Drag_Handle_Is_Sticky_So_It_Survives_Scrolling_Past_It()
    {
        var cut = RenderDrawer(b => b.AddContent(0, "Content"));

        var handle = cut.Find("[data-drawer-handle]");
        var classes = handle.GetAttribute("class") ?? "";
        Assert.Contains("sticky", classes);
        Assert.Contains("top-0", classes);
    }

    [Fact]
    public void No_Drag_Handle_Hook_For_A_Left_Or_Right_Drawer()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                b.AddAttribute(1, "Side", L.Side.Right);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll("[data-drawer-handle]"));
    }

    // #381 round 10 (P2) — "Avoid disabling touch scrolling when dragging is
    // disabled": for PreventClose + no SnapPoints, RegisterGestureAsync
    // registers NO gesture at all, so the handle's touch-none strip used to
    // be a dead touch region (native scroll blocked, no listener to hand the
    // touch to instead). The handle must not render in this configuration.
    [Fact]
    public void No_Drag_Handle_When_PreventClose_And_No_SnapPoints()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                b.AddAttribute(1, "PreventClose", true);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll("[data-drawer-handle]"));
    }

    // Control: a plain (PreventClose=false) drawer still gets the handle —
    // RegisterGestureAsync registers the plain swipe-to-dismiss gesture.
    [Fact]
    public void Drag_Handle_Present_When_PreventClose_Is_False()
    {
        var cut = RenderDrawer(b => b.AddContent(0, "Content"));

        Assert.NotEmpty(cut.FindAll("[data-drawer-handle]"));
    }

    // Control: PreventClose + SnapPoints still gets the handle —
    // RegisterGestureAsync always registers the snap gesture regardless of
    // PreventClose (a protected drawer still snaps between points, per #345;
    // it just never dismisses), so there IS a live gesture under the handle.
    [Fact]
    public void Drag_Handle_Present_When_PreventClose_With_SnapPoints()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                b.AddAttribute(1, "PreventClose", true);
                b.AddAttribute(2, "SnapPoints", new double[] { 0.4, 1.0 });
                b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Content")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.NotEmpty(cut.FindAll("[data-drawer-handle]"));
    }
}
