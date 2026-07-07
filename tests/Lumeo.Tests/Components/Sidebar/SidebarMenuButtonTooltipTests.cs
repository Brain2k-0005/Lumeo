using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// SidebarMenuButton's <c>Tooltip</c> parameter must render the Lumeo
/// <see cref="L.Tooltip"/> (styled, accessible) — NOT the native browser <c>title</c>
/// attribute, and only while the rail is collapsed (icon-only). Regression guard for
/// the "Tooltip param loads the default HTML tooltip" bug.
/// </summary>
public class SidebarMenuButtonTooltipTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SidebarMenuButtonTooltipTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderButton(bool collapsed, string? tooltip)
        => _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarProvider>(0);
            builder.AddAttribute(1, "IsCollapsed", collapsed);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarComponent>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<L.SidebarMenuButton>(0);
                    inner.AddAttribute(1, "Href", "#");
                    if (tooltip is not null) inner.AddAttribute(2, "Tooltip", tooltip);
                    inner.AddAttribute(3, "IconContent", (RenderFragment)(i => i.AddContent(0, "ICN")));
                    inner.AddAttribute(4, "LabelContent", (RenderFragment)(l => l.AddContent(0, "Home")));
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

    [Fact]
    public void Collapsed_With_Tooltip_Wraps_The_Link_In_Lumeo_Tooltip_Not_Native_Title()
    {
        var cut = RenderButton(collapsed: true, tooltip: "Dashboard");

        var link = cut.Find("a");
        // The native HTML title is GONE — the hint is the Lumeo Tooltip now.
        Assert.False(link.HasAttribute("title"));
        // AsChild renders no wrapper, so the link's parent IS the Lumeo tooltip root
        // (the `relative inline-flex` box) — proof the link is the tooltip trigger.
        Assert.Contains("inline-flex", link.ParentElement!.GetAttribute("class") ?? "");
    }

    [Fact]
    public async Task Collapsed_Hovering_Reveals_The_Lumeo_Tooltip_With_The_Text()
    {
        var cut = RenderButton(collapsed: true, tooltip: "Dashboard");

        // Hover the tooltip wrapper (the link's parent). Default ShowDelay is 200ms,
        // so poll until the role=tooltip content mounts.
        cut.Find("a").ParentElement!.TriggerEvent("onmouseenter", new MouseEventArgs());

        // No explicit ceiling: the 200ms reveal timer can starve under CI parallel
        // load, and a tight cap is exactly the flake class the 2026-07-04 deflake
        // removed — rely on the module-wide 10s default instead.
        await Task.Run(() => cut.WaitForAssertion(
            () => Assert.Contains("Dashboard", cut.Find("[role='tooltip']").TextContent)));
    }

    [Fact]
    public void Expanded_With_Tooltip_Shows_No_Tooltip_Content_And_No_Native_Title()
    {
        var cut = RenderButton(collapsed: false, tooltip: "Dashboard");

        // Expanded rail already shows the label — the tooltip content is not mounted
        // even on hover, and there is no native title either.
        cut.Find("a").ParentElement!.TriggerEvent("onmouseenter", new MouseEventArgs());
        Assert.Empty(cut.FindAll("[role='tooltip']"));
        Assert.False(cut.Find("a").HasAttribute("title"));
    }
}
