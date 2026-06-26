using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Hero;

public class HeroTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public HeroTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Title_Renders_In_An_H1_That_Labels_The_Section()
    {
        var cut = _ctx.Render<L.Hero>(p => p.Add(h => h.Title, "Build faster"));

        var h1 = cut.Find("h1");
        Assert.Equal("Build faster", h1.TextContent.Trim());
        // The section is labelled by the heading (WAI landmark naming).
        Assert.Equal(h1.GetAttribute("id"), cut.Find("section").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Subtitle_Renders()
    {
        var cut = _ctx.Render<L.Hero>(p => p
            .Add(h => h.Title, "T")
            .Add(h => h.Subtitle, "A short description"));
        Assert.Contains("A short description", cut.Markup);
    }
}
