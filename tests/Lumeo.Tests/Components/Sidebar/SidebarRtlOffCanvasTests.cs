using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Sidebar;

/// <summary>
/// Codex P2 — a collapsed off-canvas sidebar must slide off the edge it is anchored to in BOTH
/// directions. The default Side.Left sidebar anchors with the LOGICAL `start-0` (the right edge under
/// RTL), but the collapsed branch applied only the physical `-translate-x-full`, so under RTL the panel
/// slid LEFT into the viewport instead of off the (right) inline-start edge. The translate is now
/// direction-aware: physical `-translate-x-full` for LTR + `rtl:translate-x-full` for RTL.
/// </summary>
public class SidebarRtlOffCanvasTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SidebarRtlOffCanvasTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Collapsed_Overlay_Sidebar_Has_Direction_Aware_OffCanvas_Translate()
    {
        var cut = _ctx.Render<SidebarHost>(p => p
            .Add(x => x.Variant, Lumeo.SidebarProvider.SidebarVariant.Overlay)
            .Add(x => x.Collapsed, true));

        var cls = cut.Find("aside").GetAttribute("class") ?? "";
        // LTR slides off the physical left; RTL must slide off the physical right (the inline-start edge).
        Assert.Contains("-translate-x-full", cls);
        Assert.Contains("rtl:translate-x-full", cls);
    }
}
