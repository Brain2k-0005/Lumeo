using Lumeo.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Lumeo.Tests.Components.Overlay;

/// <summary>
/// #228 — the OverlayService z-index allocator leaked: it used a bare counter
/// that decremented on close, so an out-of-order close could hand a newly opened
/// overlay a tier a still-open overlay already owned (stacking collision). It now
/// tracks active tiers per overlay id, always allocating one tier above the
/// current maximum and freeing the exact tier on close.
/// </summary>
public class OverlayZIndexTests
{
    private sealed class Body : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "x");
    }

    private static (OverlayService svc, List<OverlayInstance> shown) NewService()
    {
        var svc = new OverlayService();
        var shown = new List<OverlayInstance>();
        svc.OnShow += i => shown.Add(i);
        return (svc, shown);
    }

    [Fact]
    public void Each_New_Overlay_Stacks_Strictly_Above_The_Previous()
    {
        var (svc, shown) = NewService();
        _ = svc.ShowDialogAsync<Body>();
        _ = svc.ShowDialogAsync<Body>();
        _ = svc.ShowDialogAsync<Body>();

        Assert.Equal(3, shown.Count);
        Assert.True(shown[0].ZIndex < shown[1].ZIndex);
        Assert.True(shown[1].ZIndex < shown[2].ZIndex);
        // Content sits at ZIndex+1, which must still be below the next backdrop.
        Assert.True(shown[0].ZIndex + 1 < shown[1].ZIndex);
    }

    [Fact]
    public void Reopening_After_OutOfOrder_Close_Does_Not_Collide_With_Open_Overlay()
    {
        var (svc, shown) = NewService();
        _ = svc.ShowDialogAsync<Body>();       // A
        _ = svc.ShowDialogAsync<Body>();       // B (above A)
        var a = shown[0];
        var b = shown[1];

        // Close the FIRST overlay (A) while B stays open — the old counter would
        // free A's slot AND, on the next open, reuse B's tier.
        svc.Cancel(a.Id);

        _ = svc.ShowDialogAsync<Body>();       // C
        var c = shown[2];

        // C must stack strictly above the still-open B, never equal to it.
        Assert.NotEqual(b.ZIndex, c.ZIndex);
        Assert.True(c.ZIndex > b.ZIndex);
    }

    [Fact]
    public void Tiers_Reset_To_Base_Once_All_Overlays_Close()
    {
        var (svc, shown) = NewService();
        _ = svc.ShowDialogAsync<Body>();
        _ = svc.ShowDialogAsync<Body>();
        var first = shown[0].ZIndex;

        svc.Close(shown[0].Id);
        svc.Close(shown[1].Id);

        // With every overlay closed, the allocator must reclaim back to the base
        // tier instead of drifting upward forever.
        _ = svc.ShowDialogAsync<Body>();
        Assert.Equal(first, shown[2].ZIndex);
    }

    [Fact]
    public void First_Overlay_Sits_One_Step_Above_Base()
    {
        var (svc, shown) = NewService();
        _ = svc.ShowDialogAsync<Body>();
        Assert.Equal(OverlayService.BaseZIndex + OverlayService.Step, shown[0].ZIndex);
    }
}
