using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Avatar;

/// <summary>
/// AvatarShape.Themed (radius-token wave): Circle/Square stay LITERAL contracts —
/// a consumer who asked for a circle keeps a circle in every theme. The new Themed
/// option follows the theme radius instead: identical to Circle at stock radii
/// (rounded-[calc(var(--radius)*4)] clamps to a full circle for every size up to
/// Xxl) and squares off with the rest of the UI in sharp themes.
/// Multiplier bumped 3 -> 4 when the Xxl (80px) rung was added: *3 (36px) only
/// covered up to Xl's 32px half-height and broke at Xxl's 40px half-height;
/// *4 (48px) covers Xxl too.
/// </summary>
public class AvatarThemedShapeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AvatarThemedShapeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Circle_Contract_Stays_Literal_RoundedFull()
    {
        var cut = _ctx.Render<Lumeo.Avatar>(p => p.Add(a => a.Shape, Lumeo.Avatar.AvatarShape.Circle));
        Assert.Contains("rounded-full", cut.Find("div").ClassList);
    }

    [Fact]
    public void Themed_Follows_The_Radius_Token()
    {
        var cut = _ctx.Render<Lumeo.Avatar>(p => p.Add(a => a.Shape, Lumeo.Avatar.AvatarShape.Themed));
        var root = cut.Find("div");
        Assert.Contains("rounded-[calc(var(--radius)*4)]", root.ClassList);
        Assert.DoesNotContain("rounded-full", root.ClassList);
    }

    [Fact]
    public void Themed_Radius_Multiplier_Covers_Xxl_Half_Height()
    {
        // At Xxl (h-20 = 80px, half-height 40px) the radius must reach >= 40px at
        // the default --radius (0.75rem = 12px) for the corner to clamp to a full
        // circle. *4 => 12*4 = 48px >= 40px. Regression guard for the *3->*4 fix
        // (*3 = 36px would have left Xxl visibly squared).
        var cut = _ctx.Render<Lumeo.Avatar>(p => p
            .Add(a => a.Shape, Lumeo.Avatar.AvatarShape.Themed)
            .Add(a => a.Size, Lumeo.Size.Xxl));
        var root = cut.Find("div");
        Assert.Contains("rounded-[calc(var(--radius)*4)]", root.ClassList);
    }

    [Fact]
    public void Square_Contract_Unchanged()
    {
        var cut = _ctx.Render<Lumeo.Avatar>(p => p.Add(a => a.Shape, Lumeo.Avatar.AvatarShape.Square));
        Assert.Contains("rounded-md", cut.Find("div").ClassList);
    }

    [Theory]
    [InlineData(Lumeo.Avatar.AvatarShape.Square)]
    [InlineData(Lumeo.Avatar.AvatarShape.Themed)]
    public void Fallback_Does_Not_Paint_Its_Own_Circle_Inside_NonCircle_Shapes(Lumeo.Avatar.AvatarShape shape)
    {
        // User-reported (screenshot: sharp theme, Themed avatar still a circle): the
        // fallback's own hardcoded rounded-full painted the visible bg-muted surface as
        // a circle regardless of the wrapper's shape clip — the clip was LARGER than
        // the circle, so Square/Themed avatars with initials rendered round anyway.
        var cut = _ctx.Render<Lumeo.Avatar>(p => p
            .Add(a => a.Shape, shape)
            .AddChildContent<Lumeo.AvatarFallback>(f => f.AddChildContent("MB")));

        var fallback = cut.FindAll("div").First(d => d.ClassList.Contains("bg-muted"));
        Assert.DoesNotContain("rounded-full", fallback.ClassList);
    }
}
