using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Window;

public class WindowTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public WindowTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Does_Not_Render_When_Closed()
    {
        var cut = _ctx.Render<Lumeo.Window>(p => p
            .Add(w => w.Open, false)
            .Add(w => w.Title, "My Window"));

        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void Renders_When_Open()
    {
        var cut = _ctx.Render<Lumeo.Window>(p => p
            .Add(w => w.Open, true)
            .Add(w => w.Title, "My Window")
            .AddChildContent("Window body content"));

        Assert.NotEmpty(cut.FindAll("[role='dialog']"));
        Assert.Contains("My Window", cut.Markup);
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<Lumeo.Window>(p => p
            .Add(w => w.Open, true)
            .Add(w => w.Title, "Test")
            .Add(w => w.Class, "custom-window")
            .AddChildContent("content"));

        Assert.Contains("custom-window", cut.Markup);
    }

    [Fact]
    public void Shows_Close_Button_By_Default()
    {
        var cut = _ctx.Render<Lumeo.Window>(p => p
            .Add(w => w.Open, true)
            .Add(w => w.Title, "Test Window")
            .AddChildContent("body"));

        var closeButtons = cut.FindAll("button[aria-label='Close']");
        Assert.NotEmpty(closeButtons);
    }

    [Fact]
    public void Does_Not_Show_Close_When_ShowClose_False()
    {
        var cut = _ctx.Render<Lumeo.Window>(p => p
            .Add(w => w.Open, true)
            .Add(w => w.Title, "Test Window")
            .Add(w => w.ShowClose, false)
            .AddChildContent("body"));

        var closeButtons = cut.FindAll("button[aria-label='Close']");
        Assert.Empty(closeButtons);
    }

    [Fact]
    public void Title_Is_Rendered_In_Header()
    {
        var cut = _ctx.Render<Lumeo.Window>(p => p
            .Add(w => w.Open, true)
            .Add(w => w.Title, "Hello World")
            .AddChildContent("body"));

        Assert.Contains("Hello World", cut.Markup);
    }
}
