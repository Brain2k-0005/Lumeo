using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Rating;

// Pins all 7 Lumeo.Size rungs for Rating's SizeClasses ([&_svg]:h-{n}/w-{n} on the
// star button). Sm->Md->Lg is non-uniform (S1=4px, S2=12px); using S2 outward would
// produce absurd 44px/56px stars, so both directions extrapolate off S1 (4px) per
// the spec's deliberate deviation — see the component's inline comment.
//
// Touch-target fix (owner decision, PR #388 follow-up): the star buttons carried
// zero padding, so hit-area == icon size exactly (Xxs 8px, Xs 12px, Sm 16px,
// Md 20px all under the 24px minimum; Lg 32px/Xl 36px/Xxl 40px already pass).
// Owner chose to patch ONLY Sm and Md (the two rungs reached without a deliberate
// dense-size opt-in — Md is the default) via real button padding (p-1 / p-0.5),
// leaving Xxs/Xs deliberately tiny and Lg+ untouched. The tests below establish
// BOTH halves of that change: the rendered icon SIZE is unchanged (same
// [&_svg]:h-N/w-N token as before) and the button's own BOX (icon + padding) now
// computes to >=24px at Sm/Md — a padding-only class assertion would prove neither.
public class RatingSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public RatingSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Theory]
    [InlineData(L.Size.Xxs, "[&_svg]:h-2", "[&_svg]:w-2")]
    [InlineData(L.Size.Xs, "[&_svg]:h-3", "[&_svg]:w-3")]
    [InlineData(L.Size.Sm, "[&_svg]:h-4", "[&_svg]:w-4")]
    [InlineData(L.Size.Md, "[&_svg]:h-5", "[&_svg]:w-5")]
    [InlineData(L.Size.Lg, "[&_svg]:h-8", "[&_svg]:w-8")]
    [InlineData(L.Size.Xl, "[&_svg]:h-9", "[&_svg]:w-9")]
    [InlineData(L.Size.Xxl, "[&_svg]:h-10", "[&_svg]:w-10")]
    public void SizeClasses_Per_Rung(L.Size size, string expectedH, string expectedW)
    {
        // This is the "visible star is unchanged" half of the touch-target fix:
        // the [&_svg]:h-N/w-N token — which sizes the actual rendered <svg>,
        // completely independent of the button's own padding — is identical to
        // the pre-fix values at every rung, including Sm and Md.
        var cut = _ctx.Render<L.Rating>(p => p.Add(r => r.Size, size));
        var star = cut.FindAll("button")[0];
        var tokens = star.GetAttribute("class")!.Split(' ');
        Assert.Contains(expectedH, tokens);
        Assert.Contains(expectedW, tokens);
    }

    // --- Touch-target hit box: icon size (px) + 2 * button padding (px). ---

    [Theory]
    [InlineData(L.Size.Xxs, 8)]
    [InlineData(L.Size.Xs, 12)]
    [InlineData(L.Size.Sm, 24)]
    [InlineData(L.Size.Md, 24)]
    [InlineData(L.Size.Lg, 32)]
    [InlineData(L.Size.Xl, 36)]
    [InlineData(L.Size.Xxl, 40)]
    public void Hit_Box_Total_Size_Per_Rung(L.Size size, double expectedTotalPx)
    {
        // Total hit box = icon size (from [&_svg]:h-N) + 2 * button padding (p-N).
        var cut = _ctx.Render<L.Rating>(p => p.Add(r => r.Size, size));
        var star = cut.FindAll("button")[0];
        var cls = star.GetAttribute("class")!;

        var iconMatch = System.Text.RegularExpressions.Regex.Match(cls, @"\[&_svg\]:h-(?<n>[0-9.]+)");
        Assert.True(iconMatch.Success, $"no [&_svg]:h-N token found in '{cls}'");
        var iconPx = double.Parse(iconMatch.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture) * 4.0;

        var paddingPx = SizeScaleAssert.SpacingPx(cls, "p") ?? 0; // 0 when no p-N token (Xxs/Xs/Lg/Xl/Xxl)
        var totalPx = iconPx + 2 * paddingPx;

        Assert.Equal(expectedTotalPx, totalPx);
    }

    [Fact]
    public void Sm_And_Md_Reach_Exactly_24px_Hit_Box()
    {
        var sm = _ctx.Render<L.Rating>(p => p.Add(r => r.Size, L.Size.Sm));
        var md = _ctx.Render<L.Rating>(p => p.Add(r => r.Size, L.Size.Md));

        var smCls = sm.FindAll("button")[0].GetAttribute("class")!.Split(' ');
        var mdCls = md.FindAll("button")[0].GetAttribute("class")!.Split(' ');

        Assert.Contains("p-1", smCls);
        Assert.Contains("[&_svg]:h-4", smCls); // icon itself unchanged
        Assert.Contains("p-0.5", mdCls);
        Assert.Contains("[&_svg]:h-5", mdCls); // icon itself unchanged
    }

    [Fact]
    public void Xxs_Xs_And_Lg_Plus_Get_No_Padding_Regression_Guard()
    {
        // Owner's explicit boundary: Xxs/Xs deliberately stay tiny; Lg/Xl/Xxl were
        // already passing and must not gain padding they don't need.
        foreach (var size in new[] { L.Size.Xxs, L.Size.Xs, L.Size.Lg, L.Size.Xl, L.Size.Xxl })
        {
            var cut = _ctx.Render<L.Rating>(p => p.Add(r => r.Size, size));
            var cls = cut.FindAll("button")[0].GetAttribute("class")!.Split(' ');
            Assert.DoesNotContain(cls, t => t.StartsWith("p-", StringComparison.Ordinal));
        }
    }
}
