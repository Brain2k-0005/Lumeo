using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Text;

public class TextTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TextTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Default_As_Paragraph()
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .AddChildContent("Hello"));

        Assert.NotNull(cut.Find("p"));
        Assert.Equal("Hello", cut.Find("p").TextContent);
    }

    [Fact]
    public void As_Span_Renders_Span_Element()
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.As, "span")
            .AddChildContent("Inline text"));

        Assert.NotNull(cut.Find("span"));
    }

    [Fact]
    public void As_Div_Renders_Div_Element()
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.As, "div")
            .AddChildContent("Block text"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Size_Parameter_Adds_Text_Size_Class()
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.Size, "lg")
            .AddChildContent("Text"));

        var cls = cut.Find("p").GetAttribute("class");
        Assert.Contains("text-lg", cls);
    }

    [Fact]
    public void Muted_Color_Adds_TextMuted_Class()
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.Color, "muted")
            .AddChildContent("Muted text"));

        var cls = cut.Find("p").GetAttribute("class");
        Assert.Contains("text-muted-foreground", cls);
    }

    [Fact]
    public void Truncate_True_Adds_Truncate_Class()
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.Truncate, true)
            .AddChildContent("Long text"));

        var cls = cut.Find("p").GetAttribute("class");
        Assert.Contains("truncate", cls);
    }

    // --- rc.21: full theme-token color map ---

    [Theory]
    [InlineData("foreground", "text-foreground")]
    [InlineData("muted", "text-muted-foreground")]
    [InlineData("primary", "text-primary")]
    [InlineData("destructive", "text-destructive")]
    [InlineData("success", "text-success")]
    [InlineData("warning", "text-warning")]
    [InlineData("info", "text-info")]
    [InlineData("accent", "text-accent-foreground")]
    public void Color_Token_Maps_To_Expected_Class(string token, string expectedClass)
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.Color, token)
            .AddChildContent("Tokenized"));

        var cls = cut.Find("p").GetAttribute("class");
        Assert.Contains(expectedClass, cls);
    }

    [Theory]
    [InlineData("MUTED", "text-muted-foreground")]
    [InlineData("Primary", "text-primary")]
    [InlineData("DeStRuCtIvE", "text-destructive")]
    public void Color_Token_Match_Is_Case_Insensitive(string token, string expectedClass)
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.Color, token)
            .AddChildContent("Mixed case"));

        var cls = cut.Find("p").GetAttribute("class");
        Assert.Contains(expectedClass, cls);
    }

    [Fact]
    public void Color_Default_Adds_No_Color_Class()
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.Color, "default")
            .AddChildContent("Default"));

        var cls = cut.Find("p").GetAttribute("class") ?? "";
        // No theme color class should be applied when "default" is requested.
        Assert.DoesNotContain("text-foreground", cls);
        Assert.DoesNotContain("text-muted-foreground", cls);
        Assert.DoesNotContain("text-primary", cls);
    }

    [Fact]
    public void Unknown_Color_Token_Falls_Back_To_No_Class_Without_Throwing()
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.Color, "totally-not-a-real-token")
            .AddChildContent("Unknown"));

        // Pre-rc.21 behaviour was to emit `text-totally-not-a-real-token` — rc.21
        // tightened the map so unknown tokens emit nothing rather than leaking
        // an invalid Tailwind class into the DOM.
        var cls = cut.Find("p").GetAttribute("class") ?? "";
        Assert.DoesNotContain("text-totally-not-a-real-token", cls);
    }

    // --- #294: widened semantic element set ---

    [Theory]
    [InlineData("em")]
    [InlineData("b")]
    [InlineData("i")]
    [InlineData("label")]
    [InlineData("mark")]
    public void As_Renders_Widened_Semantic_Elements(string tag)
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.As, tag)
            .AddChildContent("Semantic"));

        var el = cut.Find(tag);
        Assert.Equal("Semantic", el.TextContent);
    }

    // --- #294: LineClamp ---

    [Theory]
    [InlineData(1, "line-clamp-1")]
    [InlineData(2, "line-clamp-2")]
    [InlineData(3, "line-clamp-3")]
    public void LineClamp_In_Range_Adds_LineClamp_Class(int n, string expected)
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.LineClamp, n)
            .AddChildContent("Clamped"));

        var cls = cut.Find("p").GetAttribute("class");
        Assert.Contains(expected, cls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void LineClamp_Out_Of_Range_Adds_No_Class(int n)
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.LineClamp, n)
            .AddChildContent("Clamped"));

        var cls = cut.Find("p").GetAttribute("class") ?? "";
        Assert.DoesNotContain("line-clamp-", cls);
    }

    [Fact]
    public void Truncate_Wins_Over_LineClamp_When_Both_Set()
    {
        var cut = _ctx.Render<Lumeo.Text>(p => p
            .Add(t => t.Truncate, true)
            .Add(t => t.LineClamp, 2)
            .AddChildContent("Both"));

        var cls = cut.Find("p").GetAttribute("class") ?? "";
        Assert.Contains("truncate", cls);
        Assert.DoesNotContain("line-clamp-", cls);
    }
}
