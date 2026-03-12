using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Spacer;

public class SpacerTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SpacerTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Default_Spacer_With_Flex1()
    {
        var cut = _ctx.Render<Lumeo.Spacer>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex-1", cls);
    }

    [Fact]
    public void Size_Parameter_Adds_Width_And_Height_Classes()
    {
        var cut = _ctx.Render<Lumeo.Spacer>(p => p
            .Add(s => s.Size, "8"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("w-8", cls);
        Assert.Contains("h-8", cls);
    }

    [Fact]
    public void Size_Parameter_Does_Not_Add_Flex1()
    {
        var cut = _ctx.Render<Lumeo.Spacer>(p => p
            .Add(s => s.Size, "4"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.DoesNotContain("flex-1", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Spacer>(p => p
            .Add(s => s.Class, "my-spacer"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-spacer", cls);
    }
}
