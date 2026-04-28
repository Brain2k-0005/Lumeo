using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.NumberCountUp;

public class NumberCountUpTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public NumberCountUpTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_root_span_with_class()
    {
        var cut = _ctx.Render<Lumeo.NumberCountUp>();
        Assert.Contains("lumeo-countup", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Renders_prefix()
    {
        var cut = _ctx.Render<Lumeo.NumberCountUp>(p => p
            .Add(c => c.Prefix, "$")
            .Add(c => c.Value, 100));
        Assert.Contains("$", cut.Markup);
    }

    [Fact]
    public void Renders_suffix()
    {
        var cut = _ctx.Render<Lumeo.NumberCountUp>(p => p
            .Add(c => c.Suffix, "K")
            .Add(c => c.Value, 10));
        Assert.Contains("K", cut.Markup);
    }

    [Fact]
    public void Custom_class_appended()
    {
        var cut = _ctx.Render<Lumeo.NumberCountUp>(p => p
            .Add(c => c.Class, "count-x"));
        Assert.Contains("count-x", cut.Find("span").GetAttribute("class"));
    }
}
