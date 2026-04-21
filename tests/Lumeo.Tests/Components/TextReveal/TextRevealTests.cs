using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.TextReveal;

public class TextRevealTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TextRevealTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Span_With_Reveal_Class()
    {
        var cut = _ctx.Render<Lumeo.TextReveal>(p => p
            .Add(t => t.Text, "Hello"));

        var span = cut.Find("span");
        Assert.Contains("lumeo-reveal", span.GetAttribute("class"));
    }

    [Fact]
    public void Splits_Text_Into_Word_Spans()
    {
        var cut = _ctx.Render<Lumeo.TextReveal>(p => p
            .Add(t => t.Text, "Hello world foo"));

        var wordSpans = cut.FindAll("[data-motion-word]");
        Assert.Equal(3, wordSpans.Count);
    }

    [Fact]
    public void Preserves_Word_Text()
    {
        var cut = _ctx.Render<Lumeo.TextReveal>(p => p
            .Add(t => t.Text, "Alpha Bravo Charlie"));

        var words = cut.FindAll("[data-motion-word]").Select(w => w.TextContent).ToList();
        Assert.Contains("Alpha", words);
        Assert.Contains("Bravo", words);
        Assert.Contains("Charlie", words);
    }

    [Fact]
    public void Empty_Text_Produces_No_Word_Spans()
    {
        var cut = _ctx.Render<Lumeo.TextReveal>(p => p
            .Add(t => t.Text, ""));

        Assert.Empty(cut.FindAll("[data-motion-word]"));
    }

    [Fact]
    public void Single_Word_Produces_One_Word_Span()
    {
        var cut = _ctx.Render<Lumeo.TextReveal>(p => p
            .Add(t => t.Text, "Solo"));

        Assert.Single(cut.FindAll("[data-motion-word]"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.TextReveal>(p => p
            .Add(t => t.Text, "x")
            .Add(t => t.Class, "reveal-x"));

        Assert.Contains("reveal-x", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.TextReveal>(p => p
            .Add(t => t.Text, "x")
            .Add(t => t.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "reveal"
            }));

        Assert.Equal("reveal", cut.Find("span").GetAttribute("data-testid"));
    }
}
