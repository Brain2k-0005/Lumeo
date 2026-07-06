using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.NavigationMenu;

/// <summary>
/// Wave 4 (audit "potential HIGH" verdict). NavigationMenuViewport previously sized
/// with h-[var(--radix-navigation-menu-viewport-height)] — a CSS var nothing in
/// this library ever set (Radix sets it from JS content measurement for a shared
/// portaled viewport, which this per-panel architecture does not use). The height
/// only resolved to `auto` via the invalid-var fallback (verified in-browser: it
/// did NOT collapse to 0). The dead var is dropped; the panel now sizes to its
/// content explicitly (h-auto). This locks that in so the fragile dependency can't
/// return.
/// </summary>
public class NavigationMenuViewportHeightTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NavigationMenuViewportHeightTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.NavigationMenu> RenderNavWithViewport()
        => _ctx.Render<L.NavigationMenu>(p =>
        {
            // Value seeds an open item so the viewport (gated on ActiveItemId != null) renders.
            p.Add(m => m.Value, "a");
            p.Add(m => m.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.NavigationMenuList>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(list =>
                {
                    list.OpenComponent<L.NavigationMenuItem>(0);
                    list.AddAttribute(1, "Value", "a");
                    list.AddAttribute(2, "ChildContent", (RenderFragment)(item =>
                    {
                        item.OpenComponent<L.NavigationMenuTrigger>(0);
                        item.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Alpha")));
                        item.CloseComponent();
                    }));
                    list.CloseComponent();
                }));
                b.CloseComponent();

                b.OpenComponent<L.NavigationMenuViewport>(2);
                b.CloseComponent();
            }));
        });

    [Fact]
    public void Viewport_Inner_Does_Not_Reference_The_Unpopulated_Var()
    {
        var cut = RenderNavWithViewport();
        var inner = cut.FindAll("div").First(d => (d.GetAttribute("class") ?? "").Contains("origin-top-center"));
        var cls = inner.GetAttribute("class") ?? "";
        Assert.DoesNotContain("--radix-navigation-menu-viewport-height", cls);
        Assert.Contains("h-auto", cls);
    }
}
