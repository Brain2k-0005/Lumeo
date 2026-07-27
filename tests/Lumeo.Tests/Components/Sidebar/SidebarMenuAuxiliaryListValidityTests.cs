using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// Regression tests for a reported bug: SidebarGroupLabel and SidebarSeparator
/// both render a &lt;div&gt; root, which is invalid directly inside SidebarMenu's
/// &lt;ul&gt; (a &lt;ul&gt; may only directly contain &lt;li&gt; elements) — consumers had
/// to hand-wrap either component in an &lt;li role="presentation"&gt; themselves to
/// use them as a sub-section break within a single menu's item list.
///
/// Fix: SidebarMenu cascades a "LumeoSidebarInsideMenu" marker (a fixed
/// CascadingValue, since it's structural rather than reactive state);
/// SidebarSeparator/SidebarGroupLabel auto-detect it and wrap their existing
/// &lt;div&gt; root in &lt;li role="presentation"&gt; when present, rendering their
/// plain &lt;div&gt; unchanged everywhere else — in particular the library's own
/// documented, RECOMMENDED placement of SidebarGroupLabel as a sibling BEFORE
/// SidebarMenu (both children of SidebarGroup), and SidebarSeparator between
/// two SidebarGroups.
/// </summary>
public class SidebarMenuAuxiliaryListValidityTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SidebarMenuAuxiliaryListValidityTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ── SidebarSeparator ──────────────────────────────────────────────────

    [Fact]
    public void Separator_Standalone_Renders_A_Plain_Div_Root()
    {
        var cut = _ctx.Render<L.SidebarSeparator>();

        Assert.Equal("div", cut.Find("[role='none']").TagName.ToLowerInvariant());
        Assert.Empty(cut.FindAll("li"));
    }

    [Fact]
    public void Separator_Inside_SidebarMenu_Wraps_In_Li_Role_Presentation()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarSeparator>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var li = cut.Find("li");
        Assert.Equal("presentation", li.GetAttribute("role"));
        // The separator's own div still renders, nested inside the <li>.
        var div = li.QuerySelector("div[role='none']");
        Assert.NotNull(div);
    }

    [Fact]
    public void Separator_Between_SidebarGroups_Outside_A_Menu_Stays_A_Plain_Div()
    {
        // The library's own documented pattern: a separator as a direct sibling
        // between two SidebarGroups, never inside a SidebarMenu.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarGroup>(0);
            builder.CloseComponent();
            builder.OpenComponent<L.SidebarSeparator>(1);
            builder.CloseComponent();
            builder.OpenComponent<L.SidebarGroup>(2);
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll("li"));
        Assert.NotNull(cut.Find("[role='none']"));
    }

    // ── SidebarGroupLabel ─────────────────────────────────────────────────

    [Fact]
    public void GroupLabel_Standalone_Renders_A_Plain_Div_Root()
    {
        var cut = _ctx.Render<L.SidebarGroupLabel>(p => p
            .AddChildContent("Platform"));

        Assert.Empty(cut.FindAll("li"));
        Assert.Contains("Platform", cut.Find("div").TextContent);
    }

    [Fact]
    public void GroupLabel_Inside_SidebarMenu_Wraps_In_Li_Role_Presentation()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarGroupLabel>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Section")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var li = cut.Find("li");
        Assert.Equal("presentation", li.GetAttribute("role"));
        Assert.Contains("Section", li.TextContent);
    }

    [Fact]
    public void GroupLabel_Before_SidebarMenu_Inside_SidebarGroup_Stays_A_Plain_Div()
    {
        // The library's own documented, recommended placement: a sibling
        // BEFORE SidebarMenu, both direct children of SidebarGroup.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarGroup>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarGroupLabel>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Platform")));
                b.CloseComponent();
                b.OpenComponent<L.SidebarMenu>(2);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll("li"));
        Assert.Contains("Platform", cut.Find("div.flex.h-8").TextContent);
    }

    // ── #381 Codex P2: the cascade describes ANCESTRY, not DOM parentage — a
    // GroupLabel/Separator nested inside a SidebarMenuItem (already inside an
    // <li>, one level deeper than a direct SidebarMenu child) must NOT also
    // wrap itself in a second, invalid nested <li>. ─────────────────────────

    [Fact]
    public void Separator_Nested_Inside_SidebarMenuItem_Does_Not_Get_A_Second_Nested_Li()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarMenuItem>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                {
                    item.OpenComponent<L.SidebarSeparator>(0);
                    item.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Exactly one <li> (SidebarMenuItem's own) — the separator inside it
        // must render its plain <div>, not a second nested <li>.
        var lis = cut.FindAll("li");
        Assert.Single(lis);
        Assert.Null(lis[0].QuerySelector("li"));
        Assert.NotNull(lis[0].QuerySelector("div[role='none']"));
    }

    [Fact]
    public void GroupLabel_Nested_Inside_SidebarMenuItem_Does_Not_Get_A_Second_Nested_Li()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarMenuItem>(0);
                b.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                {
                    item.OpenComponent<L.SidebarGroupLabel>(0);
                    item.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Nested")));
                    item.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var lis = cut.FindAll("li");
        Assert.Single(lis);
        Assert.Null(lis[0].QuerySelector("li"));
        Assert.Contains("Nested", lis[0].TextContent);
    }

    // ── AsListItem explicit override (escape hatch for shapes the cascade
    // can't auto-detect — e.g. a consumer's own custom wrapper). ────────────

    [Fact]
    public void Separator_AsListItem_True_Forces_The_Li_Wrapper_Outside_A_Menu()
    {
        var cut = _ctx.Render<L.SidebarSeparator>(p => p
            .Add(c => c.AsListItem, true));

        var li = cut.Find("li");
        Assert.Equal("presentation", li.GetAttribute("role"));
    }

    [Fact]
    public void Separator_AsListItem_False_Suppresses_The_Li_Wrapper_Inside_A_Menu()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarSeparator>(0);
                b.AddAttribute(1, "AsListItem", false);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll("li"));
    }

    [Fact]
    public void GroupLabel_AsListItem_True_Forces_The_Li_Wrapper_Outside_A_Menu()
    {
        var cut = _ctx.Render<L.SidebarGroupLabel>(p => p
            .Add(c => c.AsListItem, true)
            .AddChildContent("Forced"));

        var li = cut.Find("li");
        Assert.Equal("presentation", li.GetAttribute("role"));
        Assert.Contains("Forced", li.TextContent);
    }

    // ── #381 round 10 (P2): "Keep consumer layout classes on the menu flex
    // item" — SidebarMenu's <ul> is a flex container, so the <li> (not the
    // inner div) is the actual flex item once wrapped; Class/
    // AdditionalAttributes must land there for order-*/self-*/hidden etc. to
    // have any effect on the menu's own layout. ────────────────────────────

    [Fact]
    public void Separator_Inside_Menu_Class_Lands_On_The_Li_Flex_Item()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarSeparator>(0);
                b.AddAttribute(1, "Class", "order-2");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var li = cut.Find("li");
        Assert.Contains("order-2", li.GetAttribute("class") ?? "");
        // The inner div keeps its own presentational styling but never the
        // consumer's flex-item utility — it isn't the flex item.
        var innerDiv = li.QuerySelector("div[role='none']");
        Assert.NotNull(innerDiv);
        Assert.DoesNotContain("order-2", innerDiv!.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Separator_Inside_Menu_AdditionalAttributes_Land_On_The_Li_Flex_Item()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarSeparator>(0);
                b.AddAttribute(1, "data-testid", "sep-1");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var li = cut.Find("li");
        Assert.Equal("sep-1", li.GetAttribute("data-testid"));
    }

    [Fact]
    public void GroupLabel_Inside_Menu_Class_Lands_On_The_Li_Flex_Item()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarGroupLabel>(0);
                b.AddAttribute(1, "Class", "self-end");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Section")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var li = cut.Find("li");
        Assert.Contains("self-end", li.GetAttribute("class") ?? "");
        // The inner div still renders the content and its own presentational
        // classes, but never the consumer's flex-item utility.
        var innerDiv = li.QuerySelector("div");
        Assert.NotNull(innerDiv);
        Assert.Contains("Section", innerDiv!.TextContent);
        Assert.DoesNotContain("self-end", innerDiv!.GetAttribute("class") ?? "");
    }

    [Fact]
    public void GroupLabel_Inside_Menu_Hidden_Attribute_Removes_The_Li_From_Flex_Layout()
    {
        // Before the fix, `hidden` landed on the inner div (hiding only its
        // content) while the <li> stayed a normal, empty flex item — still
        // contributing gaps on both sides. On the <li> itself, `hidden`
        // removes the box from flex layout entirely.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarGroupLabel>(0);
                b.AddAttribute(1, "hidden", true);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Section")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var li = cut.Find("li");
        Assert.True(li.HasAttribute("hidden"));
    }

    [Fact]
    public void GroupLabel_Outside_Menu_Class_Still_Lands_On_The_Div_Root()
    {
        // Control: the canonical/recommended (unwrapped) placement is
        // untouched — there the div itself is the only element and root.
        var cut = _ctx.Render<L.SidebarGroupLabel>(p => p
            .Add(c => c.Class, "self-end")
            .AddChildContent("Platform"));

        Assert.Empty(cut.FindAll("li"));
        Assert.Contains("self-end", cut.Find("div").GetAttribute("class") ?? "");
    }
}
