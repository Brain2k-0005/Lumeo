using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// DocFlow T1: SidebarMenuButton's icon-mode size/padding used to ship as
/// <c>group-data-[collapsible=icon]:size-8!</c> / <c>:p-2!</c> — an <c>!important</c> that beat
/// any consumer override on the SAME specificity regardless of Class being merged in last
/// (TailwindMerge only lets a plain token evict an important one by also being important).
/// A larger custom brand mark's <c>Class="group-data-[collapsible=icon]:px-0"</c> then lost,
/// sitting 8px off the icon column. Fixed by dropping the internal `!` (plain tokens already
/// resolve last-wins in source order, and Class is always last) — default visuals unchanged.
/// </summary>
public class SidebarMenuButtonIconModeOverrideTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SidebarMenuButtonIconModeOverrideTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.SidebarProvider> RenderRail(string? extraClass, L.SidebarMenuButton.ButtonSize size = L.SidebarMenuButton.ButtonSize.Default)
        => _ctx.Render<L.SidebarProvider>(p => p
            .Add(x => x.IsCollapsed, true)
            .AddChildContent<L.SidebarComponent>(sb => sb
                .AddChildContent<L.SidebarMenuButton>(btn =>
                {
                    btn.Add(x => x.Href, "#");
                    btn.Add(x => x.Size, size);
                    btn.Add(x => x.IconContent, (Microsoft.AspNetCore.Components.RenderFragment)(i => i.AddContent(0, "ICN")));
                    btn.Add(x => x.LabelContent, (Microsoft.AspNetCore.Components.RenderFragment)(l => l.AddContent(0, "Home")));
                    if (extraClass is not null) btn.Add(x => x.Class, extraClass);
                })));

    [Fact]
    public void Default_Icon_Mode_Still_Renders_Size_8_And_P_2()
    {
        var cut = RenderRail(extraClass: null);
        var cls = cut.Find("a").GetAttribute("class") ?? "";
        Assert.Contains("group-data-[collapsible=icon]:size-8", cls);
        Assert.Contains("group-data-[collapsible=icon]:p-2", cls);
        Assert.DoesNotContain("size-8!", cls);
        Assert.DoesNotContain("p-2!", cls);
    }

    [Fact]
    public void Lg_Size_Still_Collapses_To_P_0_In_Icon_Mode_Without_Important()
    {
        var cut = RenderRail(extraClass: null, size: L.SidebarMenuButton.ButtonSize.Lg);
        var cls = cut.Find("a").GetAttribute("class") ?? "";
        Assert.Contains("group-data-[collapsible=icon]:p-0", cls);
        // p-2 (the base icon-rail padding) must have been superseded, not merely appended.
        Assert.DoesNotContain("group-data-[collapsible=icon]:p-2 ", cls + " ");
    }

    [Fact]
    public void Consumer_Class_Overrides_Icon_Mode_Padding_Without_Needing_Important()
    {
        // Without `!important` fighting it, the consumer's plain px-0 reaches the class
        // attribute (previously: p-2! always won the CSS specificity fight regardless of
        // what Class contained, forcing the consumer into their own `!` and a raw
        // stylesheet-order battle Lumeo doesn't control). Tailwind's own compiled utility
        // order (px-* always emitted after p-* within the same layer) is what then makes
        // px-0 win the horizontal padding — this asserts the class Lumeo now emits, not
        // Tailwind's compiled CSS order, which is exercised by the visual check.
        var cut = RenderRail(extraClass: "group-data-[collapsible=icon]:px-0");
        var cls = cut.Find("a").GetAttribute("class") ?? "";
        Assert.Contains("group-data-[collapsible=icon]:px-0", cls);
        Assert.DoesNotContain("!", cls);
    }
}
