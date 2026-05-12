using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Toolbar;

public class ToolbarTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToolbarTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_With_Role_Toolbar()
    {
        var cut = _ctx.Render<Lumeo.Toolbar>(p => p
            .AddChildContent("toolbar content"));

        var el = cut.Find("[role='toolbar']");
        Assert.NotNull(el);
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<Lumeo.Toolbar>(p => p
            .Add(t => t.Class, "my-toolbar")
            .AddChildContent("content"));

        var cls = cut.Find("[role='toolbar']").GetAttribute("class");
        Assert.Contains("my-toolbar", cls);
    }

    [Fact]
    public void Renders_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.Toolbar>(p => p
            .AddChildContent("<span>Action</span>"));

        Assert.Contains("Action", cut.Markup);
    }

    [Fact]
    public void Separator_Has_Separator_Role()
    {
        var cut = _ctx.Render<Lumeo.ToolbarSeparator>();

        var el = cut.Find("[role='separator']");
        Assert.NotNull(el);
    }

    [Fact]
    public void Spacer_Has_Flex1_Class()
    {
        var cut = _ctx.Render<Lumeo.ToolbarSpacer>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex-1", cls);
    }

    [Fact]
    public void Group_Renders_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.ToolbarGroup>(p => p
            .AddChildContent("<span>grouped</span>"));

        Assert.Contains("grouped", cut.Markup);
    }
}
