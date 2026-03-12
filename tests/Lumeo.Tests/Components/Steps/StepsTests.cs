using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Steps;

public class StepsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StepsTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Default_Horizontal_Steps()
    {
        var cut = _ctx.Render<Lumeo.Steps>(p => p
            .AddChildContent("Steps content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("flex-row", cls);
    }

    [Fact]
    public void Vertical_Orientation_Uses_FlexCol()
    {
        var cut = _ctx.Render<Lumeo.Steps>(p => p
            .Add(s => s.Orientation, Lumeo.Steps.StepsOrientation.Vertical)
            .AddChildContent("Steps content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex-col", cls);
    }

    [Fact]
    public void Has_Role_List_Attribute()
    {
        var cut = _ctx.Render<Lumeo.Steps>(p => p
            .AddChildContent("Steps content"));

        Assert.Equal("list", cut.Find("div").GetAttribute("role"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Steps>(p => p
            .Add(s => s.Class, "my-steps")
            .AddChildContent("Steps content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-steps", cls);
    }
}
