using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toast;

/// <summary>
/// Tests for Toast positioning (viewport-level) and MaxVisible stacking cap.
/// </summary>
public class ToastPositioningTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToastPositioningTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void ToastViewport_TopLeft_Has_Correct_Position_And_FlexCol_Direction()
    {
        var cut = _ctx.Render<L.ToastViewport>(p => p
            .Add(b => b.Position, L.ToastViewport.ToastPosition.TopLeft));

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        // Position anchor classes
        Assert.Contains("top-4", cls);
        Assert.Contains("left-4", cls);
        // Viewport uses flex-col for top positions (not flex-col-reverse)
        Assert.Contains("flex-col", cls);
        Assert.DoesNotContain("flex-col-reverse", cls);
    }

    [Fact]
    public void ToastProvider_MaxVisible_2_With_3_Toasts_Shows_Only_2()
    {
        var toastService = _ctx.Services.GetService(typeof(ToastService)) as ToastService;
        Assert.NotNull(toastService);

        // MaxVisible=2 syncs MaxToasts=2, so the 3rd toast evicts the 1st.
        var cut = _ctx.Render<L.ToastProvider>(p => p
            .Add(b => b.MaxVisible, 2));

        toastService!.Show("Toast One");
        toastService!.Show("Toast Two");
        toastService!.Show("Toast Three");

        // Wait until eviction settles to exactly 2 (exit animation is 220 ms).
        cut.WaitForState(
            () => cut.FindAll("[role='alert'],[role='status']").Count == 2,
            TimeSpan.FromSeconds(5));

        Assert.Equal(2, cut.FindAll("[role='alert'],[role='status']").Count);
    }
}
