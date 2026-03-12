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
}
