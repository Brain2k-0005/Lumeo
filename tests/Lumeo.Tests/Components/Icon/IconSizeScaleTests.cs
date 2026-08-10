using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Icon;

// Pins all 7 Lumeo.Size rungs for Icon's SizeClass. Icon already implemented
// Xs/Sm/Md/Lg/Xl before the full-scale rollout — only Xxs/Xxl are new; every
// rung asserts the exact space-delimited h-{n}/w-{n} tokens.
public class IconSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public IconSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Theory]
    [InlineData(L.Size.Xxs, "h-2.5", "w-2.5")]
    [InlineData(L.Size.Xs, "h-3", "w-3")]
    [InlineData(L.Size.Sm, "h-3.5", "w-3.5")]
    [InlineData(L.Size.Md, "h-4", "w-4")]
    [InlineData(L.Size.Lg, "h-5", "w-5")]
    [InlineData(L.Size.Xl, "h-6", "w-6")]
    [InlineData(L.Size.Xxl, "h-7", "w-7")]
    public void SizeClass_Per_Rung(L.Size size, string expectedH, string expectedW)
    {
        var cut = _ctx.Render<L.Icon>(p => p.Add(i => i.Name, "Search").Add(i => i.Size, size));
        var tokens = cut.Find("svg").GetAttribute("class")!.Split(' ');
        Assert.Contains(expectedH, tokens);
        Assert.Contains(expectedW, tokens);
    }
}
