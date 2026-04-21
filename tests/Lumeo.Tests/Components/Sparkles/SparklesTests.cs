using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Sparkles;

public class SparklesTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SparklesTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Root_Span_With_Sparkles_Class()
    {
        var cut = _ctx.Render<Lumeo.Sparkles>();

        Assert.Contains("lumeo-sparkles", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Default_Count_Is_Five()
    {
        var cut = _ctx.Render<Lumeo.Sparkles>();

        var sparkles = cut.FindAll(".lumeo-sparkle");
        Assert.Equal(5, sparkles.Count);
    }

    [Fact]
    public void Count_Zero_Renders_No_Sparkles()
    {
        var cut = _ctx.Render<Lumeo.Sparkles>(p => p
            .Add(s => s.Count, 0));

        Assert.Empty(cut.FindAll(".lumeo-sparkle"));
    }

    [Fact]
    public void Count_Custom_Renders_That_Many()
    {
        var cut = _ctx.Render<Lumeo.Sparkles>(p => p
            .Add(s => s.Count, 10));

        Assert.Equal(10, cut.FindAll(".lumeo-sparkle").Count);
    }

    [Fact]
    public void Color_Is_Applied_To_Sparkle_Style()
    {
        var cut = _ctx.Render<Lumeo.Sparkles>(p => p
            .Add(s => s.Color, "red")
            .Add(s => s.Count, 1));

        var sparkle = cut.Find(".lumeo-sparkle");
        Assert.Contains("color:red", sparkle.GetAttribute("style"));
    }

    [Fact]
    public void ChildContent_Renders_Inside_Root()
    {
        var cut = _ctx.Render<Lumeo.Sparkles>(p => p
            .AddChildContent("<span data-testid='sc'>child</span>"));

        Assert.NotNull(cut.Find("[data-testid='sc']"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Sparkles>(p => p
            .Add(s => s.Class, "spk-x"));

        Assert.Contains("spk-x", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.Sparkles>(p => p
            .Add(s => s.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "spk"
            }));

        Assert.Equal("spk", cut.Find("span").GetAttribute("data-testid"));
    }
}
