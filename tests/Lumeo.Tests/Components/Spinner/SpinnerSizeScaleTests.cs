using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Spinner;

// Pins all 7 Lumeo.Size rungs for Spinner's Ring/Dots/Bars sub-dimensions.
// Where two rungs legitimately render identically (floored — no valid
// Tailwind token exists below the pre-existing Sm value), the tie is
// asserted explicitly so a future change that accidentally differentiates
// them (or accidentally ties a THIRD rung) is caught either way.
public class SpinnerSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SpinnerSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Ring: SizeClasses — no ties, every rung distinct ---

    [Theory]
    // The RING moved down one rung in the 5.0 scale alignment (shadcn's spinner is size-4,
    // so Md sits there). The Bars and Dots variants below are untouched.
    [InlineData(L.Size.Xxs, "h-1.5 w-1.5")]
    [InlineData(L.Size.Xs, "h-2 w-2")]
    [InlineData(L.Size.Sm, "h-3 w-3")]
    [InlineData(L.Size.Md, "h-4 w-4")]
    [InlineData(L.Size.Lg, "h-6 w-6")]
    [InlineData(L.Size.Xl, "h-8 w-8")]
    [InlineData(L.Size.Xxl, "h-10 w-10")]
    public void Ring_SizeClasses_Per_Rung(L.Size size, string expected)
    {
        var cut = _ctx.Render<L.Spinner>(p => p.Add(s => s.Size, size));
        var cls = cut.Find("svg").GetAttribute("class")!;
        var (h, w) = (expected.Split(' ')[0], expected.Split(' ')[1]);
        var tokens = cls.Split(' ');
        Assert.Contains(h, tokens);
        Assert.Contains(w, tokens);
    }

    // --- Dots: DotGapClass — Xxs and Xs legitimately tie at gap-0 ---

    [Theory]
    [InlineData(L.Size.Xxs, "gap-0")]
    [InlineData(L.Size.Xs, "gap-0")]
    [InlineData(L.Size.Sm, "gap-0.5")]
    [InlineData(L.Size.Md, "gap-1")]
    [InlineData(L.Size.Lg, "gap-1.5")]
    [InlineData(L.Size.Xl, "gap-2")]
    [InlineData(L.Size.Xxl, "gap-2.5")]
    public void DotGapClass_Per_Rung(L.Size size, string expected)
    {
        var cut = _ctx.Render<L.Spinner>(p => p
            .Add(s => s.Size, size).Add(s => s.Variant, L.Spinner.SpinnerVariant.Dots));
        // Token-exact: "gap-0" is a substring of "gap-0.5" ("gap-1" of "gap-1.5", etc.) —
        // a raw string.Contains would silently pass on the wrong decimal rung.
        var containerCls = cut.Find("div[aria-hidden='true']").GetAttribute("class")!.Split(' ');
        Assert.Contains(expected, containerCls);
    }

    [Fact]
    public void DotGapClass_Xxs_And_Xs_Legitimately_Tie()
    {
        var xxs = _ctx.Render<L.Spinner>(p => p.Add(s => s.Size, L.Size.Xxs).Add(s => s.Variant, L.Spinner.SpinnerVariant.Dots));
        var xs = _ctx.Render<L.Spinner>(p => p.Add(s => s.Size, L.Size.Xs).Add(s => s.Variant, L.Spinner.SpinnerVariant.Dots));

        var xxsCls = xxs.Find("div[aria-hidden='true']").GetAttribute("class");
        var xsCls = xs.Find("div[aria-hidden='true']").GetAttribute("class");

        Assert.Contains("gap-0", xxsCls);
        Assert.Contains("gap-0", xsCls);
        Assert.DoesNotContain("gap-0.5", xxsCls);
        Assert.DoesNotContain("gap-0.5", xsCls);
    }

    // --- Dots: DotClass (diameter) — no ties, every rung distinct ---

    [Theory]
    [InlineData(L.Size.Xxs, "h-0.5 w-0.5")]
    [InlineData(L.Size.Xs, "h-1 w-1")]
    [InlineData(L.Size.Sm, "h-1.5 w-1.5")]
    [InlineData(L.Size.Md, "h-2 w-2")]
    [InlineData(L.Size.Lg, "h-3 w-3")]
    [InlineData(L.Size.Xl, "h-4 w-4")]
    [InlineData(L.Size.Xxl, "h-5 w-5")]
    public void DotClass_Diameter_Per_Rung(L.Size size, string expected)
    {
        var cut = _ctx.Render<L.Spinner>(p => p
            .Add(s => s.Size, size).Add(s => s.Variant, L.Spinner.SpinnerVariant.Dots));
        var dot = cut.FindAll("div.animate-bounce")[0];
        var tokens = dot.GetAttribute("class")!.Split(' ');
        var (h, w) = (expected.Split(' ')[0], expected.Split(' ')[1]);
        Assert.Contains(h, tokens);
        Assert.Contains(w, tokens);
    }

    // --- Bars: BarsContainerClass (height) — no ties, every rung distinct ---

    [Theory]
    [InlineData(L.Size.Xxs, "h-1")]
    [InlineData(L.Size.Xs, "h-2")]
    [InlineData(L.Size.Sm, "h-3")]
    [InlineData(L.Size.Md, "h-4")]
    [InlineData(L.Size.Lg, "h-6")]
    [InlineData(L.Size.Xl, "h-8")]
    [InlineData(L.Size.Xxl, "h-10")]
    public void BarsContainerClass_Height_Per_Rung(L.Size size, string expected)
    {
        var cut = _ctx.Render<L.Spinner>(p => p
            .Add(s => s.Size, size).Add(s => s.Variant, L.Spinner.SpinnerVariant.Bars));
        var containerCls = cut.Find("div[aria-hidden='true']").GetAttribute("class");
        // Match the whole token, not a substring of e.g. h-10 matching h-1.
        Assert.Contains(containerCls!.Split(' '), t => t == expected);
    }

    // --- Bars: BarClass (width) — Xxs, Xs, and pre-existing Sm all
    //     legitimately tie at w-0.5 (three-way tie: Sm - 0.5 step would be a
    //     zero/invisible width, so both new rungs floor at Sm's own value). ---

    [Theory]
    [InlineData(L.Size.Xxs, "w-0.5")]
    [InlineData(L.Size.Xs, "w-0.5")]
    [InlineData(L.Size.Sm, "w-0.5")]
    [InlineData(L.Size.Md, "w-1")]
    [InlineData(L.Size.Lg, "w-1.5")]
    [InlineData(L.Size.Xl, "w-2")]
    [InlineData(L.Size.Xxl, "w-2.5")]
    public void BarClass_Width_Per_Rung(L.Size size, string expected)
    {
        var cut = _ctx.Render<L.Spinner>(p => p
            .Add(s => s.Size, size).Add(s => s.Variant, L.Spinner.SpinnerVariant.Bars));
        var bar = cut.FindAll("div.animate-pulse")[0];
        Assert.Contains(bar.GetAttribute("class")!.Split(' '), t => t == expected);
    }

    [Fact]
    public void BarClass_Xxs_Xs_Sm_Legitimately_Three_Way_Tie()
    {
        L.Size[] rungs = [L.Size.Xxs, L.Size.Xs, L.Size.Sm];
        foreach (var size in rungs)
        {
            var cut = _ctx.Render<L.Spinner>(p => p
                .Add(s => s.Size, size).Add(s => s.Variant, L.Spinner.SpinnerVariant.Bars));
            var bar = cut.FindAll("div.animate-pulse")[0];
            Assert.Contains(bar.GetAttribute("class")!.Split(' '), t => t == "w-0.5");
        }
    }
}
