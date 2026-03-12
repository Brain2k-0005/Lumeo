using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Stack;

public class StackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public StackTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Default_Stack()
    {
        var cut = _ctx.Render<Lumeo.Stack>(p => p
            .AddChildContent("Content"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Default_Is_Vertical_Flex_Col()
    {
        var cut = _ctx.Render<Lumeo.Stack>(p => p
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("flex-col", cls);
    }

    [Fact]
    public void Horizontal_Direction_Uses_FlexRow()
    {
        var cut = _ctx.Render<Lumeo.Stack>(p => p
            .Add(s => s.Direction, Lumeo.Stack.StackDirection.Horizontal)
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex-row", cls);
    }

    [Fact]
    public void Gap_Parameter_Adds_Gap_Class()
    {
        var cut = _ctx.Render<Lumeo.Stack>(p => p
            .Add(s => s.Gap, "6")
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("gap-6", cls);
    }

    [Fact]
    public void Wrap_True_Adds_FlexWrap_Class()
    {
        var cut = _ctx.Render<Lumeo.Stack>(p => p
            .Add(s => s.Wrap, true)
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex-wrap", cls);
    }
}
