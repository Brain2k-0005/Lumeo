using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.BottomNav;

/// <summary>
/// #243 — route matching for the active item must ignore the query string and
/// fragment (NavLinkMatch-style), so /orders?status=open and /docs#install
/// still light up their Href="/orders" / Href="/docs" items.
/// </summary>
public class BottomNavItemActiveMatchTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BottomNavItemActiveMatchTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private void NavigateTo(string relativeUri)
    {
        var nav = _ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(relativeUri);
    }

    [Fact]
    public void Active_When_Path_Matches_With_Query_String()
    {
        NavigateTo("/orders?status=open");

        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Href, "/orders")
            .Add(i => i.Label, "Orders"));

        Assert.Equal("page", cut.Find("a").GetAttribute("aria-current"));
    }

    [Fact]
    public void Active_When_Path_Matches_With_Fragment()
    {
        NavigateTo("/docs#install");

        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Href, "/docs")
            .Add(i => i.Label, "Docs"));

        Assert.Equal("page", cut.Find("a").GetAttribute("aria-current"));
    }

    [Fact]
    public void Active_When_Path_Matches_With_Query_And_Fragment()
    {
        NavigateTo("/orders?status=open#top");

        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Href, "/orders")
            .Add(i => i.Label, "Orders"));

        Assert.Equal("page", cut.Find("a").GetAttribute("aria-current"));
    }

    [Fact]
    public void Not_Active_When_Path_Differs_Despite_Matching_Query()
    {
        NavigateTo("/profile?status=open");

        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Href, "/orders")
            .Add(i => i.Label, "Orders"));

        Assert.Null(cut.Find("a").GetAttribute("aria-current"));
    }

    [Fact]
    public void Root_Href_Active_On_Root_With_Query()
    {
        NavigateTo("/?ref=email");

        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Href, "/")
            .Add(i => i.Label, "Home"));

        Assert.Equal("page", cut.Find("a").GetAttribute("aria-current"));
    }

    [Fact]
    public void Root_Href_Not_Active_On_Child_Route_With_Query()
    {
        NavigateTo("/orders?status=open");

        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Href, "/")
            .Add(i => i.Label, "Home"));

        Assert.Null(cut.Find("a").GetAttribute("aria-current"));
    }

    [Fact]
    public void Updates_Active_On_LocationChanged_To_Query_Url()
    {
        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Href, "/orders")
            .Add(i => i.Label, "Orders"));

        // Initially on "/", not active.
        Assert.Null(cut.Find("a").GetAttribute("aria-current"));

        NavigateTo("/orders?page=2");

        Assert.Equal("page", cut.Find("a").GetAttribute("aria-current"));
    }
}
