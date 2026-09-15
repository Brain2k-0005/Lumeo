using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Drawer;

/// <summary>
/// #346 findings B and C.
///
/// B — dismissible vs modal split: vaul distinguishes <c>dismissible</c> (can the
/// user close it — swipe/overlay click/Escape) from <c>modal</c> (backdrop + focus
/// trap + scroll lock, vs a non-modal drawer that leaves the page interactive).
/// Lumeo already had <c>PreventClose</c> as the <c>dismissible=false</c> equivalent
/// (it also disables the swipe gesture); this adds <see cref="L.DrawerContent.Modal"/>
/// (default <c>true</c>) as the independent <c>modal</c> axis. The two compose freely.
///
/// C — built-in close (X) button: SheetContent already has ShowCloseButton;
/// DrawerContent required a manual DrawerClose. New bool? ShowCloseButton mirrors
/// SheetContent's parameter name, default (null = legacy PreventClose coupling),
/// markup (lumeo-drawer-close hook class, z-10), and aria-label key (Dialog.Close).
/// </summary>
public class DrawerModalAndCloseButtonTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public DrawerModalAndCloseButtonTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderDrawer(bool? modal = null, bool preventClose = false, bool? showCloseButton = null)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.Drawer>(0);
            builder.AddAttribute(1, "IsOpen", true);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.DrawerContent>(0);
                var seq = 1;
                if (modal is not null) b.AddAttribute(seq++, "Modal", modal.Value);
                b.AddAttribute(seq++, "PreventClose", preventClose);
                if (showCloseButton is not null) b.AddAttribute(seq++, "ShowCloseButton", showCloseButton.Value);
                b.AddAttribute(seq, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Drawer body")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    // ---- Modal (finding B) ---------------------------------------------------------

    [Fact]
    public void Default_Parameter_Value_Is_Modal_True()
    {
        Assert.True(new L.DrawerContent().Modal);
    }

    [Fact]
    public void Modal_True_Default_Renders_A_Backdrop_Locks_Scroll_And_Traps_Focus()
    {
        var cut = RenderDrawer();

        var wrapper = cut.Find("[data-slot='drawer-content']");
        Assert.NotNull(wrapper.QuerySelector(".animate-fade-in"));
        Assert.Single(_interop.FocusTrapSetups);
    }

    [Fact]
    public void Modal_False_Renders_No_Backdrop_No_Focus_Trap_No_Scroll_Lock()
    {
        var cut = RenderDrawer(modal: false);

        var wrapper = cut.Find("[data-slot='drawer-content']");
        Assert.Null(wrapper.QuerySelector(".animate-fade-in"));
        Assert.Empty(_interop.FocusTrapSetups);
    }

    [Fact]
    public void Modal_False_Sets_Aria_Modal_False_Even_While_Open()
    {
        var cut = RenderDrawer(modal: false);
        var panel = cut.Find("[role='dialog']");
        Assert.Equal("false", panel.GetAttribute("aria-modal"));
    }

    [Fact]
    public void Modal_True_Sets_Aria_Modal_True_While_Open()
    {
        var cut = RenderDrawer(modal: true);
        var panel = cut.Find("[role='dialog']");
        Assert.Equal("true", panel.GetAttribute("aria-modal"));
    }

    [Fact]
    public void Closing_A_Modal_Drawer_Removes_The_Focus_Trap_And_Unlocks_Scroll()
    {
        var cut = _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, true)
            .AddChildContent<L.DrawerContent>(cp => cp.AddChildContent("Body")));

        Assert.Single(_interop.FocusTrapSetups);
        cut.Render(p => p.Add(d => d.Open, false));
        Assert.Single(_interop.FocusTrapRemovals);
    }

    [Fact]
    public void Closing_A_NonModal_Drawer_Never_Touched_The_Focus_Trap()
    {
        var cut = _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, true)
            .AddChildContent<L.DrawerContent>(cp => cp
                .Add(c => c.Modal, false)
                .AddChildContent("Body")));

        cut.Render(p => p.Add(d => d.Open, false));

        Assert.Empty(_interop.FocusTrapSetups);
        Assert.Empty(_interop.FocusTrapRemovals);
    }

    // Fix round 1 (task review) — DrawerContent used to read the LIVE Modal
    // value at both the open branch and Cleanup(). A Modal flip WHILE the
    // drawer stays open, followed by close, used to either leak the global
    // ref-counted scrollLockCount (locked at open under Modal=true, Cleanup's
    // `if (Modal)` skips UnlockScroll because Modal is now false) or wrongly
    // call Remove/Unlock for a session that never applied them. Fixed via a
    // _modalApplied flag (mirrors _scaleApplied) latched at open time and
    // read — not Modal — at Cleanup time.
    [Fact]
    public void Modal_Flipped_False_While_Open_Still_Balances_Lock_And_Trap_On_Close()
    {
        var cut = _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, true)
            .AddChildContent<L.DrawerContent>(cp => cp.Add(c => c.Modal, true).AddChildContent("Body")));

        Assert.Equal(1, _interop.LockScrollCallCount);
        Assert.Single(_interop.FocusTrapSetups);

        // Flip Modal false WHILE the drawer stays open (SetParametersAndRender
        // via bUnit's typed Render, targeting DrawerContent directly this time
        // so Open itself doesn't change).
        var drawerContent = cut.FindComponent<L.DrawerContent>();
        drawerContent.Render(p => p.Add(c => c.Modal, false).Add(c => c.ChildContent, (RenderFragment)(b => b.AddContent(0, "Body"))));

        cut.Render(p => p.Add(d => d.Open, false));

        // Exactly one Unlock/Remove — the pair this session actually applied
        // at open, no more (no leak from the live-Modal read) and no less
        // (Cleanup still tears down what was really locked/trapped).
        Assert.Equal(1, _interop.UnlockScrollCallCount);
        Assert.Single(_interop.FocusTrapRemovals);
    }

    [Fact]
    public void Modal_Flipped_True_While_Open_After_A_NonModal_Open_Never_Calls_Unlock_Or_Remove()
    {
        // Mirror case: opened non-modal (never locked/trapped), Modal flips
        // true while it stays open, then closes. Cleanup must not call
        // Unlock/Remove for a lock/trap this session never applied.
        var cut = _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, true)
            .AddChildContent<L.DrawerContent>(cp => cp.Add(c => c.Modal, false).AddChildContent("Body")));

        Assert.Equal(0, _interop.LockScrollCallCount);
        Assert.Empty(_interop.FocusTrapSetups);

        var drawerContent = cut.FindComponent<L.DrawerContent>();
        drawerContent.Render(p => p.Add(c => c.Modal, true).Add(c => c.ChildContent, (RenderFragment)(b => b.AddContent(0, "Body"))));

        cut.Render(p => p.Add(d => d.Open, false));

        Assert.Equal(0, _interop.UnlockScrollCallCount);
        Assert.Empty(_interop.FocusTrapRemovals);
    }

    [Fact]
    public void Modal_Is_Independent_Of_PreventClose_NonModal_Can_Still_Be_PreventClose()
    {
        // PreventClose still disables swipe/backdrop/Escape dismissal; Modal only
        // controls whether the page is blocked. The two compose freely.
        var cut = RenderDrawer(modal: false, preventClose: true);

        var wrapper = cut.Find("[data-slot='drawer-content']");
        Assert.Null(wrapper.QuerySelector(".animate-fade-in"));
        Assert.Empty(cut.FindAll("[data-drawer-handle]")); // PreventClose + no SnapPoints -> no gesture, mirrors DrawerSizeTests' own control case
    }

    // ---- ShowCloseButton (finding C) -----------------------------------------------

    [Fact]
    public void Default_Parameter_Value_Is_ShowCloseButton_Null()
    {
        Assert.Null(new L.DrawerContent().ShowCloseButton);
    }

    [Fact]
    public void Default_Coupling_X_Follows_PreventClose()
    {
        Assert.NotEmpty(RenderDrawer(preventClose: false).FindAll(".lumeo-drawer-close"));
        Assert.Empty(RenderDrawer(preventClose: true).FindAll(".lumeo-drawer-close"));
    }

    [Fact]
    public void ShowCloseButton_True_Forces_The_X_On_Regardless_Of_PreventClose()
    {
        var cut = RenderDrawer(preventClose: true, showCloseButton: true);
        Assert.NotEmpty(cut.FindAll(".lumeo-drawer-close"));
    }

    [Fact]
    public void ShowCloseButton_False_Hides_The_X_For_Custom_Chrome()
    {
        var cut = RenderDrawer(preventClose: false, showCloseButton: false);
        Assert.Empty(cut.FindAll(".lumeo-drawer-close"));
    }

    [Fact]
    public void Close_Button_Carries_The_Stable_Hook_Class_And_ZIndex_And_Aria_Label()
    {
        var x = RenderDrawer().Find(".lumeo-drawer-close");
        Assert.Contains("z-10", x.ClassList);
        Assert.Equal("Close", x.GetAttribute("aria-label"));
    }

    [Fact]
    public void Clicking_The_Close_Button_Dismisses_The_Drawer()
    {
        var open = true;
        var cut = _ctx.Render<L.Drawer>(p => p
            .Add(d => d.Open, open)
            .Add(d => d.OpenChanged, EventCallback.Factory.Create<bool>(this, v => open = v))
            .AddChildContent<L.DrawerContent>(cp => cp.AddChildContent("Body")));

        cut.Find(".lumeo-drawer-close").Click();

        Assert.False(open);
    }
}
