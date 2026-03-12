using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Rating;

public class RatingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public RatingTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Default_Five_Stars()
    {
        var cut = _ctx.Render<Lumeo.Rating>();

        var buttons = cut.FindAll("button");
        Assert.Equal(5, buttons.Count);
    }

    [Fact]
    public void Max_Parameter_Changes_Star_Count()
    {
        var cut = _ctx.Render<Lumeo.Rating>(p => p
            .Add(r => r.Max, 10));

        var buttons = cut.FindAll("button");
        Assert.Equal(10, buttons.Count);
    }

    [Fact]
    public void Container_Has_Inline_Flex_Class()
    {
        var cut = _ctx.Render<Lumeo.Rating>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("inline-flex", cls);
        Assert.Contains("items-center", cls);
    }

    [Fact]
    public void ReadOnly_Disables_All_Buttons()
    {
        var cut = _ctx.Render<Lumeo.Rating>(p => p
            .Add(r => r.ReadOnly, true));

        var buttons = cut.FindAll("button");
        Assert.All(buttons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Rating>(p => p
            .Add(r => r.Class, "my-rating"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-rating", cls);
    }
}
