using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.SparkCard;

// Pins all 7 Lumeo.Size rungs for SparkCard's SparkHeight (int, forwarded to
// the child Sparkline's viewBox), SizePadding, SparklineHeightClass,
// LabelClass and ValueClass. Asserts rendered attributes, not source
// strings.
public class SparkCardSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SparkCardSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly double[] TwoPoints = [1, 2];

    // --- SparkHeight (int) -> Sparkline's viewBox "0 0 100 {height}", and
    //     SparklineHeightClass's h-* MUST stay pixel-matched to it. ---

    [Theory]
    [InlineData(L.Size.Xxs, 16, "mt-0 h-4")]
    [InlineData(L.Size.Xs, 24, "mt-1 h-6")]
    [InlineData(L.Size.Sm, 32, "mt-2 h-8")]
    [InlineData(L.Size.Md, 40, "mt-3 h-10")]
    [InlineData(L.Size.Lg, 56, "mt-4 h-14")]
    [InlineData(L.Size.Xl, 72, "mt-5 h-[72px]")]
    [InlineData(L.Size.Xxl, 88, "mt-6 h-[88px]")]
    public void SparkHeight_And_SparklineHeightClass_Stay_Pixel_Matched(L.Size size, int expectedPx, string expectedWrapperClass)
    {
        var cut = _ctx.Render<Lumeo.SparkCard>(p => p
            .Add(s => s.Size, size).Add(s => s.Data, TwoPoints));

        var viewBox = cut.Find("svg").GetAttribute("viewBox");
        Assert.Equal($"0 0 100 {expectedPx}", viewBox);

        var wrapperCls = cut.Find("div.w-full").GetAttribute("class");
        Assert.Contains(expectedWrapperClass, wrapperCls);
    }

    // --- SizePadding, all 7 rungs (root card padding) ---

    [Theory]
    [InlineData(L.Size.Xxs, "p-0")]
    [InlineData(L.Size.Xs, "p-1")]
    [InlineData(L.Size.Sm, "p-3")]
    [InlineData(L.Size.Md, "p-5")]
    [InlineData(L.Size.Lg, "p-6")]
    [InlineData(L.Size.Xl, "p-7")]
    [InlineData(L.Size.Xxl, "p-8")]
    public void SizePadding_Per_Rung(L.Size size, string expected)
    {
        var cut = _ctx.Render<Lumeo.SparkCard>(p => p.Add(s => s.Size, size));
        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains(cls!.Split(' '), t => t == expected);
    }

    // --- LabelClass, all 7 rungs ---

    [Theory]
    [InlineData(L.Size.Xxs, "text-[8px]")]
    [InlineData(L.Size.Xs, "text-[10px]")]
    [InlineData(L.Size.Sm, "text-xs")]
    [InlineData(L.Size.Md, "text-sm")]
    [InlineData(L.Size.Lg, "text-base")]
    [InlineData(L.Size.Xl, "text-lg")]
    [InlineData(L.Size.Xxl, "text-xl")]
    public void LabelClass_Per_Rung(L.Size size, string expected)
    {
        var cut = _ctx.Render<Lumeo.SparkCard>(p => p
            .Add(s => s.Size, size).Add(s => s.Label, "Revenue"));
        var cls = cut.Find("div.truncate").GetAttribute("class");
        Assert.Contains(cls!.Split(' '), t => t == expected);
    }

    // --- ValueClass, all 7 rungs. Xxs/Xs both clamp mt to mt-0 (flagged
    //     tie); Xxs's text is floored to text-[10px] (readability floor,
    //     not raw arithmetic) while Xs lands exactly on text-xs. ---

    [Theory]
    [InlineData(L.Size.Xxs, "mt-0", "text-[10px]")]
    [InlineData(L.Size.Xs, "mt-0", "text-xs")]
    [InlineData(L.Size.Sm, "mt-0.5", "text-lg")]
    [InlineData(L.Size.Md, "mt-1", "text-2xl")]
    [InlineData(L.Size.Lg, "mt-1.5", "text-3xl")]
    [InlineData(L.Size.Xl, "mt-2", "text-4xl")]
    [InlineData(L.Size.Xxl, "mt-2.5", "text-5xl")]
    public void ValueClass_Per_Rung(L.Size size, string expectedMt, string expectedText)
    {
        var cut = _ctx.Render<Lumeo.SparkCard>(p => p
            .Add(s => s.Size, size).Add(s => s.Value, "1,024"));
        var cls = cut.Find("div.font-semibold").GetAttribute("class")!.Split(' ');
        Assert.Contains(expectedMt, cls);
        Assert.Contains(expectedText, cls);
    }

    [Fact]
    public void ValueClass_Xxs_And_Xs_Legitimately_Tie_On_Margin_Only()
    {
        var xxs = _ctx.Render<Lumeo.SparkCard>(p => p.Add(s => s.Size, L.Size.Xxs).Add(s => s.Value, "1"));
        var xs = _ctx.Render<Lumeo.SparkCard>(p => p.Add(s => s.Size, L.Size.Xs).Add(s => s.Value, "1"));

        var xxsCls = xxs.Find("div.font-semibold").GetAttribute("class")!.Split(' ');
        var xsCls = xs.Find("div.font-semibold").GetAttribute("class")!.Split(' ');

        Assert.Contains("mt-0", xxsCls);
        Assert.Contains("mt-0", xsCls);
        // Text size does NOT tie — Xxs is floored to text-[10px], Xs is text-xs.
        Assert.Contains("text-[10px]", xxsCls);
        Assert.Contains("text-xs", xsCls);
    }
}
