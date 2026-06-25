using Bunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Components.BottomNav;

/// <summary>
/// #190 (edge-data) — RecomputeActive must treat an Href that strips down to an
/// empty/whitespace route path as a NON-route target. A fragment-only Href
/// ("#section") strips to "" and, before the guard, normalized to the root "/"
/// target so it falsely lit up as active on the root route. A whitespace Href
/// ("  ") stays non-empty, skipped the empty guard, and built a bogus "/  "
/// target. Both must now resolve to inactive without throwing.
/// </summary>
public class BottomNavItemFragmentHrefTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BottomNavItemFragmentHrefTests()
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
    public void Fragment_Only_Href_Not_Active_On_Root()
    {
        // On the root route, a fragment-only Href used to strip to "" -> target "/"
        // -> falsely matched root. It must NOT be active.
        NavigateTo("/");

        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Href, "#section")
            .Add(i => i.Label, "Jump"));

        Assert.Null(cut.Find("a").GetAttribute("aria-current"));
    }

    [Fact]
    public void Whitespace_Href_Not_Active_And_Does_Not_Throw()
    {
        NavigateTo("/");

        var ex = Record.Exception(() =>
        {
            var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
                .Add(i => i.Href, "  ")
                .Add(i => i.Label, "Blank"));

            // Whitespace Href is a non-route target: never active.
            Assert.Null(cut.Find("a").GetAttribute("aria-current"));
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Fragment_Only_Href_Not_Active_On_Child_Route()
    {
        NavigateTo("/orders");

        var cut = _ctx.Render<Lumeo.BottomNavItem>(p => p
            .Add(i => i.Href, "#top")
            .Add(i => i.Label, "Top"));

        Assert.Null(cut.Find("a").GetAttribute("aria-current"));
    }
}
