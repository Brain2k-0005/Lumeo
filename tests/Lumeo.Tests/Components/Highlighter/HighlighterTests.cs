using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Highlighter;

public class HighlighterTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public HighlighterTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Plain_Text_When_No_Highlight_Term()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "Hello World"));

        Assert.Contains("Hello World", cut.Markup);
        Assert.Empty(cut.FindAll("mark"));
    }

    [Fact]
    public void Wraps_Matching_Substring_In_Mark()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "Hello World")
            .Add(h => h.Highlight, "World"));

        var marks = cut.FindAll("mark");
        Assert.Single(marks);
        Assert.Equal("World", marks[0].TextContent);
    }

    [Fact]
    public void Case_Insensitive_By_Default()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "Hello World")
            .Add(h => h.Highlight, "hello"));

        Assert.NotEmpty(cut.FindAll("mark"));
    }

    [Fact]
    public void Case_Sensitive_Mode_Does_Not_Match_Wrong_Case()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "Hello World")
            .Add(h => h.Highlight, "hello")
            .Add(h => h.CaseSensitive, true));

        Assert.Empty(cut.FindAll("mark"));
    }

    [Fact]
    public void Case_Sensitive_Mode_Matches_Correct_Case()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "Hello World")
            .Add(h => h.Highlight, "Hello")
            .Add(h => h.CaseSensitive, true));

        Assert.Single(cut.FindAll("mark"));
    }

    [Fact]
    public void Multiple_Terms_Via_HighlightTerms()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "The quick brown fox jumps")
            .Add(h => h.HighlightTerms, new[] { "quick", "fox" }));

        var marks = cut.FindAll("mark");
        Assert.Equal(2, marks.Count);
    }

    [Fact]
    public void Empty_Text_Renders_Nothing()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "")
            .Add(h => h.Highlight, "test"));

        Assert.Empty(cut.FindAll("mark"));
        // The container span should be present but empty (whitespace only)
        Assert.NotNull(cut.Find("span"));
    }

    [Fact]
    public void Custom_Class_Applied_To_Container()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "Hello")
            .Add(h => h.Class, "my-highlighter"));

        var span = cut.Find("span");
        Assert.Contains("my-highlighter", span.GetAttribute("class") ?? "");
    }

    [Fact]
    public void Highlight_Class_Applied_To_Mark_Element()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "Hello World")
            .Add(h => h.Highlight, "World")
            .Add(h => h.HighlightClass, "custom-highlight"));

        var mark = cut.Find("mark");
        Assert.Contains("custom-highlight", mark.GetAttribute("class") ?? "");
    }

    [Fact]
    public void WholeWord_True_Does_Not_Match_Partial_Word()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "highlight highlighter")
            .Add(h => h.Highlight, "highlight")
            .Add(h => h.WholeWord, true));

        // Should only match the standalone "highlight" not "highlighter"
        var marks = cut.FindAll("mark");
        Assert.Single(marks);
        Assert.Equal("highlight", marks[0].TextContent);
    }

    [Fact]
    public void Multiple_Occurrences_All_Highlighted()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "cat and cat and cat")
            .Add(h => h.Highlight, "cat"));

        var marks = cut.FindAll("mark");
        Assert.Equal(3, marks.Count);
    }

    [Fact]
    public void Non_Matching_Parts_Preserved_As_Text()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "Hello World")
            .Add(h => h.Highlight, "World"));

        Assert.Contains("Hello ", cut.Markup);
    }

    [Fact]
    public void Regex_Special_Chars_In_Term_Do_Not_Throw()
    {
        var cut = _ctx.Render<Lumeo.Highlighter>(p => p
            .Add(h => h.Text, "Hello (World)")
            .Add(h => h.Highlight, "(World)"));

        // Should not throw; "(World)" should be escaped and matched literally
        Assert.NotEmpty(cut.FindAll("mark"));
    }
}
