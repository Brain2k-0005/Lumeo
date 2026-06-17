using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.MegaMenu;

/// <summary>
/// #226 — MegaMenu gains WAI-ARIA menubar arrow-key navigation between the
/// top-level triggers, a roving tabindex (single tab stop), and aria-orientation.
/// Focus moves are routed through IComponentInteropService.FocusElement, recorded
/// by TrackingInteropService.
/// </summary>
public class MegaMenuKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public MegaMenuKeyboardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<IComponent> RenderMenu(L.Orientation orientation = L.Orientation.Horizontal)
    {
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<L.MegaMenu>(0);
            builder.AddAttribute(1, "Orientation", orientation);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<L.MegaMenuItem>(0);
                b.AddAttribute(1, "Label", "Products");
                b.AddAttribute(2, "ChildContent", (RenderFragment)(p => p.AddContent(0, "panel-a")));
                b.CloseComponent();

                b.OpenComponent<L.MegaMenuItem>(3);
                b.AddAttribute(4, "Label", "Solutions");
                b.AddAttribute(5, "ChildContent", (RenderFragment)(p => p.AddContent(0, "panel-b")));
                b.CloseComponent();

                b.OpenComponent<L.MegaMenuItem>(6);
                b.AddAttribute(7, "Label", "Pricing");
                b.AddAttribute(8, "ChildContent", (RenderFragment)(p => p.AddContent(0, "panel-c")));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void Menubar_Has_Aria_Orientation()
    {
        var cut = RenderMenu();
        var ul = cut.Find("ul[role='menubar']");
        Assert.Equal("horizontal", ul.GetAttribute("aria-orientation"));

        var vcut = RenderMenu(L.Orientation.Vertical);
        Assert.Equal("vertical", vcut.Find("ul[role='menubar']").GetAttribute("aria-orientation"));
    }

    [Fact]
    public void Only_First_Trigger_Is_In_Tab_Order_Initially()
    {
        var cut = RenderMenu();
        var triggers = cut.FindAll("button[role='menuitem']");
        Assert.Equal(3, triggers.Count);
        // Roving tabindex: first trigger tabbable (0), the rest removed from the
        // tab order (-1).
        Assert.Equal("0", triggers[0].GetAttribute("tabindex"));
        Assert.Equal("-1", triggers[1].GetAttribute("tabindex"));
        Assert.Equal("-1", triggers[2].GetAttribute("tabindex"));
    }

    [Fact]
    public void ArrowRight_Focuses_Next_Trigger()
    {
        var cut = RenderMenu();
        var firstLi = cut.FindAll("li")[0];
        firstLi.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        // FocusElement is called with the second trigger's id.
        var focused = Assert.Single(_interop.FocusElementCalls);
        Assert.StartsWith("megamenu-trigger-", focused);
        // And the roving tab stop moved to it.
        var triggers = cut.FindAll("button[role='menuitem']");
        Assert.Equal("-1", triggers[0].GetAttribute("tabindex"));
        Assert.Equal("0", triggers[1].GetAttribute("tabindex"));
    }

    [Fact]
    public void ArrowLeft_From_First_Wraps_To_Last()
    {
        var cut = RenderMenu();
        cut.FindAll("li")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.Single(_interop.FocusElementCalls);
        var triggers = cut.FindAll("button[role='menuitem']");
        Assert.Equal("0", triggers[2].GetAttribute("tabindex")); // last is now the tab stop
    }

    [Fact]
    public void End_Focuses_Last_Home_Focuses_First()
    {
        var cut = RenderMenu();
        cut.FindAll("li")[0].KeyDown(new KeyboardEventArgs { Key = "End" });
        var triggers = cut.FindAll("button[role='menuitem']");
        Assert.Equal("0", triggers[2].GetAttribute("tabindex"));

        cut.FindAll("li")[2].KeyDown(new KeyboardEventArgs { Key = "Home" });
        triggers = cut.FindAll("button[role='menuitem']");
        Assert.Equal("0", triggers[0].GetAttribute("tabindex"));
    }

    [Fact]
    public void Vertical_Orientation_Uses_ArrowDown_For_Next()
    {
        var cut = RenderMenu(L.Orientation.Vertical);
        cut.FindAll("li")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Single(_interop.FocusElementCalls);
        var triggers = cut.FindAll("button[role='menuitem']");
        Assert.Equal("0", triggers[1].GetAttribute("tabindex"));
    }

    [Fact]
    public void Horizontal_Ignores_ArrowDown_For_Roving()
    {
        var cut = RenderMenu(L.Orientation.Horizontal);
        // ArrowDown in horizontal orientation must not move roving focus between
        // top-level triggers (it would open/navigate a panel instead).
        cut.FindAll("li")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Empty(_interop.FocusElementCalls);
    }
}
