using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ImageCompare;

public class ImageCompareTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ImageCompareTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/before.jpg")
            .Add(c => c.AfterSrc, "/after.jpg"));
        // The slider input should be present
        Assert.NotEmpty(cut.FindAll("input[type='range']"));
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.Class, "compare-cls"));
        Assert.Contains("compare-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "ic" }));
        Assert.Contains("data-testid=\"ic\"", cut.Markup);
    }

    [Fact]
    public void Renders_before_and_after_labels()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.BeforeLabel, "Before")
            .Add(c => c.AfterLabel, "After"));
        Assert.Contains("Before", cut.Markup);
        Assert.Contains("After", cut.Markup);
    }

    [Fact]
    public void Renders_vertical_orientation_slider()
    {
        var cut = _ctx.Render<L.ImageCompare>(p => p
            .Add(c => c.BeforeSrc, "/b.jpg")
            .Add(c => c.AfterSrc, "/a.jpg")
            .Add(c => c.Orientation, L.ImageCompare.ImageCompareOrientation.Vertical));
        Assert.Contains("ns-resize", cut.Markup);
    }
}
