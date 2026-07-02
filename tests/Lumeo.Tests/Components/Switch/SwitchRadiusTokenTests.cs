using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Switch;

/// <summary>
/// Consumer report: the Switch was the one control unaffected by the theme radius —
/// track and thumb hardcoded <c>rounded-full</c>, so in a deliberately sharp theme
/// (<c>--radius: 0</c>) everything squared off except the Switch, which stayed a pill.
/// (Stock shadcn keeps its Switch rounded-full; this is a deliberate divergence in
/// favor of theme consistency.)
///
/// Contract: track + thumb use <c>rounded-[calc(var(--radius)*2)]</c>. At every stock
/// radius the computed value exceeds half the track height of ALL sizes (default
/// 0.75rem → 24px ≥ Lg's 12px half-height), so CSS corner-overlap reduction renders the
/// exact same full pill as before — the default look is pixel-identical. Only small/zero
/// radii square the Switch off along with the rest of the theme.
/// </summary>
public class SwitchRadiusTokenTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SwitchRadiusTokenTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Theory]
    [InlineData(Lumeo.Size.Sm)]
    [InlineData(Lumeo.Size.Md)]
    [InlineData(Lumeo.Size.Lg)]
    public void Track_Rounding_Follows_The_Theme_Radius_Token(Lumeo.Size size)
    {
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(s => s.Size, size));
        var track = cut.Find("button");

        Assert.Contains("rounded-[calc(var(--radius)*2)]", track.ClassList);
        Assert.DoesNotContain("rounded-full", track.ClassList);
    }

    [Fact]
    public void Thumb_Rounding_Follows_The_Theme_Radius_Token()
    {
        var cut = _ctx.Render<Lumeo.Switch>();
        // The thumb is the translating span inside the track button.
        var thumb = cut.FindAll("button span").First(s => s.ClassList.Contains("transition-transform"));

        Assert.Contains("rounded-[calc(var(--radius)*2)]", thumb.ClassList);
        Assert.DoesNotContain("rounded-full", thumb.ClassList);
    }

    [Fact]
    public void Consumer_Class_Can_Still_Override_The_Rounding()
    {
        // Cx.Merge conflict resolution: a consumer-supplied rounded-* must win over
        // the token mapping (e.g. forcing rounded-full back on a per-instance basis).
        var cut = _ctx.Render<Lumeo.Switch>(p => p.Add(s => s.Class, "rounded-full"));
        var track = cut.Find("button");

        Assert.Contains("rounded-full", track.ClassList);
        Assert.DoesNotContain("rounded-[calc(var(--radius)*2)]", track.ClassList);
    }
}
