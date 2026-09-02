using Microsoft.AspNetCore.Components;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// The sidebar members shadcn's sidebar-07 block needs and Lumeo lacked (field report 3.1):
/// Inset, Rail, the MenuSub family, MenuAction, MenuBadge, GroupContent, GroupAction, Input,
/// MenuSkeleton, and the three sizes of MenuButton. Geometry measured live on
/// ui.shadcn.com/view/new-york-v4/sidebar-07 on 2026-09-02.
/// </summary>
public class SidebarShadcnMembersTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SidebarShadcnMembersTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.SidebarProvider> RenderInProvider(RenderFragment body, bool collapsed = false) =>
        _ctx.Render<L.SidebarProvider>(p => p
            .Add(s => s.IsCollapsed, collapsed)
            .Add(s => s.Variant, L.SidebarProvider.SidebarVariant.Icon)
            .AddChildContent(body));

    [Fact]
    public void MenuButton_Without_Href_Is_A_Button_With_The_Data_Contract()
    {
        var cut = _ctx.Render<L.SidebarMenuButton>(p => p.AddChildContent("Playground"));
        var btn = cut.Find("button");

        Assert.Equal("sidebar-menu-button", btn.GetAttribute("data-slot"));
        Assert.Equal("default", btn.GetAttribute("data-size"));
        Assert.Equal("false", btn.GetAttribute("data-active"));
        Assert.Contains("peer/menu-button", btn.ClassList);
        Assert.Contains("h-8", btn.ClassList);
        Assert.Contains("text-sm", btn.ClassList);
    }

    [Fact]
    public void MenuButton_With_Href_Is_A_Link()
    {
        var cut = _ctx.Render<L.SidebarMenuButton>(p => p.Add(b => b.Href, "/x").Add(b => b.IsActive, true).AddChildContent("Home"));
        var a = cut.Find("a");

        Assert.Equal("/x", a.GetAttribute("href"));
        Assert.Equal("true", a.GetAttribute("data-active"));
        Assert.Equal("page", a.GetAttribute("aria-current"));
    }

    [Theory]
    [InlineData(L.SidebarMenuButton.ButtonSize.Sm, "sm", "h-7", "text-xs")]
    [InlineData(L.SidebarMenuButton.ButtonSize.Lg, "lg", "h-12", "text-sm")]
    public void MenuButton_Sizes_Match_Shadcn(L.SidebarMenuButton.ButtonSize size, string attr, string h, string text)
    {
        var cut = _ctx.Render<L.SidebarMenuButton>(p => p.Add(b => b.Size, size).AddChildContent("x"));
        var btn = cut.Find("button");

        Assert.Equal(attr, btn.GetAttribute("data-size"));
        Assert.Contains(h, btn.ClassList);
        Assert.Contains(text, btn.ClassList);
    }

    [Fact]
    public void MenuButton_Outline_Variant_Draws_Its_Ring()
    {
        var cut = _ctx.Render<L.SidebarMenuButton>(p => p.Add(b => b.Variant, L.SidebarMenuButton.ButtonVariant.Outline).AddChildContent("x"));

        Assert.Contains("bg-background", cut.Find("button").ClassList);
    }

    [Fact]
    public void MenuSub_Is_An_Indented_List_With_A_Guide_Line()
    {
        var cut = _ctx.Render<L.SidebarMenuSub>(p => p.AddChildContent("x"));
        var ul = cut.Find("ul");

        Assert.Equal("sidebar-menu-sub", ul.GetAttribute("data-slot"));
        foreach (var c in new[] { "mx-3.5", "px-2.5", "py-0.5", "gap-1", "border-l", "group-data-[collapsible=icon]:hidden" })
            Assert.Contains(c, ul.ClassList);
    }

    [Fact]
    public void MenuSubButton_Is_28px_And_Links_When_Given_An_Href()
    {
        var cut = _ctx.Render<L.SidebarMenuSubButton>(p => p.Add(b => b.Href, "/h").AddChildContent("History"));
        var a = cut.Find("a");

        Assert.Contains("h-7", a.ClassList);
        Assert.Contains("px-2", a.ClassList);
        Assert.Contains("text-sm", a.ClassList);
        Assert.Equal("md", a.GetAttribute("data-size"));
    }

    [Fact]
    public void MenuSubButton_Sm_Is_12px_And_Active_Is_Highlighted()
    {
        var cut = _ctx.Render<L.SidebarMenuSubButton>(p => p
            .Add(b => b.Size, L.SidebarMenuSubButton.SubSize.Sm)
            .Add(b => b.IsActive, true)
            .AddChildContent("x"));
        var btn = cut.Find("button");

        Assert.Equal("sm", btn.GetAttribute("data-size"));
        Assert.Contains("text-xs", btn.ClassList);
        Assert.Contains("bg-sidebar-accent", btn.ClassList);
    }

    [Fact]
    public void MenuAction_Sits_On_The_Right_Edge_And_Can_Hide_Until_Hover()
    {
        var plain = _ctx.Render<L.SidebarMenuAction>(p => p.AddChildContent("...")).Find("button");
        var hover = _ctx.Render<L.SidebarMenuAction>(p => p.Add(a => a.ShowOnHover, true).AddChildContent("...")).Find("button");

        Assert.Contains("absolute", plain.ClassList);
        Assert.Contains("right-1", plain.ClassList);
        Assert.Contains("w-5", plain.ClassList);
        Assert.DoesNotContain("md:opacity-0", plain.ClassList);
        Assert.Contains("md:opacity-0", hover.ClassList);
        Assert.Contains("group-hover/menu-item:opacity-100", hover.ClassList);
    }

    [Fact]
    public void MenuBadge_Is_A_Non_Interactive_Count()
    {
        var div = _ctx.Render<L.SidebarMenuBadge>(p => p.AddChildContent("24")).Find("div");

        Assert.Contains("pointer-events-none", div.ClassList);
        Assert.Contains("tabular-nums", div.ClassList);
        Assert.Contains("h-5", div.ClassList);
    }

    [Fact]
    public void GroupContent_And_GroupAction_Render_Their_Slots()
    {
        Assert.Equal("sidebar-group-content", _ctx.Render<L.SidebarGroupContent>(p => p.AddChildContent("x")).Find("div").GetAttribute("data-slot"));
        var action = _ctx.Render<L.SidebarGroupAction>(p => p.AddChildContent("+")).Find("button");
        Assert.Equal("sidebar-group-action", action.GetAttribute("data-slot"));
        Assert.Contains("top-3.5", action.ClassList);
    }

    [Fact]
    public void Group_And_MenuItem_Are_Positioning_Anchors_For_Actions()
    {
        Assert.Contains("relative", _ctx.Render<L.SidebarGroup>(p => p.AddChildContent("x")).Find("div").ClassList);
        var li = _ctx.Render<L.SidebarMenuItem>(p => p.AddChildContent("x")).Find("li");
        Assert.Contains("relative", li.ClassList);
        Assert.Contains("group/menu-item", li.ClassList);
    }

    [Fact]
    public void Inset_Is_The_Main_Column()
    {
        var main = _ctx.Render<L.SidebarInset>(p => p.AddChildContent("page")).Find("main");

        Assert.Equal("sidebar-inset", main.GetAttribute("data-slot"));
        Assert.Contains("flex-1", main.ClassList);
        Assert.Contains("min-w-0", main.ClassList);
    }

    [Fact]
    public void Input_Wraps_The_Library_Input_At_Sidebar_Height()
    {
        var input = _ctx.Render<L.SidebarInput>(p => p.AddUnmatched("placeholder", "Search")).Find("input");

        Assert.Equal("Search", input.GetAttribute("placeholder"));
        Assert.Contains("h-8", input.ClassList);
        Assert.Contains("shadow-none", input.ClassList);
    }

    [Fact]
    public void MenuSkeleton_Optionally_Shows_An_Icon_Box()
    {
        Assert.Empty(_ctx.Render<L.SidebarMenuSkeleton>().FindAll("[data-sidebar=menu-skeleton-icon]"));
        Assert.Single(_ctx.Render<L.SidebarMenuSkeleton>(p => p.Add(s => s.ShowIcon, true)).FindAll("[data-sidebar=menu-skeleton-icon]"));
    }

    [Fact]
    public void Rail_Renders_Beside_The_Aside_And_Toggles_The_Sidebar()
    {
        var cut = RenderInProvider(b =>
        {
            b.OpenComponent<L.SidebarComponent>(0);
            b.AddAttribute(1, "Rail", true);
            b.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "nav")));
            b.CloseComponent();
        });

        var rail = cut.Find("[data-slot=sidebar-rail]");
        Assert.Equal("Toggle Sidebar", rail.GetAttribute("aria-label"));
        // Expanded on the left: drag west to collapse (shadcn's cursor semantics).
        Assert.Contains("cursor-w-resize", rail.ClassList);

        rail.Click();

        cut.WaitForAssertion(() => Assert.Equal("collapsed", cut.Find("aside").GetAttribute("data-state")));
        Assert.Contains("cursor-e-resize", cut.Find("[data-slot=sidebar-rail]").ClassList);
    }

    [Fact]
    public void Rail_Is_Absent_Without_Opt_In()
    {
        var cut = RenderInProvider(b =>
        {
            b.OpenComponent<L.SidebarComponent>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(c => c.AddContent(0, "nav")));
            b.CloseComponent();
        });

        Assert.Empty(cut.FindAll("[data-slot=sidebar-rail]"));
    }
}
