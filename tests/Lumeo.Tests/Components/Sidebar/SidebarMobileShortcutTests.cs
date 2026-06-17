using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// #241 — mobile off-canvas sheet (responsive) and a keyboard shortcut to
/// toggle the sidebar.
/// </summary>
public class SidebarMobileShortcutTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingKeyboardShortcutService _shortcuts = new();

    public SidebarMobileShortcutTests()
    {
        _ctx.AddLumeoServices();
        // Recording keyboard service so we can assert the registration and drive
        // its handler without a real DOM keydown.
        _ctx.Services.AddSingleton<IKeyboardShortcutService>(_shortcuts);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private ResponsiveService Responsive => _ctx.Services.GetRequiredService<ResponsiveService>();

    private void SetViewport(double width) => Responsive.OnViewportChange(width, 800);

    // --- Keyboard shortcut ---

    [Fact]
    public void Registers_Toggle_Shortcut_When_Provided()
    {
        _ctx.Render<SidebarHost>(p => p.Add(h => h.ToggleShortcut, "ctrl+b"));

        Assert.Contains("ctrl+b", _shortcuts.RegisteredCombos);
    }

    [Fact]
    public void No_Shortcut_Registered_When_Not_Provided()
    {
        _ctx.Render<SidebarHost>();

        Assert.Equal(0, _shortcuts.RegistrationCount);
    }

    [Fact]
    public async Task Shortcut_Toggles_Collapsed_State()
    {
        var cut = _ctx.Render<SidebarHost>(p => p
            .Add(h => h.Collapsed, false)
            .Add(h => h.ToggleShortcut, "ctrl+b"));

        await cut.InvokeAsync(() => _shortcuts.TriggerAsync("ctrl+b"));

        Assert.True(cut.Instance.LastCollapsed);
    }

    // --- Mobile sheet ---

    [Fact]
    public void MobileSheet_Renders_Scrim_On_Mobile_When_Open()
    {
        SetViewport(400); // < md -> mobile
        var cut = _ctx.Render<SidebarHost>(p => p
            .Add(h => h.MobileSheet, true)
            .Add(h => h.Collapsed, false));

        // The off-canvas sheet gets a dismiss scrim and an absolutely positioned
        // panel (translate-x-0 = open).
        Assert.Contains("bg-black/50", cut.Markup);
        var aside = cut.Find("aside");
        Assert.Contains("absolute", aside.GetAttribute("class"));
        Assert.Contains("translate-x-0", aside.GetAttribute("class"));
    }

    [Fact]
    public void MobileSheet_Collapsed_Slides_Panel_Off_Canvas()
    {
        SetViewport(400);
        var cut = _ctx.Render<SidebarHost>(p => p
            .Add(h => h.MobileSheet, true)
            .Add(h => h.Collapsed, true));

        var aside = cut.Find("aside");
        Assert.Contains("-translate-x-full", aside.GetAttribute("class"));
        // Closed sheet has no scrim.
        Assert.DoesNotContain("bg-black/50", cut.Markup);
    }

    [Fact]
    public void MobileSheet_Falls_Back_To_Variant_On_Desktop()
    {
        SetViewport(1280); // desktop
        var cut = _ctx.Render<SidebarHost>(p => p
            .Add(h => h.MobileSheet, true)
            .Add(h => h.Variant, L.SidebarProvider.SidebarVariant.Icon)
            .Add(h => h.Collapsed, true));

        var aside = cut.Find("aside");
        // Icon variant collapsed = w-16 rail, not an off-canvas sheet.
        Assert.Contains("w-16", aside.GetAttribute("class"));
        Assert.DoesNotContain("absolute", aside.GetAttribute("class"));
    }

    [Fact]
    public void Without_MobileSheet_No_Sheet_On_Mobile()
    {
        SetViewport(400);
        var cut = _ctx.Render<SidebarHost>(p => p
            .Add(h => h.Variant, L.SidebarProvider.SidebarVariant.Push)
            .Add(h => h.Collapsed, false));

        var aside = cut.Find("aside");
        // Push variant keeps its inline rail; not opted into the sheet.
        Assert.DoesNotContain("absolute", aside.GetAttribute("class"));
    }
}
