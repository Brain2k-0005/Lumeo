using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Avatar;

/// <summary>
/// AvatarShape.Themed (radius-token wave): Circle/Square stay LITERAL contracts —
/// a consumer who asked for a circle keeps a circle in every theme. The new Themed
/// option follows the theme radius instead: identical to Circle at stock radii
/// (rounded-[calc(var(--radius)*5)] clamps to a full circle for every size up to
/// Xxl, including the tighter 0.5rem base under .style-new-york) and squares off
/// with the rest of the UI in sharp themes.
/// Multiplier history: *3 (24px at the tight base) broke Xl's 32px half-height
/// under .style-new-york; *4 (32px at the tight base) exactly covered Xl but not
/// the newly added Xxl rung's 40px half-height; *5 (40px at the tight base)
/// covers Xxl too.
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
        Assert.Contains("rounded-[calc(var(--radius)*5)]", root.ClassList);
        Assert.DoesNotContain("rounded-full", root.ClassList);
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, 20)]
    [InlineData(Lumeo.Size.Sm, 32)]
    [InlineData(Lumeo.Size.Md, 40)]
    [InlineData(Lumeo.Size.Lg, 48)]
    [InlineData(Lumeo.Size.Xl, 64)]
    [InlineData(Lumeo.Size.Xxl, 80)]
    public void Themed_Circle_Survives_The_Tightest_Shipped_Radius_Base(Lumeo.Size size, int boxPx)
    {
        // The tightest radius base Lumeo ships today is .style-new-york's
        // --radius: 0.5rem (8px). Extract Themed's live multiplier from the
        // rendered class and assert the resulting radius still clamps every
        // size to a full circle at that base. Wave-0 regression guard: at the
        // old *3 multiplier, Xl's computed radius (3 * 8px = 24px) fell below
        // its 32px half-height, so the Xl avatar stopped being a circle under
        // .style-new-york even though AvatarThemedShapeTests' class-string
        // assertion above stayed green throughout. The old *4 multiplier later
        // covered Xl (4 * 8px = 32px, exactly its half-height) but not Xxl's
        // 40px half-height once that rung was added — bumped to *5.
        var cut = _ctx.Render<Lumeo.Avatar>(p => p
            .Add(a => a.Shape, Lumeo.Avatar.AvatarShape.Themed)
            .Add(a => a.Size, size));
        const string prefix = "rounded-[calc(var(--radius)*";
        var cls = cut.Find("div").ClassList.First(c => c.StartsWith(prefix, StringComparison.Ordinal));
        var rest = cls[prefix.Length..]; // e.g. "4)]" — everything after the '*'
        var multiplier = double.Parse(rest[..rest.IndexOf(')')], System.Globalization.CultureInfo.InvariantCulture);

        const double newYorkRadiusBasePx = 8; // .style-new-york: --radius: 0.5rem
        var computedRadiusPx = multiplier * newYorkRadiusBasePx;

        Assert.True(computedRadiusPx >= boxPx / 2.0,
            $"Themed avatar at {size} (box {boxPx}px) needs radius >= {boxPx / 2.0}px to stay a " +
            $"circle under .style-new-york's 0.5rem base, but the *{multiplier} multiplier only " +
            $"reaches {computedRadiusPx}px.");
    }

    [Fact]
    public void Themed_Radius_Multiplier_Covers_Xxl_Half_Height()
    {
        // At Xxl (h-20 = 80px, half-height 40px) the radius must reach >= 40px at
        // the default --radius (0.75rem = 12px) for the corner to clamp to a full
        // circle. *5 => 12*5 = 60px >= 40px. It must also clear 40px at the
        // tightest shipped base (.style-new-york's 0.5rem = 8px): 8*5 = 40px,
        // exactly equal — see Themed_Circle_Survives_The_Tightest_Shipped_Radius_Base
        // for that cross-base check. Regression guard for the *3->*4->*5 history
        // (*3 = 36px / *4 = 48px at the default base would have left Xxl visibly
        // squared or, at the tight base, squared even earlier sizes).
        var cut = _ctx.Render<Lumeo.Avatar>(p => p
            .Add(a => a.Shape, Lumeo.Avatar.AvatarShape.Themed)
            .Add(a => a.Size, Lumeo.Size.Xxl));
        var root = cut.Find("div");
        Assert.Contains("rounded-[calc(var(--radius)*5)]", root.ClassList);
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
