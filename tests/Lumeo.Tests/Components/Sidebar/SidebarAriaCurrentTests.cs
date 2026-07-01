using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// The active sidebar nav item must expose aria-current="page" (WAI-ARIA navigation
/// pattern), not just a visual highlight — so a screen reader announces which page the
/// user is on. Inactive items omit the attribute entirely.
/// </summary>
public class SidebarAriaCurrentTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SidebarAriaCurrentTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Active_Item_Exposes_AriaCurrent_Page()
    {
        var cut = _ctx.Render<L.SidebarMenuButton>(p => p
            .Add(x => x.IsActive, true)
            .Add(x => x.Href, "/dashboard")
            .AddChildContent("Dashboard"));

        Assert.Equal("page", cut.Find("a").GetAttribute("aria-current"));
    }

    [Fact]
    public void Inactive_Item_Has_No_AriaCurrent()
    {
        var cut = _ctx.Render<L.SidebarMenuButton>(p => p
            .Add(x => x.IsActive, false)
            .Add(x => x.Href, "/settings")
            .AddChildContent("Settings"));

        Assert.False(cut.Find("a").HasAttribute("aria-current"));
    }
}
