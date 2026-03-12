using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Timeline;

public class TimelineTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TimelineTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Default_Vertical_Timeline()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .AddChildContent("Timeline content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("flex-col", cls);
    }

    [Fact]
    public void Horizontal_Orientation_Uses_FlexRow()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Orientation, Lumeo.Timeline.TimelineOrientation.Horizontal)
            .AddChildContent("Timeline content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex-row", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Class, "my-timeline")
            .AddChildContent("Timeline content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-timeline", cls);
    }

    [Fact]
    public void Renders_Child_Content()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .AddChildContent("<span id='child'>Event</span>"));

        Assert.Equal("Event", cut.Find("#child").TextContent);
    }
}
