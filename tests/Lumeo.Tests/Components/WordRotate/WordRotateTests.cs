using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.WordRotate;

public class WordRotateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public WordRotateTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_span_with_class()
    {
        var cut = _ctx.Render<Lumeo.WordRotate>(p => p
            .Add(c => c.Words, new[] { "Hello", "World" }));
        Assert.Contains("lumeo-word-rotate", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Shows_first_word_on_render()
    {
        var cut = _ctx.Render<Lumeo.WordRotate>(p => p
            .Add(c => c.Words, new[] { "Hello", "World" }));
        Assert.Contains("Hello", cut.Markup);
    }

    [Fact]
    public void Custom_class_appended()
    {
        var cut = _ctx.Render<Lumeo.WordRotate>(p => p
            .Add(c => c.Words, new[] { "A" })
            .Add(c => c.Class, "my-class"));
        Assert.Contains("my-class", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Empty_words_renders_empty_span()
    {
        var cut = _ctx.Render<Lumeo.WordRotate>(p => p
            .Add(c => c.Words, Array.Empty<string>()));
        Assert.NotNull(cut.Find("span"));
    }
}
