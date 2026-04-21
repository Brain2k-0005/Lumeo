using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AgentMessageList;

public class AgentMessageListTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AgentMessageListTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Root_Div_With_Scroll_Container_Classes()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("flex-col", cls);
        Assert.Contains("overflow-y-auto", cls);
    }

    [Fact]
    public void ChildContent_Wrapped_In_Flex_Col_Gap()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .AddChildContent("<span data-testid='msg'>m</span>"));

        var inner = cut.Find("div > div");
        var cls = inner.GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("flex-col", cls);
        Assert.Contains("gap-4", cls);
    }

    [Fact]
    public void ChildContent_Renders()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .AddChildContent("<span data-testid='m'>msg</span>"));

        Assert.NotNull(cut.Find("[data-testid='m']"));
    }

    [Fact]
    public void AutoScroll_Default_Is_True()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>();

        Assert.True(cut.Instance.AutoScroll);
    }

    [Fact]
    public void AutoScroll_Can_Be_Set_False()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.AutoScroll, false));

        Assert.False(cut.Instance.AutoScroll);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.Class, "aml-x"));

        Assert.Contains("aml-x", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "aml"
            }));

        Assert.Equal("aml", cut.Find("div").GetAttribute("data-testid"));
    }
}
