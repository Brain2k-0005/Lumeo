using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// shadcn-parity Wave 2 (data-state / data-collapsible / data-side / group on the
/// sidebar element) and the Wave 4 Sidebar item: cmd/ctrl+b as the DEFAULT toggle
/// shortcut. Uses the tracking keyboard service so no real JS/DOM is needed.
/// </summary>
public class SidebarDataStateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingKeyboardShortcutService _shortcuts = new();

    public SidebarDataStateTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IKeyboardShortcutService>(_shortcuts);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.SidebarProvider> Render(
        bool collapsed = false,
        L.SidebarProvider.SidebarVariant variant = L.SidebarProvider.SidebarVariant.Push,
        L.Side side = L.Side.Left,
        string? shortcut = null,
        bool setShortcut = false)
    {
        return _ctx.Render<L.SidebarProvider>(p =>
        {
            p.Add(x => x.IsCollapsed, collapsed);
            p.Add(x => x.Variant, variant);
            if (setShortcut) p.Add(x => x.ToggleShortcut, shortcut);
            p.Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<L.SidebarComponent>(0);
                b.AddAttribute(1, "Side", side);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(c => c.AddContent(0, "Nav")));
                b.CloseComponent();
            }));
        });
    }

    // --- data-* styling hooks on the sidebar element ---

    [Fact]
    public void Aside_Is_A_Group_With_Expanded_State_By_Default()
    {
        var aside = Render().Find("aside");
        Assert.Contains("group", aside.GetAttribute("class")!.Split(' '));
        Assert.Equal("expanded", aside.GetAttribute("data-state"));
        Assert.Equal("", aside.GetAttribute("data-collapsible")); // empty while expanded
    }

    [Fact]
    public void Collapsed_Push_Reports_Offcanvas()
    {
        var aside = Render(collapsed: true, variant: L.SidebarProvider.SidebarVariant.Push).Find("aside");
        Assert.Equal("collapsed", aside.GetAttribute("data-state"));
        Assert.Equal("offcanvas", aside.GetAttribute("data-collapsible"));
    }

    [Fact]
    public void Collapsed_Icon_Reports_Icon()
    {
        var aside = Render(collapsed: true, variant: L.SidebarProvider.SidebarVariant.Icon).Find("aside");
        Assert.Equal("collapsed", aside.GetAttribute("data-state"));
        Assert.Equal("icon", aside.GetAttribute("data-collapsible"));
    }

    [Fact]
    public void DataSide_Reflects_Side()
    {
        Assert.Equal("left", Render(side: L.Side.Left).Find("aside").GetAttribute("data-side"));
        Assert.Equal("right", Render(side: L.Side.Right).Find("aside").GetAttribute("data-side"));
    }

    [Fact]
    public void DataVariant_Reflects_Variant()
    {
        Assert.Equal("icon", Render(variant: L.SidebarProvider.SidebarVariant.Icon).Find("aside").GetAttribute("data-variant"));
    }

    // --- Wave 4: cmd/ctrl+b default toggle shortcut ---

    [Fact]
    public void Registers_Ctrl_B_By_Default()
    {
        Render(); // ToggleShortcut not set → falls back to the default
        Assert.Contains("ctrl+b", _shortcuts.RegisteredCombos);
    }

    [Fact]
    public async Task Default_Shortcut_Toggles_Collapsed()
    {
        var cut = Render(collapsed: false);
        await cut.InvokeAsync(() => _shortcuts.TriggerAsync("ctrl+b"));
        Assert.Equal("collapsed", cut.Find("aside").GetAttribute("data-state"));
    }

    [Fact]
    public void Empty_Shortcut_Disables_The_Default()
    {
        Render(shortcut: "", setShortcut: true);
        Assert.Equal(0, _shortcuts.RegistrationCount);
    }

    [Fact]
    public void Custom_Shortcut_Overrides_The_Default()
    {
        Render(shortcut: "ctrl+k", setShortcut: true);
        Assert.Contains("ctrl+k", _shortcuts.RegisteredCombos);
        Assert.DoesNotContain("ctrl+b", _shortcuts.RegisteredCombos);
    }
}
