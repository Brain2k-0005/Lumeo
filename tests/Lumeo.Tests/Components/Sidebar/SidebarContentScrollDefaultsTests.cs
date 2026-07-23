using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// Regression test for a reported bug: SidebarContent's root defaulted to
/// "overflow-auto" (both axes). Nav content routinely grows taller than the
/// viewport, so the vertical scrollbar appears and steals width from the
/// content box — every full-width entry then reports as slightly too wide
/// for the now-narrower box, triggering a spurious HORIZONTAL scrollbar too.
/// Fix: scroll only the vertical axis (overflow-y-auto/overflow-x-hidden,
/// the same pair the sidebar's own root <c>&lt;aside&gt;</c> already uses)
/// and reserve the scrollbar's track space up front (scrollbar-gutter:stable)
/// so the content box doesn't reflow width-wise when the list crosses one page.
/// </summary>
public class SidebarContentScrollDefaultsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SidebarContentScrollDefaultsTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Default_Root_Class_Scrolls_Only_The_Vertical_Axis()
    {
        var cut = _ctx.Render<Lumeo.SidebarContent>();

        var cssClass = cut.Find("div").GetAttribute("class");
        Assert.Contains("overflow-y-auto", cssClass);
        Assert.Contains("overflow-x-hidden", cssClass);
        Assert.DoesNotContain("overflow-auto", cssClass);
    }

    [Fact]
    public void Default_Root_Class_Reserves_A_Stable_Scrollbar_Gutter()
    {
        var cut = _ctx.Render<Lumeo.SidebarContent>();

        Assert.Contains("scrollbar-gutter:stable", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Class_Override_Still_Merges_Onto_The_Root()
    {
        var cut = _ctx.Render<Lumeo.SidebarContent>(p => p
            .Add(c => c.Class, "my-sidebar-content"));

        var cssClass = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-sidebar-content", cssClass);
        Assert.Contains("overflow-y-auto", cssClass);
    }

    // ── #381 Codex P1: scrollbar-gutter scoped to the EXPANDED state only ───
    // (a classic-scrollbar platform's reserved ~15px track would otherwise eat
    // a real chunk of a collapsed icon rail's 48px width, which never scrolls).

    private IRenderedComponent<L.SidebarProvider> RenderWithProvider(bool isCollapsed)
    {
        return _ctx.Render<L.SidebarProvider>(p => p
            .Add(x => x.IsCollapsed, isCollapsed)
            .Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarContent>(0);
                b.CloseComponent();
            })));
    }

    [Fact]
    public void Scrollbar_Gutter_Present_When_Sidebar_Is_Expanded()
    {
        var cut = RenderWithProvider(isCollapsed: false);

        var sidebarContentDiv = cut.FindAll("div").First(d => (d.GetAttribute("class") ?? "").Contains("overflow-y-auto"));
        Assert.Contains("scrollbar-gutter:stable", sidebarContentDiv.GetAttribute("class"));
    }

    [Fact]
    public void Scrollbar_Gutter_Omitted_When_Sidebar_Is_Collapsed()
    {
        var cut = RenderWithProvider(isCollapsed: true);

        var sidebarContentDiv = cut.FindAll("div").First(d => (d.GetAttribute("class") ?? "").Contains("overflow-y-auto"));
        Assert.DoesNotContain("scrollbar-gutter:stable", sidebarContentDiv.GetAttribute("class"));
        // The vertical-only scroll fix itself is unaffected by collapse state.
        Assert.Contains("overflow-y-auto", sidebarContentDiv.GetAttribute("class"));
    }
}
