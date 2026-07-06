using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.NavigationMenu;

/// <summary>
/// Wave 4 — NavigationMenu gains controlled <c>Value</c>/<c>ValueChanged</c> plus
/// <c>DefaultValue</c>, addressing the open submenu by each item's stable
/// <see cref="L.NavigationMenuItem.Value"/>. The trigger's data-state reflects the
/// open item; controlled parents can veto to roll an optimistic open back.
/// </summary>
public class NavigationMenuControlledValueTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NavigationMenuControlledValueTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Two items ("a","b"), trigger-only. data-state on each trigger reveals which
    // item the menu considers open.
    private static RenderFragment TwoItems() => list =>
    {
        AddItem(list, 0, "a", "Alpha");
        AddItem(list, 10, "b", "Bravo");
    };

    private static void AddItem(RenderTreeBuilder list, int seq, string value, string label)
    {
        list.OpenComponent<L.NavigationMenuItem>(seq);
        list.AddAttribute(seq + 1, "Value", value);
        list.AddAttribute(seq + 2, "ChildContent", (RenderFragment)(item =>
        {
            item.OpenComponent<L.NavigationMenuTrigger>(0);
            item.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, label)));
            item.CloseComponent();
        }));
        list.CloseComponent();
    }

    private IRenderedComponent<L.NavigationMenu> RenderNav(
        Action<ComponentParameterCollectionBuilder<L.NavigationMenu>> configure)
        => _ctx.Render<L.NavigationMenu>(p =>
        {
            configure(p);
            p.Add(m => m.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.NavigationMenuList>(0);
                b.AddAttribute(1, "ChildContent", TwoItems());
                b.CloseComponent();
            }));
        });

    private static string State(IRenderedComponent<L.NavigationMenu> cut, string label)
        => cut.FindAll("button").First(x => x.TextContent.Contains(label)).GetAttribute("data-state")!;

    [Fact]
    public void Value_Seeds_The_Open_Item()
    {
        var cut = RenderNav(p => p.Add(m => m.Value, "b"));
        Assert.Equal("closed", State(cut, "Alpha"));
        Assert.Equal("open", State(cut, "Bravo"));
    }

    [Fact]
    public void DefaultValue_Seeds_The_Open_Item_Uncontrolled()
    {
        var cut = RenderNav(p => p.Add(m => m.DefaultValue, "a"));
        Assert.Equal("open", State(cut, "Alpha"));
        Assert.Equal("closed", State(cut, "Bravo"));
    }

    [Fact]
    public void Controlled_Menu_Ignores_DefaultValue_And_Honors_Null_As_Closed()
    {
        // Round-2 P2: a controlled menu (ValueChanged bound) with Value == null must open
        // NOTHING — DefaultValue is an uncontrolled-only seed. Pre-fix the seed opened
        // DefaultValue ("a") while _lastPushed stayed null, so the parent's null could
        // never force-close it (mistaken for the echo of our own push).
        var cut = RenderNav(p =>
        {
            p.Add(m => m.DefaultValue, "a");
            p.Add(m => m.ValueChanged, EventCallback.Factory.Create<string?>(this, _ => { }));
        });

        Assert.Equal("closed", State(cut, "Alpha"));
        Assert.Equal("closed", State(cut, "Bravo"));
    }

    [Fact]
    public void Clicking_A_Trigger_Emits_ValueChanged_With_The_Item_Value()
    {
        string? emitted = null;
        var seen = false;
        var cut = RenderNav(p => p.Add(m => m.ValueChanged,
            EventCallback.Factory.Create<string?>(this, v => { emitted = v; seen = true; })));

        cut.FindAll("button").First(x => x.TextContent.Contains("Bravo")).Click();

        Assert.True(seen);
        Assert.Equal("b", emitted);
    }

    [Fact]
    public void Item_Value_Change_While_Mounted_Propagates_Through_The_Cascade()
    {
        // Round-3 P2: NavigationMenuItem provided its item context via a FIXED cascade,
        // yet the context's ItemId derives from the mutable Value parameter. When Value
        // changed while the item stayed mounted, the Trigger kept the STALE ItemId and
        // matched it against the menu's ActiveItemId incorrectly. The cascade is now
        // non-fixed (like the parent NavigationMenuContext), so a changed Value flows down.
        RenderFragment OneItem(string itemValue) => list =>
        {
            list.OpenComponent<L.NavigationMenuItem>(0);
            list.AddAttribute(1, "Value", itemValue);
            list.AddAttribute(2, "ChildContent", (RenderFragment)(item =>
            {
                item.OpenComponent<L.NavigationMenuTrigger>(0);
                item.AddAttribute(1, "ChildContent", (RenderFragment)(t => t.AddContent(0, "Alpha")));
                item.CloseComponent();
            }));
            list.CloseComponent();
        };

        void Configure(ComponentParameterCollectionBuilder<L.NavigationMenu> p, string value)
        {
            p.Add(m => m.Value, value);
            p.Add(m => m.ValueChanged, EventCallback.Factory.Create<string?>(this, _ => { }));
            p.Add(m => m.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.NavigationMenuList>(0);
                b.AddAttribute(1, "ChildContent", OneItem(value));
                b.CloseComponent();
            }));
        }

        // Controlled: menu Value == item Value ("a") → the item is open.
        var cut = _ctx.Render<L.NavigationMenu>(p => Configure(p, "a"));
        Assert.Equal("open", State(cut, "Alpha"));

        // Change BOTH the menu's controlled Value and the item's Value to "b" in place.
        cut.Render(p => Configure(p, "b"));

        // The trigger must still read open: its ItemContext.ItemId followed "a"→"b".
        // Pre-fix (fixed cascade) it kept "a", so ActiveItemId "b" != "a" → wrongly closed.
        Assert.Equal("open", State(cut, "Alpha"));
    }

    [Fact]
    public void Controlled_Item_Reopens_After_Being_Removed_And_Readded()
    {
        // Regression for round-4 P2: active item torn down → NotifyItemRemoved clears
        // _activeItemId but left _lastPushed == "a". Re-adding the item while Value
        // is still "a" hit the echo guard (Value == _lastPushed → no re-adopt) and
        // rendered the item closed. Fix: sentinel _lastPushed on removal.
        RenderFragment ItemsWithFlag(bool includeA) => list =>
        {
            if (includeA) AddItem(list, 0, "a", "Alpha");
            AddItem(list, 10, "b", "Bravo");
        };

        void Configure(ComponentParameterCollectionBuilder<L.NavigationMenu> p, bool includeA)
        {
            p.Add(m => m.Value, "a");
            p.Add(m => m.ValueChanged, EventCallback.Factory.Create<string?>(this, _ => { }));
            p.Add(m => m.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.NavigationMenuList>(0);
                b.AddAttribute(1, "ChildContent", ItemsWithFlag(includeA));
                b.CloseComponent();
            }));
        }

        // Step 1: item "a" present and open.
        var cut = _ctx.Render<L.NavigationMenu>(p => Configure(p, includeA: true));
        Assert.Equal("open", State(cut, "Alpha"));

        // Step 2: conditionally remove item "a" (simulates conditional rendering).
        cut.Render(p => Configure(p, includeA: false));

        // Step 3: re-add item "a" — parent's Value is still "a".
        cut.Render(p => Configure(p, includeA: true));

        // Must be open again; pre-fix the echo guard blocked re-adoption → "closed".
        Assert.Equal("open", State(cut, "Alpha"));
    }

    [Fact]
    public void Controlled_Parent_Veto_Rolls_The_Optimistic_Open_Back()
    {
        // Parent is controlled but pins Value="a" and ignores the change — a veto.
        var cut = RenderNav(p =>
        {
            p.Add(m => m.Value, "a");
            p.Add(m => m.ValueChanged, EventCallback.Factory.Create<string?>(this, _ => { }));
        });
        Assert.Equal("open", State(cut, "Alpha"));

        // Click Bravo: optimistically opens b + emits, then the parent re-renders
        // still pinned to "a". OnParametersSet sees Value("a") != lastPushed("b")
        // and rolls the open item back to "a".
        cut.FindAll("button").First(x => x.TextContent.Contains("Bravo")).Click();
        cut.Render(p =>
        {
            p.Add(m => m.Value, "a");
            p.Add(m => m.ValueChanged, EventCallback.Factory.Create<string?>(this, _ => { }));
            p.Add(m => m.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.NavigationMenuList>(0);
                b.AddAttribute(1, "ChildContent", TwoItems());
                b.CloseComponent();
            }));
        });

        Assert.Equal("open", State(cut, "Alpha"));
        Assert.Equal("closed", State(cut, "Bravo"));
    }
}
