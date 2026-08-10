using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Kbd;

// Pins all 7 Lumeo.Size rungs for Kbd's SizeClasses. Height's Sm->Md step is 0
// (both tie at h-5) so the new Xxs/Xs rungs deliberately deviate from mechanical
// extrapolation (which would freeze them at 20px too) and use a flat 1-step
// Tailwind decrement instead — see the component's inline comment and the spec.
public class KbdSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public KbdSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Theory]
    [InlineData(L.Size.Xxs, "h-3.5", "px-0", "text-[8px]")]
    [InlineData(L.Size.Xs, "h-4", "px-0.5", "text-[9px]")]
    [InlineData(L.Size.Sm, "h-5", "px-1", "text-[10px]")]
    [InlineData(L.Size.Md, "h-5", "px-1.5", "text-[11px]")]
    [InlineData(L.Size.Lg, "h-6", "px-2", "text-xs")]
    [InlineData(L.Size.Xl, "h-7", "px-2.5", "text-[13px]")]
    [InlineData(L.Size.Xxl, "h-8", "px-3", "text-sm")]
    public void SizeClasses_Per_Rung(L.Size size, string expectedH, string expectedPx, string expectedText)
    {
        var cut = _ctx.Render<L.Kbd>(p => p.Add(k => k.Size, size).AddChildContent("K"));
        var tokens = cut.Find("kbd").GetAttribute("class")!.Split(' ');
        Assert.Contains(expectedH, tokens);
        Assert.Contains(expectedPx, tokens);
        Assert.Contains(expectedText, tokens);
    }

    [Fact]
    public void Sm_And_Md_Legitimately_Tie_On_Height()
    {
        // Pre-existing tie (h-5 at both Sm and Md) — asserted explicitly so a future
        // change that differentiates or further ties a rung is caught either way.
        var sm = _ctx.Render<L.Kbd>(p => p.Add(k => k.Size, L.Size.Sm).AddChildContent("K"));
        var md = _ctx.Render<L.Kbd>(p => p.Add(k => k.Size, L.Size.Md).AddChildContent("K"));

        Assert.Contains("h-5", sm.Find("kbd").GetAttribute("class")!.Split(' '));
        Assert.Contains("h-5", md.Find("kbd").GetAttribute("class")!.Split(' '));
    }
}
