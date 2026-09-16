using Bunit;
using Lumeo.Docs.Shared;
using Lumeo.Docs.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace Lumeo.Docs.Tests;

/// <summary>
/// #490: the customizer's Menu Color setting used to reach every SidebarComponent on the
/// page via inline --color-sidebar-* styles on &lt;html&gt; — including docs component
/// demos and the dashboard teaser, not just the real app chrome the setting is meant for.
/// The fix opts these embedded previews out via data-menu-color-isolate (lumeo.css
/// redeclares --color-sidebar-* directly on that element, which always wins over the
/// page-wide data-menu-color override). This guards that ComponentDemo renders the
/// attribute on its preview wrapper. (PatternCard carried the same guard until the
/// Patterns section — and PatternCard itself — were removed; Home/DashboardPattern's
/// own isolation is covered by HomePageTests.)
/// </summary>
public class MenuColorIsolationTests
{
    private static BunitContext NewContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddLumeo();
        ctx.AddDocsServices();
        return ctx;
    }

    [Fact]
    public void ComponentDemo_Preview_Container_Carries_The_Isolate_Attribute()
    {
        using var ctx = NewContext();
        var cut = ctx.Render<ComponentDemo>(p =>
        {
            p.Add(x => x.ChildContent, (RenderFragment)(b => b.AddContent(0, "content")));
        });

        var isolated = cut.FindAll("[data-menu-color-isolate]");
        Assert.NotEmpty(isolated);
    }
}
