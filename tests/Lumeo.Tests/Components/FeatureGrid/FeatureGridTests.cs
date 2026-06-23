using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.FeatureGrid;

public class FeatureGridTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public FeatureGridTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Title_Renders_In_An_H2_That_Labels_The_Section()
    {
        var cut = _ctx.Render<L.FeatureGrid>(p => p.Add(f => f.Title, "Why us"));

        var h2 = cut.Find("h2");
        Assert.Equal("Why us", h2.TextContent.Trim());
        Assert.Equal(h2.GetAttribute("id"), cut.Find("section").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Child_Feature_Content_Renders_In_The_Grid()
    {
        var cut = _ctx.Render<L.FeatureGrid>(p => p
            .Add(f => f.Title, "T")
            .AddChildContent("<div>Feature A</div>"));
        Assert.Contains("Feature A", cut.Markup);
        Assert.Contains("grid", cut.Markup);
    }
}
