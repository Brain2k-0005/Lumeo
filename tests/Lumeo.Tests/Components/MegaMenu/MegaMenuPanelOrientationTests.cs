using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.MegaMenu;

/// <summary>
/// #550 — in a vertical menubar the panel was still anchored to the &lt;nav&gt; at
/// top-full, so it opened below the whole column over the other items instead of
/// beside its own trigger. Vertical must anchor to the trigger's &lt;li&gt; at its
/// inline-end edge; horizontal keeps the drop-below placement.
/// </summary>
public class MegaMenuPanelOrientationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MegaMenuPanelOrientationTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<MegaMenuOrientationHost> RenderOpen(L.Orientation o)
    {
        var cut = _ctx.Render<MegaMenuOrientationHost>(p => p.Add(h => h.Orientation, o));
        cut.Find("button[role=menuitem]").Click();
        return cut;
    }

    [Fact]
    public void Horizontal_panel_drops_below_the_menubar()
    {
        var cut = RenderOpen(L.Orientation.Horizontal);
        var cls = cut.Find("[data-slot=mega-menu-panel]").ClassName!;
        Assert.Contains("top-full", cls);
        Assert.Contains("end-0", cls);
        Assert.DoesNotContain("start-full", cls);
        Assert.DoesNotContain("relative", cut.Find("li[data-slot=mega-menu-item]").ClassName ?? "");
    }

    [Fact]
    public void Vertical_panel_sits_at_the_inline_end_of_its_trigger()
    {
        var cut = RenderOpen(L.Orientation.Vertical);
        var cls = cut.Find("[data-slot=mega-menu-panel]").ClassName!;
        Assert.Contains("top-0", cls);
        Assert.Contains("start-full", cls);
        Assert.DoesNotContain("top-full", cls);
        Assert.DoesNotContain("end-0", cls);
        // The <li> is the containing block the panel is anchored to.
        Assert.Contains("relative", cut.Find("li[data-slot=mega-menu-item]").ClassName!);
    }

    [Fact]
    public void Switching_orientation_while_open_re_places_the_panel()
    {
        var cut = RenderOpen(L.Orientation.Horizontal);
        Assert.Contains("top-full", cut.Find("[data-slot=mega-menu-panel]").ClassName!);

        cut.Render(p => p.Add(h => h.Orientation, L.Orientation.Vertical));

        var cls = cut.Find("[data-slot=mega-menu-panel]").ClassName!;
        Assert.Contains("start-full", cls);
        Assert.DoesNotContain("top-full", cls);
    }
}
