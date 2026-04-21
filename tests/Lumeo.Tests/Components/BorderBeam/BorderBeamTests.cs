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
    [InlineData(Lumeo.BorderBeam.BeamSize.Sm, "1px")]
    [InlineData(Lumeo.BorderBeam.BeamSize.Md, "1.5px")]
    [InlineData(Lumeo.BorderBeam.BeamSize.Lg, "3px")]
    public void Size_Maps_To_Beam_Size_Variable(Lumeo.BorderBeam.BeamSize size, string expectedPx)
    {
        var cut = _ctx.Render<Lumeo.BorderBeam>(p => p
            .Add(b => b.Size, size));

        Assert.Contains($"--lumeo-beam-size: {expectedPx}", cut.Find("div").GetAttribute("style"));
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
