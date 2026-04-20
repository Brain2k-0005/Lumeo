using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.StreamingText;

public class StreamingTextTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StreamingTextTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Initial_Text()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Text, "Hello"));

        Assert.Contains("Hello", cut.Markup);
    }

    [Fact]
    public void Empty_Text_Renders_No_Content_Spans()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Text, ""));

        // Only the outer span — no alreadyRendered or newSuffix inner spans
        Assert.Empty(cut.FindAll("span > span"));
    }

    [Fact]
    public void IsStreaming_True_Renders_Animated_Caret()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Text, "x")
            .Add(s => s.IsStreaming, true));

        Assert.Contains("animate-pulse", cut.Markup);
    }

    [Fact]
    public void IsStreaming_False_Does_Not_Render_Caret()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Text, "x")
            .Add(s => s.IsStreaming, false));

        Assert.DoesNotContain("animate-pulse", cut.Markup);
    }

    [Fact]
    public void Appending_Text_Emits_Fade_In_Span_On_Suffix()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Text, "Hello"));

        // Rerender with longer text
        cut.Render(p => p
            .Add(s => s.Text, "Hello World"));

        // The new suffix " World" should be in a fade-in span
        Assert.Contains("fade-in", cut.Markup);
        Assert.Contains("World", cut.Markup);
    }

    [Fact]
    public void Shrinking_Text_Resets_Diff_Window()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Text, "Hello World"));

        cut.Render(p => p
            .Add(s => s.Text, "Hi"));

        Assert.Contains("Hi", cut.Markup);
        Assert.DoesNotContain("World", cut.Markup);
    }

    [Fact]
    public void Prose_Adds_Prose_Classes()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Text, "x")
            .Add(s => s.Prose, true));

        Assert.Contains("prose", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Default_Has_Whitespace_Pre_Wrap()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Text, "x"));

        Assert.Contains("whitespace-pre-wrap", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Text, "x")
            .Add(s => s.Class, "st-x"));

        Assert.Contains("st-x", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Text, "x")
            .Add(s => s.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "st"
            }));

        Assert.Equal("st", cut.Find("span").GetAttribute("data-testid"));
    }
}
