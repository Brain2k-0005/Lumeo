using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.BorderBeam;

public class BorderBeamTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BorderBeamTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.BorderBeam>(p => p
            .AddChildContent("<span data-testid='x'>child</span>"));

        Assert.NotNull(cut.Find("[data-testid='x']"));
    }

    [Fact]
    public void Root_Has_BorderBeam_Class()
    {
        var cut = _ctx.Render<Lumeo.BorderBeam>();

        Assert.Contains("lumeo-border-beam", cut.Find("div").GetAttribute("class"));
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "0.5px")]
    [InlineData(Lumeo.Size.Xs, "0.75px")]
    [InlineData(Lumeo.Size.Sm, "1px")]
    [InlineData(Lumeo.Size.Md, "1.5px")]
    [InlineData(Lumeo.Size.Lg, "3px")]
    [InlineData(Lumeo.Size.Xl, "4px")]
    [InlineData(Lumeo.Size.Xxl, "5px")]
    public void Size_Maps_To_Beam_Size_Variable(Lumeo.Size size, string expectedPx)
    {
        var cut = _ctx.Render<Lumeo.BorderBeam>(p => p
            .Add(b => b.Size, size));

        Assert.Contains($"--lumeo-beam-size: {expectedPx}", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Size_Sm_Md_Lg_Render_Byte_Identical_To_Pre_Migration_Values()
    {
        // Disable-check precedent: this is the pinned pre-migration mapping
        // (Sm=1px, Md=1.5px, Lg=3px) — a regression here would silently
        // change every existing consumer's beam thickness.
        string PxFor(Lumeo.Size size)
        {
            var cut = _ctx.Render<Lumeo.BorderBeam>(p => p.Add(b => b.Size, size));
            var style = cut.Find("div").GetAttribute("style") ?? "";
            var m = System.Text.RegularExpressions.Regex.Match(style, @"--lumeo-beam-size:\s*([0-9.]+px)");
            return m.Success ? m.Groups[1].Value : "";
        }

        Assert.Equal("1px", PxFor(Lumeo.Size.Sm));
        Assert.Equal("1.5px", PxFor(Lumeo.Size.Md));
        Assert.Equal("3px", PxFor(Lumeo.Size.Lg));
    }

    [Fact]
    public void DurationMs_Applies_To_Variable()
    {
        var cut = _ctx.Render<Lumeo.BorderBeam>(p => p
            .Add(b => b.DurationMs, 5000));

        Assert.Contains("--lumeo-beam-duration: 5000ms", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void ColorFrom_Applies_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.BorderBeam>(p => p
            .Add(b => b.ColorFrom, "red"));

        Assert.Contains("--lumeo-beam-from: red", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void ColorTo_Applies_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.BorderBeam>(p => p
            .Add(b => b.ColorTo, "blue"));

        Assert.Contains("--lumeo-beam-to: blue", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.BorderBeam>(p => p
            .Add(b => b.Class, "bb-x"));

        Assert.Contains("bb-x", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.BorderBeam>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "bb"
            }));

        Assert.Equal("bb", cut.Find("div").GetAttribute("data-testid"));
    }
}
