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

    [Fact]
    public void Whitespace_Title_Renders_No_H1_And_No_Empty_Section_Name()
    {
        // Bug #45 (edge-data): a whitespace-only Title used IsNullOrEmpty, so it
        // passed the guard and rendered an empty <h1> while pointing the section's
        // aria-labelledby at it — giving the landmark an empty accessible name.
        var cut = _ctx.Render<L.Hero>(p => p.Add(h => h.Title, "   "));

        // No heading should be emitted for a blank title.
        Assert.Empty(cut.FindAll("h1"));
        // The section must not be labelled by an empty heading.
        Assert.Null(cut.Find("section").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Whitespace_Subtitle_Renders_No_Paragraph()
    {
        // Bug #45 (edge-data): a whitespace-only Subtitle previously rendered an empty <p>.
        var cut = _ctx.Render<L.Hero>(p => p
            .Add(h => h.Title, "T")
            .Add(h => h.Subtitle, "   "));

        Assert.Empty(cut.FindAll("p"));
    }
}
