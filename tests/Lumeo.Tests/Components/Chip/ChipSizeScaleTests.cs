using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Chip;

// Pins all 7 Lumeo.Size rungs (Xxs, Xs, Sm, Md, Lg, Xl, Xxl) for Chip's
// SizeClass (text + radius, embedding PadClass at Comfortable density),
// AvatarSizeClass and CloseIconSize. Asserts the RENDERED class attribute,
// not the source switch — Cx.Merge's argument order can silently drop a
// class merged before the base class.
public class ChipSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ChipSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- SizeClass (text + radius), Comfortable density (default) ---

    [Theory]
    [InlineData(L.Size.Xxs, "text-[10px]", "rounded")]
    [InlineData(L.Size.Xs, "text-[11px]", "rounded")]
    [InlineData(L.Size.Sm, "text-xs", "rounded-md")]
    [InlineData(L.Size.Md, "text-sm", "rounded-md")]
    [InlineData(L.Size.Lg, "text-sm", "rounded-lg")]
    [InlineData(L.Size.Xl, "text-base", "rounded-xl")]
    [InlineData(L.Size.Xxl, "text-lg", "rounded-2xl")]
    public void SizeClass_Text_And_Radius_Per_Rung(L.Size size, string expectedText, string expectedRadius)
    {
        var cut = _ctx.Render<L.Chip>(p => p.Add(c => c.Size, size).AddChildContent("X"));
        // Token-exact match: "rounded" is a substring of "rounded-md"/"rounded-lg"/etc,
        // so a raw string.Contains would silently pass even if the wrong radius rendered.
        var cls = cut.Find("div").GetAttribute("class")!.Split(' ');
        Assert.Contains(expectedText, cls);
        Assert.Contains(expectedRadius, cls);
    }

    // --- PadClass at Comfortable (default) density, all 7 rungs ---

    [Theory]
    [InlineData(L.Size.Xxs, "px-1.5", "py-0")]
    [InlineData(L.Size.Xs, "px-1.5", "py-0.5")]
    [InlineData(L.Size.Sm, "px-2", "py-0.5")]
    [InlineData(L.Size.Md, "px-2.5", "py-0.5")]
    [InlineData(L.Size.Lg, "px-3", "py-1")]
    [InlineData(L.Size.Xl, "px-3.5", "py-1.5")]
    [InlineData(L.Size.Xxl, "px-4", "py-2")]
    public void PadClass_Comfortable_Per_Rung(L.Size size, string expectedPx, string expectedPy)
    {
        var cut = _ctx.Render<L.Chip>(p => p.Add(c => c.Size, size).AddChildContent("X"));
        // Token-exact: "py-0" is a substring of "py-0.5" ("px-2" of "px-2.5", etc.) —
        // a raw string.Contains would silently pass on the wrong decimal rung.
        var cls = cut.Find("div").GetAttribute("class")!.Split(' ');
        Assert.Contains(expectedPx, cls);
        Assert.Contains(expectedPy, cls);
    }

    // --- PadClass at Compact density: Xxs and Xs tie (no valid Tailwind
    //     token strictly between px-1/4px and px-1.5/6px) ---

    [Fact]
    public void PadClass_Compact_Xxs_And_Xs_Tie_At_Px1_Py0()
    {
        var xxs = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Size, L.Size.Xxs).Add(c => c.Density, L.Density.Compact).AddChildContent("X"));
        var xs = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Size, L.Size.Xs).Add(c => c.Density, L.Density.Compact).AddChildContent("X"));

        var xxsCls = xxs.Find("div").GetAttribute("class");
        var xsCls = xs.Find("div").GetAttribute("class");

        Assert.Contains("px-1 py-0", xxsCls);
        Assert.Contains("px-1 py-0", xsCls);
    }

    // --- AvatarSizeClass, all 7 rungs ---

    [Theory]
    [InlineData(L.Size.Xxs, "h-3 w-3")]
    [InlineData(L.Size.Xs, "h-3.5 w-3.5")]
    [InlineData(L.Size.Sm, "h-4 w-4")]
    [InlineData(L.Size.Md, "h-5 w-5")]
    [InlineData(L.Size.Lg, "h-6 w-6")]
    [InlineData(L.Size.Xl, "h-7 w-7")]
    [InlineData(L.Size.Xxl, "h-8 w-8")]
    public void AvatarSizeClass_Per_Rung(L.Size size, string expected)
    {
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Size, size).Add(c => c.Avatar, "/x.png").AddChildContent("X"));
        var cls = cut.Find("img").GetAttribute("class");
        Assert.Contains(expected, cls);
    }

    // --- CloseIconSize, all 7 rungs. Lg deliberately has NO explicit case
    //     (pre-existing gap, left as-is): it still ties Md at h-3.5 w-3.5. ---

    [Theory]
    [InlineData(L.Size.Xxs, "h-2.5 w-2.5")]
    [InlineData(L.Size.Xs, "h-2.5 w-2.5")] // ties Xxs — no token between 10px/12px
    [InlineData(L.Size.Sm, "h-3 w-3")]
    [InlineData(L.Size.Md, "h-3.5 w-3.5")]
    [InlineData(L.Size.Lg, "h-3.5 w-3.5")] // deliberately still ties Md (pre-existing gap, not fixed)
    [InlineData(L.Size.Xl, "h-4 w-4")]
    [InlineData(L.Size.Xxl, "h-5 w-5")]
    public void CloseIconSize_Per_Rung(L.Size size, string expected)
    {
        var cut = _ctx.Render<L.Chip>(p => p
            .Add(c => c.Size, size).Add(c => c.Closable, true).AddChildContent("X"));
        var svg = cut.Find("button svg");
        Assert.Equal(expected, svg.GetAttribute("class"));
    }
}
