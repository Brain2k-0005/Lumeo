using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.BlurFade;

public class BlurFadeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BlurFadeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.BlurFade>(p => p
            .AddChildContent("<span data-testid='c'>hello</span>"));

        Assert.NotNull(cut.Find("[data-testid='c']"));
    }

    [Fact]
    public void Root_Has_BlurFade_Class()
    {
        var cut = _ctx.Render<Lumeo.BlurFade>();

        Assert.Contains("lumeo-blur-fade", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void BlurPx_Applies_To_Css_Variable()
    {
        var cut = _ctx.Render<Lumeo.BlurFade>(p => p
            .Add(b => b.BlurPx, 16));

        Assert.Contains("--lumeo-blur-px: 16px", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void DurationMs_Applies_To_Css_Variable()
    {
        var cut = _ctx.Render<Lumeo.BlurFade>(p => p
            .Add(b => b.DurationMs, 1200));

        Assert.Contains("--lumeo-blur-duration: 1200ms", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Yoffset_Applies_To_Css_Variable()
    {
        var cut = _ctx.Render<Lumeo.BlurFade>(p => p
            .Add(b => b.Yoffset, 20));

        Assert.Contains("--lumeo-blur-y: 20px", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.BlurFade>(p => p
            .Add(b => b.Class, "bf-x"));

        Assert.Contains("bf-x", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.BlurFade>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "bf"
            }));

        Assert.Equal("bf", cut.Find("div").GetAttribute("data-testid"));
    }
}
