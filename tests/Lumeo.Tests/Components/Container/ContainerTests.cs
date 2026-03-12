using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Container;

public class ContainerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ContainerTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Default_Container()
    {
        var cut = _ctx.Render<Lumeo.Container>(p => p
            .AddChildContent("Content"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Default_Has_FullWidth_And_MaxWidth_lg()
    {
        var cut = _ctx.Render<Lumeo.Container>(p => p
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("w-full", cls);
        Assert.Contains("max-w-lg", cls);
        Assert.Contains("mx-auto", cls);
    }

    [Fact]
    public void MaxWidth_Parameter_Changes_Class()
    {
        var cut = _ctx.Render<Lumeo.Container>(p => p
            .Add(c => c.MaxWidth, "xl")
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("max-w-xl", cls);
    }

    [Fact]
    public void Center_False_Removes_MxAuto()
    {
        var cut = _ctx.Render<Lumeo.Container>(p => p
            .Add(c => c.Center, false)
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.DoesNotContain("mx-auto", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Container>(p => p
            .Add(c => c.Class, "my-container")
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-container", cls);
    }
}
