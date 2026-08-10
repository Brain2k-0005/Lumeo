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
// Touch-target note (not asserted here, see PR report): the star buttons carry zero
// padding, so hit-area == icon size exactly. Xxs/Xs/Sm/Md all render under the 24px
// touch-target minimum; only Lg/Xl/Xxl pass.
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
        var cut = _ctx.Render<L.Rating>(p => p.Add(r => r.Size, size));
        var star = cut.FindAll("button")[0];
        var tokens = star.GetAttribute("class")!.Split(' ');
        Assert.Contains(expectedH, tokens);
        Assert.Contains(expectedW, tokens);
    }
}
