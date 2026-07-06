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
