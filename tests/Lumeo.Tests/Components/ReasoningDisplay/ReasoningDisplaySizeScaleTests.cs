using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ReasoningDisplay;

// Pins all 7 Lumeo.Size rungs for ReasoningDisplay's SummaryClass (gap+text)
// and BodyClass (mt/ps/py/text). Asserts rendered attributes, not source
// strings.
public class ReasoningDisplaySizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ReasoningDisplaySizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- SummaryClass: gap + text, all 7 rungs ---

    [Theory]
    [InlineData(L.Size.Xxs, "gap-0.5", "text-[9px]")]
    [InlineData(L.Size.Xs, "gap-1", "text-[10px]")]
    [InlineData(L.Size.Sm, "gap-1.5", "text-[11px]")]
    [InlineData(L.Size.Md, "gap-2", "text-xs")]
    [InlineData(L.Size.Lg, "gap-2.5", "text-sm")]
    [InlineData(L.Size.Xl, "gap-3", "text-base")]
    [InlineData(L.Size.Xxl, "gap-3.5", "text-lg")]
    public void SummaryClass_Per_Rung(L.Size size, string expectedGap, string expectedText)
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>(p => p.Add(r => r.Size, size));
        var cls = cut.Find("summary").GetAttribute("class")!.Split(' ');
        Assert.Contains(expectedGap, cls);
        Assert.Contains(expectedText, cls);
    }

    // --- BodyClass: mt/ps/py/text, all 7 rungs. py clamps to 0 at Xxs/Xs
    //     (raw arithmetic goes negative below Sm's py-0.5) — a deliberate,
    //     explicit tie, not a bug. ---

    [Theory]
    [InlineData(L.Size.Xxs, "mt-0.5", "ps-1", "py-0", "text-[9px]")]
    [InlineData(L.Size.Xs, "mt-1", "ps-2", "py-0", "text-[10px]")]
    [InlineData(L.Size.Sm, "mt-1.5", "ps-3", "py-0.5", "text-[11px]")]
    [InlineData(L.Size.Md, "mt-2", "ps-4", "py-1", "text-xs")]
    [InlineData(L.Size.Lg, "mt-2.5", "ps-5", "py-1.5", "text-sm")]
    [InlineData(L.Size.Xl, "mt-3", "ps-6", "py-2", "text-base")]
    [InlineData(L.Size.Xxl, "mt-3.5", "ps-7", "py-2.5", "text-lg")]
    public void BodyClass_Per_Rung(L.Size size, string expectedMt, string expectedPs, string expectedPy, string expectedText)
    {
        var cut = _ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(r => r.Size, size).Add(r => r.Text, "Step 1."));
        var cls = cut.Find("div.leading-relaxed").GetAttribute("class")!.Split(' ');
        Assert.Contains(expectedMt, cls);
        Assert.Contains(expectedPs, cls);
        Assert.Contains(expectedPy, cls);
        Assert.Contains(expectedText, cls);
    }

    [Fact]
    public void BodyClass_Py_Xxs_And_Xs_Legitimately_Tie_At_Py0()
    {
        var xxs = _ctx.Render<Lumeo.ReasoningDisplay>(p => p.Add(r => r.Size, L.Size.Xxs).Add(r => r.Text, "x"));
        var xs = _ctx.Render<Lumeo.ReasoningDisplay>(p => p.Add(r => r.Size, L.Size.Xs).Add(r => r.Text, "x"));

        var xxsCls = xxs.Find("div.leading-relaxed").GetAttribute("class")!.Split(' ');
        var xsCls = xs.Find("div.leading-relaxed").GetAttribute("class")!.Split(' ');

        Assert.Contains("py-0", xxsCls);
        Assert.Contains("py-0", xsCls);
    }
}
