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
}
