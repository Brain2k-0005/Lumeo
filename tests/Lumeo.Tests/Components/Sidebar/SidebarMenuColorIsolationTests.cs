using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// #490 owner report: the customizer's Menu Color setting used to write hard-coded zinc
/// values as INLINE --color-sidebar-* styles on &lt;html&gt;, which reached every
/// SidebarComponent on the page — including docs demos and the dashboard teaser — not
/// just the app chrome it was meant for. The fix moved Menu Color to a data-menu-color
/// attribute (resolved in lumeo.css against the active theme's own sidebar colors) and
/// gave SidebarProvider an opt-out: IsolateMenuColor="true" renders
/// data-menu-color-isolate on the root wrapper so an embedded preview always follows the
/// active theme's own light/dark sidebar instead of the page-wide setting.
/// </summary>
public class SidebarMenuColorIsolationTests
{
    private static IRenderedComponent<L.SidebarProvider> Render(bool? isolate = null)
    {
        var ctx = new BunitContext();
        ctx.AddLumeoServices();
        return ctx.Render<L.SidebarProvider>(p =>
        {
            if (isolate.HasValue) p.Add(x => x.IsolateMenuColor, isolate.Value);
            p.Add(x => x.ChildContent, (RenderFragment)(b => b.AddContent(0, "content")));
        });
    }

    [Fact]
    public void IsolateMenuColor_False_By_Default_Omits_The_Attribute()
    {
        var wrapper = Render().Find("[data-slot='sidebar-wrapper']");
        Assert.False(wrapper.HasAttribute("data-menu-color-isolate"));
    }

    [Fact]
    public void IsolateMenuColor_True_Renders_The_Attribute()
    {
        var wrapper = Render(isolate: true).Find("[data-slot='sidebar-wrapper']");
        Assert.True(wrapper.HasAttribute("data-menu-color-isolate"));
    }

    [Fact]
    public void IsolateMenuColor_Explicit_False_Omits_The_Attribute()
    {
        var wrapper = Render(isolate: false).Find("[data-slot='sidebar-wrapper']");
        Assert.False(wrapper.HasAttribute("data-menu-color-isolate"));
    }
}
