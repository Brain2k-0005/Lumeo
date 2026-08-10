using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Statistic;

// Pins all 7 Lumeo.Size rungs for Statistic's TitleClass, ValueClass and
// SuffixClass. NOTE: the spec's derivation table for ValueClass carried an
// `mt-*` prefix copied from SparkCard's sibling ValueClass shape; Statistic's
// actual ValueClass has never had a margin (Prefix/Value/Suffix render
// inline via `items-baseline gap-1`, not stacked), so these tests pin the
// text-size delta only — see the component's ValueClass comment.
public class StatisticSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public StatisticSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- TitleClass, all 7 rungs ---

    [Theory]
    [InlineData(L.Size.Xxs, "text-[8px]")]
    [InlineData(L.Size.Xs, "text-[10px]")]
    [InlineData(L.Size.Sm, "text-xs")]
    [InlineData(L.Size.Md, "text-sm")]
    [InlineData(L.Size.Lg, "text-base")]
    [InlineData(L.Size.Xl, "text-lg")]
    [InlineData(L.Size.Xxl, "text-xl")]
    public void TitleClass_Per_Rung(L.Size size, string expected)
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Size, size).Add(s => s.Title, "Revenue"));
        var cls = cut.Find("p").GetAttribute("class")!.Split(' ');
        Assert.Contains(expected, cls);
    }

    // --- ValueClass, all 7 rungs. Xxs and Xs legitimately tie at text-xs
    //     (raw arithmetic for Xxs is 6px, unreadable — deliberately floored
    //     to Xs's value rather than following the arithmetic down). ---

    [Theory]
    [InlineData(L.Size.Xxs, "text-xs")]
    [InlineData(L.Size.Xs, "text-xs")]
    [InlineData(L.Size.Sm, "text-lg")]
    [InlineData(L.Size.Md, "text-2xl")]
    [InlineData(L.Size.Lg, "text-4xl")]
    [InlineData(L.Size.Xl, "text-5xl")]
    [InlineData(L.Size.Xxl, "text-6xl")]
    public void ValueClass_Per_Rung(L.Size size, string expected)
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Size, size).Add(s => s.Value, "42"));
        var span = cut.FindAll("span")[0]; // ValueBody span
        var cls = span.GetAttribute("class")!.Split(' ');
        Assert.Contains(expected, cls);
    }

    [Fact]
    public void ValueClass_Xxs_And_Xs_Legitimately_Tie()
    {
        var xxs = _ctx.Render<Lumeo.Statistic>(p => p.Add(s => s.Size, L.Size.Xxs).Add(s => s.Value, "1"));
        var xs = _ctx.Render<Lumeo.Statistic>(p => p.Add(s => s.Size, L.Size.Xs).Add(s => s.Value, "1"));

        var xxsCls = xxs.FindAll("span")[0].GetAttribute("class")!.Split(' ');
        var xsCls = xs.FindAll("span")[0].GetAttribute("class")!.Split(' ');

        Assert.Contains("text-xs", xxsCls);
        Assert.Contains("text-xs", xsCls);
    }

    // --- SuffixClass, all 7 rungs (mirrors TitleClass) ---

    [Theory]
    [InlineData(L.Size.Xxs, "text-[8px]")]
    [InlineData(L.Size.Xs, "text-[10px]")]
    [InlineData(L.Size.Sm, "text-xs")]
    [InlineData(L.Size.Md, "text-sm")]
    [InlineData(L.Size.Lg, "text-base")]
    [InlineData(L.Size.Xl, "text-lg")]
    [InlineData(L.Size.Xxl, "text-xl")]
    public void SuffixClass_Per_Rung(L.Size size, string expected)
    {
        var cut = _ctx.Render<Lumeo.Statistic>(p => p
            .Add(s => s.Size, size)
            .Add(s => s.Value, "42")
            .Add(s => s.Suffix, (Microsoft.AspNetCore.Components.RenderFragment)(b => b.AddContent(0, "kg"))));
        var suffixSpan = cut.FindAll("span").Last(sp => sp.TextContent == "kg");
        var cls = suffixSpan.GetAttribute("class")!.Split(' ');
        Assert.Contains(expected, cls);
    }
}
