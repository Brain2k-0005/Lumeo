using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.MorphingText;

public class MorphingTextTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public MorphingTextTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_div_with_class()
    {
        var cut = _ctx.Render<Lumeo.MorphingText>(p => p
            .Add(c => c.From, "Hello")
            .Add(c => c.To, "World"));
        Assert.Contains("lumeo-morphing", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Shows_from_text_on_initial_render()
    {
        var cut = _ctx.Render<Lumeo.MorphingText>(p => p
            .Add(c => c.From, "Hello")
            .Add(c => c.To, "World"));
        Assert.Contains("Hello", cut.Markup);
    }

    [Fact]
    public void Renders_svg_filter()
    {
        var cut = _ctx.Render<Lumeo.MorphingText>(p => p
            .Add(c => c.From, "A")
            .Add(c => c.To, "B"));
        Assert.Contains("<filter", cut.Markup);
    }

    [Fact]
    public void Custom_class_appended()
    {
        var cut = _ctx.Render<Lumeo.MorphingText>(p => p
            .Add(c => c.From, "A")
            .Add(c => c.To, "B")
            .Add(c => c.Class, "my-morph"));
        Assert.Contains("my-morph", cut.Find("div").GetAttribute("class"));
    }
}
