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
}
