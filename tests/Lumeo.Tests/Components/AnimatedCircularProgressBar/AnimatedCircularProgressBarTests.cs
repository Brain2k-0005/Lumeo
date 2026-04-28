using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AnimatedCircularProgressBar;

public class AnimatedCircularProgressBarTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AnimatedCircularProgressBarTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_div_with_class()
    {
        var cut = _ctx.Render<Lumeo.AnimatedCircularProgressBar>();
        Assert.Contains("lumeo-circular-progress", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Shows_percentage_label_at_50()
    {
        var cut = _ctx.Render<Lumeo.AnimatedCircularProgressBar>(p => p
            .Add(c => c.Value, 50.0));
        Assert.Contains("50%", cut.Markup);
    }

    [Fact]
    public void Shows_zero_percent_by_default()
    {
        var cut = _ctx.Render<Lumeo.AnimatedCircularProgressBar>();
        Assert.Contains("0%", cut.Markup);
    }

    [Fact]
    public void Custom_size_applied_to_svg()
    {
        var cut = _ctx.Render<Lumeo.AnimatedCircularProgressBar>(p => p
            .Add(c => c.Size, 200));
        Assert.Contains("width:200px", cut.Markup);
    }
}
