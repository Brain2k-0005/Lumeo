using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AnimatedSubscribeButton;

public class AnimatedSubscribeButtonTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AnimatedSubscribeButtonTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_button_with_class()
    {
        var cut = _ctx.Render<Lumeo.AnimatedSubscribeButton>();
        Assert.Contains("lumeo-subscribe-button", cut.Find("button").GetAttribute("class"));
    }

    [Fact]
    public void Shows_idle_label_by_default()
    {
        var cut = _ctx.Render<Lumeo.AnimatedSubscribeButton>(p => p
            .Add(c => c.IdleLabel, "Subscribe"));
        Assert.Contains("Subscribe", cut.Markup);
    }

    [Fact]
    public void Click_transitions_to_loading()
    {
        var cut = _ctx.Render<Lumeo.AnimatedSubscribeButton>(p => p
            .Add(c => c.IdleLabel, "Subscribe")
            .Add(c => c.LoadingLabel, "Subscribing…"));
        cut.Find("button").Click();
        Assert.Contains("Subscribing…", cut.Markup);
    }

    [Fact]
    public void Custom_class_appended()
    {
        var cut = _ctx.Render<Lumeo.AnimatedSubscribeButton>(p => p
            .Add(c => c.Class, "sub-x"));
        Assert.Contains("sub-x", cut.Find("button").GetAttribute("class"));
    }
}
