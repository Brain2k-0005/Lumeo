using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Avatar;

// Pins all 7 Lumeo.Size rungs for Avatar's SizeClasses (outer wrapper div).
//
// Xs is the ONE deliberate exception to "purely additive" in this batch: it used to
// fall through to the Md catch-all (h-10 w-10 = 40px), rendering LARGER than Sm
// (32px) — a non-monotonic bug. Xs now gets its own rung (h-6 w-6 = 24px), which is
// a real visual change for any consumer currently relying on Size.Xs.
/// The rungs moved down one step in the 5.0 scale alignment: reui's and shadcn's avatar is
/// size-8 at the default rung, size-6 small, size-10 large. What this file guards - that the
/// rungs are distinct and monotonic, and that Xs no longer falls through to Md - is intact.
public class AvatarSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public AvatarSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Theory]
    [InlineData(L.Size.Xxs, "h-4", "w-4")]
    [InlineData(L.Size.Xs, "h-5", "w-5")]
    [InlineData(L.Size.Sm, "h-6", "w-6")]
    [InlineData(L.Size.Md, "h-8", "w-8")]
    [InlineData(L.Size.Lg, "h-10", "w-10")]
    [InlineData(L.Size.Xl, "h-12", "w-12")]
    [InlineData(L.Size.Xxl, "h-16", "w-16")]
    public void SizeClasses_Per_Rung(L.Size size, string expectedH, string expectedW)
    {
        var cut = _ctx.Render<L.Avatar>(p => p.Add(a => a.Size, size));
        var tokens = cut.Find("div").GetAttribute("class")!.Split(' ');
        Assert.Contains(expectedH, tokens);
        Assert.Contains(expectedW, tokens);
    }

    [Fact]
    public void Xxs_Also_Carries_Its_Text_Size_Override()
    {
        var cut = _ctx.Render<L.Avatar>(p => p.Add(a => a.Size, L.Size.Xxs));
        var tokens = cut.Find("div").GetAttribute("class")!.Split(' ');
        Assert.Contains("text-[8px]", tokens);
    }

    [Fact]
    public void Xs_No_Longer_Falls_Through_To_Md()
    {
        // Regression guard for the fixed inversion: Xs must render its own 24px
        // rung, not Md's 40px catch-all.
        var xs = _ctx.Render<L.Avatar>(p => p.Add(a => a.Size, L.Size.Xs));
        var md = _ctx.Render<L.Avatar>(p => p.Add(a => a.Size, L.Size.Md));

        var xsTokens = xs.Find("div").GetAttribute("class")!.Split(' ');
        var mdTokens = md.Find("div").GetAttribute("class")!.Split(' ');

        Assert.Contains("h-5", xsTokens);
        Assert.DoesNotContain("h-8", xsTokens);
        Assert.Contains("h-8", mdTokens);
    }

    [Fact]
    public void Xs_Is_Smaller_Than_Sm_Monotonic_Order_Restored()
    {
        // Xs (24px) must render smaller than Sm (32px) — the exact ordering that
        // was previously broken by the Md catch-all fallthrough.
        var xs = _ctx.Render<L.Avatar>(p => p.Add(a => a.Size, L.Size.Xs));
        var sm = _ctx.Render<L.Avatar>(p => p.Add(a => a.Size, L.Size.Sm));

        Assert.Contains("h-5", xs.Find("div").GetAttribute("class")!.Split(' '));
        Assert.Contains("h-6", sm.Find("div").GetAttribute("class")!.Split(' '));
    }

    [Fact]
    public void An_Avatar_That_Loses_Its_Size_Follows_Its_Group_Again()
    {
        // The flag that records "the caller gave this avatar a size" is read from the
        // ParameterView on every update. Raising it once and never lowering it meant a reused
        // avatar - same element, re-rendered without Size - kept ignoring its group for the
        // rest of its life, which is precisely the case a virtualized list produces.
        var cut = _ctx.Render<Lumeo.AvatarGroup>(p => p
            .Add(g => g.Size, Lumeo.Size.Xxs)
            .AddChildContent<Lumeo.Avatar>(a => a.Add(x => x.Size, Lumeo.Size.Lg)));

        Assert.Contains("h-10", Tokens(cut));      // its own Lg wins

        cut.Render(p => p
            .Add(g => g.Size, Lumeo.Size.Xxs)
            .AddChildContent<Lumeo.Avatar>(_ => { }));

        Assert.Contains("h-4", Tokens(cut));       // no Size of its own: follows the group
        Assert.DoesNotContain("h-10", Tokens(cut));
    }

    private static string[] Tokens(IRenderedComponent<Lumeo.AvatarGroup> cut) =>
        (cut.Find("[class*='shrink-0']").GetAttribute("class") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
