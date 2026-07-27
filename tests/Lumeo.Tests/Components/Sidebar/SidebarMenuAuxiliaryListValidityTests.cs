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
    // item", corrected round 11 (P2 regression — "Preserve visual classes on
    // the wrapped content"). Round 10 moved Class/AdditionalAttributes onto
    // the <li> so order-*/self-*/hidden would affect the menu's own layout —
    // but that broke visual utilities (bg-*, h-*, mx-*, text color), which
    // only do anything on the SAME element as the label/separator's own base
    // styling. Round 11's fix: the <li> is `display: contents` (never
    // generates its own box), promoting the inner div to be the ACTUAL flex
    // item — verified empirically (gap/order/align-self all correctly
    // promote through display:contents on Chromium/Firefox/WebKit; see the
    // razor files' own remarks). Class/AdditionalAttributes/hidden go back
    // on the div exclusively, satisfying BOTH directions with the SAME
    // placement — these tests cover both together so a future change can't
    // silently regress either one without breaking a test here. ───────────

    [Fact]
    public void Separator_Inside_Menu_Li_Wrapper_Is_Display_Contents()
    {
        // The structural half of the fix bUnit CAN verify (it has no real
        // flex layout engine to check gap/order/align-self against — that's
        // the empirically-verified browser behavior the razor comments cite).
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
        Assert.Contains("contents", li.GetAttribute("class") ?? "");
        Assert.Equal("presentation", li.GetAttribute("role"));
    }

    [Fact]
    public void Separator_Inside_Menu_Mixed_Class_Lands_Entirely_On_The_Inner_Div()
    {
        // A single Class value mixing a LAYOUT utility (order-2) with VISUAL
        // utilities (mx-0, h-0.5, bg-primary) — the exact real-world shape
        // ("Class=\"mx-0 h-0.5 bg-primary\"") the round-11 regression report
        // named. Both kinds must land together on the div: it's the actual
        // flex item (display:contents promotion) AND the element carrying
        // the separator's own base mx-3/my-2/h-px/bg-border classes that
        // mx-0/h-0.5/bg-primary need to override via Cx.Merge.
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarSeparator>(0);
                b.AddAttribute(1, "Class", "order-2 mx-0 h-0.5 bg-primary");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var li = cut.Find("li");
        var liClass = li.GetAttribute("class") ?? "";
        Assert.DoesNotContain("order-2", liClass);
        Assert.DoesNotContain("bg-primary", liClass);

        var innerDiv = li.QuerySelector("div[role='none']");
        Assert.NotNull(innerDiv);
        var divClass = innerDiv!.GetAttribute("class") ?? "";
        Assert.Contains("order-2", divClass);
        Assert.Contains("mx-0", divClass);
        Assert.Contains("h-0.5", divClass);
        Assert.Contains("bg-primary", divClass);
        // Cx.Merge (tailwind-merge) drops the conflicting defaults these
        // override, rather than shipping both — the base mx-3/h-px must be
        // gone, not just outranked.
        Assert.DoesNotContain("mx-3", divClass);
        Assert.DoesNotContain("h-px", divClass);
    }

    [Fact]
    public void Separator_Inside_Menu_AdditionalAttributes_Land_On_The_Inner_Div()
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
        Assert.False(li.HasAttribute("data-testid"));
        var innerDiv = li.QuerySelector("div[role='none']");
        Assert.Equal("sep-1", innerDiv!.GetAttribute("data-testid"));
    }

    [Fact]
    public void GroupLabel_Inside_Menu_Mixed_Class_Lands_Entirely_On_The_Inner_Div()
    {
        var cut = _ctx.Render(builder =>
        {
            builder.OpenComponent<L.SidebarMenu>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarGroupLabel>(0);
                b.AddAttribute(1, "Class", "self-end text-red-500");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Section")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var li = cut.Find("li");
        var liClass = li.GetAttribute("class") ?? "";
        Assert.DoesNotContain("self-end", liClass);
        Assert.DoesNotContain("text-red-500", liClass);
        Assert.Contains("contents", liClass);

        var innerDiv = li.QuerySelector("div");
        Assert.NotNull(innerDiv);
        Assert.Contains("Section", innerDiv!.TextContent);
        var divClass = innerDiv.GetAttribute("class") ?? "";
        Assert.Contains("self-end", divClass);
        Assert.Contains("text-red-500", divClass);
        // The base text-muted-foreground must not linger alongside an
        // explicit override (Cx.Merge dedup, same check as the separator).
        Assert.DoesNotContain("text-muted-foreground", divClass);
    }

    [Fact]
    public void GroupLabel_Inside_Menu_Hidden_Attribute_Lands_On_The_Inner_Div()
    {
        // Round 10 put `hidden` on the <li> so the (then-real) flex item
        // would be removed from layout entirely. Round 11 makes the DIV the
        // real flex item instead (display:contents on the li), so `hidden`
        // needs to be there now for the same removal-from-layout effect —
        // and it's also just where AdditionalAttributes belong per the
        // AdditionalAttributes tests above.
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
        Assert.False(li.HasAttribute("hidden"));
        var innerDiv = li.QuerySelector("div");
        Assert.True(innerDiv!.HasAttribute("hidden"));
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
