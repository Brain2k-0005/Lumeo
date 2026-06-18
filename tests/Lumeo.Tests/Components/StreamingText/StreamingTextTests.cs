using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

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

    // ── #306: markdown rendering + meaningful Prose ──────────────────────────

    [Fact]
    public void Markdown_Renders_Bold_As_Strong_Element()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Markdown, true)
            .Add(s => s.Text, "Hello **world**"));

        var strong = cut.Find("strong");
        Assert.Equal("world", strong.TextContent);
    }

    [Fact]
    public void Markdown_Renders_Italic_And_Inline_Code()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Markdown, true)
            .Add(s => s.Text, "an *emphasised* `code` span"));

        Assert.Equal("emphasised", cut.Find("em").TextContent);
        Assert.Equal("code", cut.Find("code").TextContent);
    }

    [Fact]
    public void Markdown_Renders_Unordered_List()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Markdown, true)
            .Add(s => s.Text, "- one\n- two"));

        var items = cut.FindAll("ul li");
        Assert.Equal(2, items.Count);
        Assert.Equal("one", items[0].TextContent);
    }

    [Fact]
    public void Markdown_Renders_Fenced_Code_Block()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Markdown, true)
            .Add(s => s.Text, "```\nlet x = 1;\n```"));

        Assert.Contains("let x = 1;", cut.Find("pre code").TextContent);
    }

    [Fact]
    public void Markdown_Escapes_Raw_Html_In_Source()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Markdown, true)
            .Add(s => s.Text, "<script>alert(1)</script>"));

        // The script tag must be neutralised — no live <script> element.
        Assert.Empty(cut.FindAll("script"));
        Assert.Contains("alert(1)", cut.Markup);
    }

    [Fact]
    public void Markdown_Drops_Javascript_Link_Scheme()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Markdown, true)
            .Add(s => s.Text, "[click](javascript:alert(1))"));

        // Unsafe scheme → no anchor emitted, label preserved.
        Assert.Empty(cut.FindAll("a"));
        Assert.Contains("click", cut.Markup);
    }

    [Fact]
    public void Markdown_Allows_Https_Link()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Markdown, true)
            .Add(s => s.Text, "[lumeo](https://example.com)"));

        var a = cut.Find("a");
        Assert.Equal("https://example.com", a.GetAttribute("href"));
    }

    [Fact]
    public void MarkdownRenderer_Hook_Overrides_Builtin()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Markdown, true)
            .Add(s => s.MarkdownRenderer, raw => (MarkupString)$"<aside>{raw.ToUpperInvariant()}</aside>")
            .Add(s => s.Text, "hi"));

        Assert.Equal("HI", cut.Find("aside").TextContent);
    }

    [Fact]
    public void Markdown_Mode_Renders_Div_Root_With_Prose()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Markdown, true)
            .Add(s => s.Prose, true)
            .Add(s => s.Text, "text"));

        var root = cut.Find("div");
        Assert.Contains("prose", root.GetAttribute("class"));
        // whitespace-pre-wrap must NOT be applied in markdown mode (would break blocks).
        Assert.DoesNotContain("whitespace-pre-wrap", root.GetAttribute("class"));
    }

    [Fact]
    public void Markdown_Mode_Shows_Streaming_Caret()
    {
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p
            .Add(s => s.Markdown, true)
            .Add(s => s.IsStreaming, true)
            .Add(s => s.Text, "x"));

        Assert.Contains("animate-pulse", cut.Markup);
    }

    [Fact]
    public void Plain_Mode_Default_Keeps_Whitespace_Pre_Wrap()
    {
        // Regression: the non-markdown path must keep its original classes.
        var cut = _ctx.Render<Lumeo.StreamingText>(p => p.Add(s => s.Text, "x"));

        Assert.Contains("whitespace-pre-wrap", cut.Find("span").GetAttribute("class"));
    }
}
