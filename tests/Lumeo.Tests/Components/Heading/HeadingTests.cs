using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Heading;

public class HeadingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public HeadingTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Default_Level2_Renders_H2()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .AddChildContent("My Heading"));

        Assert.NotNull(cut.Find("h2"));
        Assert.Equal("My Heading", cut.Find("h2").TextContent);
    }

    [Fact]
    public void Level1_Renders_H1()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .Add(h => h.Level, 1)
            .AddChildContent("H1 Heading"));

        Assert.NotNull(cut.Find("h1"));
    }

    [Fact]
    public void Level3_Renders_H3()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .Add(h => h.Level, 3)
            .AddChildContent("H3 Heading"));

        Assert.NotNull(cut.Find("h3"));
    }

    [Fact]
    public void Default_Includes_Text_Foreground_Class()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .AddChildContent("Heading"));

        var cls = cut.Find("h2").GetAttribute("class");
        Assert.Contains("text-foreground", cls);
    }

    [Fact]
    public void Level1_Defaults_To_Bold_And_Tight_Tracking()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .Add(h => h.Level, 1)
            .AddChildContent("H1"));

        var cls = cut.Find("h1").GetAttribute("class");
        Assert.Contains("font-bold", cls);
        Assert.Contains("tracking-tight", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .Add(h => h.Class, "my-heading")
            .AddChildContent("Heading"));

        var cls = cut.Find("h2").GetAttribute("class");
        Assert.Contains("my-heading", cls);
    }

    // Battle-wave3 bug #44 (edge-data): a whitespace-only `As` passed the
    // IsNullOrEmpty guard and ran RenderAs -> builder.OpenElement(0, "   "),
    // emitting an element with a blank/invalid tag name. Whitespace `As`
    // must be treated as absent and fall through to the semantic h{Level}.
    [Fact]
    public void Whitespace_As_Falls_Through_To_Semantic_Heading_Tag()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .Add(h => h.As, "   ")
            .AddChildContent("Section title"));

        // Default Level is 2 -> a real <h2>, not a blank-tag element.
        var h2 = cut.Find("h2");
        Assert.Equal("Section title", h2.TextContent);
    }

    // Guard the normal path: a genuine, non-blank `As` still overrides the tag.
    [Fact]
    public void NonBlank_As_Renders_The_Custom_Tag()
    {
        var cut = _ctx.Render<Lumeo.Heading>(p => p
            .Add(h => h.As, "p")
            .AddChildContent("Looks like a heading"));

        Assert.NotNull(cut.Find("p"));
        Assert.Empty(cut.FindAll("h2"));
    }
}
