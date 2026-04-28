using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.TypingAnimation;

public class TypingAnimationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TypingAnimationTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Span_With_TypingAnimation_Class()
    {
        var cut = _ctx.Render<Lumeo.TypingAnimation>(p => p
            .Add(t => t.Text, "Hello"));

        Assert.Contains("lumeo-typing-animation", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Aria_Label_Contains_Full_Text()
    {
        var cut = _ctx.Render<Lumeo.TypingAnimation>(p => p
            .Add(t => t.Text, "Hello World"));

        Assert.Equal("Hello World", cut.Find("span").GetAttribute("aria-label"));
    }

    [Fact]
    public void ShowCursor_True_Renders_Cursor_Span()
    {
        var cut = _ctx.Render<Lumeo.TypingAnimation>(p => p
            .Add(t => t.Text, "Hi")
            .Add(t => t.ShowCursor, true)
            .Add(t => t.IntervalMs, 99999)); // slow so typing doesn't complete

        Assert.NotNull(cut.Find(".lumeo-typing-cursor"));
    }

    [Fact]
    public void ShowCursor_False_Hides_Cursor()
    {
        var cut = _ctx.Render<Lumeo.TypingAnimation>(p => p
            .Add(t => t.Text, "Hi")
            .Add(t => t.ShowCursor, false));

        Assert.Empty(cut.FindAll(".lumeo-typing-cursor"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.TypingAnimation>(p => p
            .Add(t => t.Text, "Test")
            .Add(t => t.Class, "text-primary"));

        Assert.Contains("text-primary", cut.Find("span").GetAttribute("class"));
    }
}
