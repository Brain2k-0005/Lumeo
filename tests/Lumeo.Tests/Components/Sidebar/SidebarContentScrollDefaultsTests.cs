using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

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
}
