using System.Globalization;
using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Skeleton;

/// <summary>
/// Accessibility tier for <see cref="L.SkeletonCircle"/>. SkeletonCircle is a loading
/// placeholder just like <see cref="L.Skeleton"/> and carries the identical contract.
///
/// Field report #464, finding 3: a SkeletonCircle previously rendered role="status"
/// aria-label="Loading" on EVERY instance, so a row of several avatar/icon placeholders was
/// several competing live regions, and the hardcoded "Loading" literal ignored the culture.
/// The corrected contract mirrors Skeleton.razor exactly: a circle is aria-hidden and silent
/// by default (matching shadcn's plain-div Skeleton); it announces as a single role="status"
/// region only when the caller opts in, either by setting <see cref="L.SkeletonCircle.AriaLabel"/>
/// (a custom label implies the caller wants it read) or <see cref="L.SkeletonCircle.Announce"/>
/// (uses the localized default when no AriaLabel is set).
/// </summary>
public class SkeletonCircleBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SkeletonCircleBehaviorTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Default: silent / aria-hidden (no per-instance live region) ---

    [Fact]
    public void Default_Render_Has_No_Status_Role_And_Is_AriaHidden()
    {
        var cut = _ctx.Render<L.SkeletonCircle>();

        var div = cut.Find("div");
        Assert.Null(div.GetAttribute("role"));
        Assert.Equal("true", div.GetAttribute("aria-hidden"));
        Assert.Null(div.GetAttribute("aria-label"));
        Assert.Null(div.GetAttribute("aria-busy"));
    }

    // --- Explicit AriaLabel opts the circle into a single announcement ---

    [Fact]
    public void Explicit_AriaLabel_Renders_One_Status_Region_With_The_Given_Text()
    {
        var cut = _ctx.Render<L.SkeletonCircle>(p => p
            .Add(s => s.AriaLabel, "Lädt Avatar"));

        var div = cut.Find("div");
        Assert.Equal("status", div.GetAttribute("role"));
        Assert.Equal("true", div.GetAttribute("aria-busy"));
        Assert.Equal("Lädt Avatar", div.GetAttribute("aria-label"));
        Assert.Null(div.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Empty_AriaLabel_Does_Not_Opt_In_And_Stays_AriaHidden()
    {
        // An empty string is not a customization — it must behave exactly like
        // AriaLabel being unset (silent by default), not fall back to a
        // "Loading" text while still announcing.
        var cut = _ctx.Render<L.SkeletonCircle>(p => p
            .Add(s => s.AriaLabel, ""));

        var div = cut.Find("div");
        Assert.Null(div.GetAttribute("role"));
        Assert.Equal("true", div.GetAttribute("aria-hidden"));
        Assert.Null(div.GetAttribute("aria-label"));
    }

    // --- Announce opts in without a custom label: localized default ---

    [Fact]
    public void Announce_True_Without_AriaLabel_Renders_Status_With_Localized_Default()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var cutEn = _ctx.Render<L.SkeletonCircle>(p => p.Add(s => s.Announce, true));
            var divEn = cutEn.Find("div");
            Assert.Equal("status", divEn.GetAttribute("role"));
            Assert.Equal("true", divEn.GetAttribute("aria-busy"));
            Assert.Equal("Loading", divEn.GetAttribute("aria-label"));

            // The localized default follows CultureInfo.CurrentUICulture, not a
            // hardcoded English literal (the field-report bug).
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            var cutDe = _ctx.Render<L.SkeletonCircle>(p => p.Add(s => s.Announce, true));
            var divDe = cutDe.Find("div");
            Assert.Equal("status", divDe.GetAttribute("role"));
            Assert.Equal("Wird geladen", divDe.GetAttribute("aria-label"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void Announce_True_With_AriaLabel_Prefers_The_Explicit_Label()
    {
        var cut = _ctx.Render<L.SkeletonCircle>(p => p
            .Add(s => s.Announce, true)
            .Add(s => s.AriaLabel, "Custom text"));

        var div = cut.Find("div");
        Assert.Equal("status", div.GetAttribute("role"));
        Assert.Equal("Custom text", div.GetAttribute("aria-label"));
    }

    [Fact]
    public void Announce_False_Stays_AriaHidden_Even_Without_AriaLabel()
    {
        var cut = _ctx.Render<L.SkeletonCircle>(p => p.Add(s => s.Announce, false));

        var div = cut.Find("div");
        Assert.Null(div.GetAttribute("role"));
        Assert.Equal("true", div.GetAttribute("aria-hidden"));
    }

    // --- Wave branch: same opt-in contract, distinct animation markup ---

    [Fact]
    public void Wave_Default_Is_AriaHidden_Like_Every_Other_Branch()
    {
        var cut = _ctx.Render<L.SkeletonCircle>(p => p
            .Add(s => s.Animation, L.Skeleton.SkeletonAnimation.Wave));

        var div = cut.Find("div");
        Assert.Null(div.GetAttribute("role"));
        Assert.Equal("true", div.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Wave_With_Explicit_AriaLabel_Announces_And_Emits_Gradient_Animation()
    {
        var cut = _ctx.Render<L.SkeletonCircle>(p => p
            .Add(s => s.Animation, L.Skeleton.SkeletonAnimation.Wave)
            .Add(s => s.AriaLabel, "Wird geladen"));

        var div = cut.Find("div");

        // Same opt-in screen-reader contract regardless of which animation branch renders.
        Assert.Equal("status", div.GetAttribute("role"));
        Assert.Equal("true", div.GetAttribute("aria-busy"));
        Assert.Equal("Wird geladen", div.GetAttribute("aria-label"));

        // Wave uses a moving gradient driven by an inline animation referencing the
        // skeleton-wave keyframes (instead of the Tailwind animate-pulse utility).
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("bg-gradient-to-r", cls);

        var style = div.GetAttribute("style") ?? "";
        Assert.Contains("skeleton-wave", style);
    }
}
