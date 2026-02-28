using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Resizable;

public class ResizableTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- ResizablePanelGroup ---

    [Fact]
    public void PanelGroup_Renders_Div()
    {
        var cut = _ctx.Render<L.ResizablePanelGroup>(p => p
            .AddChildContent("content"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void PanelGroup_Default_Direction_Is_Horizontal()
    {
        var cut = _ctx.Render<L.ResizablePanelGroup>(p => p
            .AddChildContent("content"));

        var div = cut.Find("div");
        Assert.Equal("horizontal", div.GetAttribute("data-panel-group-direction"));
    }

    [Fact]
    public void PanelGroup_Vertical_Direction_Sets_Attribute()
    {
        var cut = _ctx.Render<L.ResizablePanelGroup>(p => p
            .Add(b => b.Direction, L.ResizablePanelGroup.ResizableDirection.Vertical)
            .AddChildContent("content"));

        var div = cut.Find("div");
        Assert.Equal("vertical", div.GetAttribute("data-panel-group-direction"));
    }

    [Fact]
    public void PanelGroup_Horizontal_Has_Flex_Class()
    {
        var cut = _ctx.Render<L.ResizablePanelGroup>(p => p
            .Add(b => b.Direction, L.ResizablePanelGroup.ResizableDirection.Horizontal)
            .AddChildContent("content"));

        var div = cut.Find("div");
        var cls = div.GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.DoesNotContain("flex-col", cls);
    }

    [Fact]
    public void PanelGroup_Vertical_Has_FlexCol_Class()
    {
        var cut = _ctx.Render<L.ResizablePanelGroup>(p => p
            .Add(b => b.Direction, L.ResizablePanelGroup.ResizableDirection.Vertical)
            .AddChildContent("content"));

        var div = cut.Find("div");
        Assert.Contains("flex-col", div.GetAttribute("class"));
    }

    [Fact]
    public void PanelGroup_Has_Id_Attribute()
    {
        var cut = _ctx.Render<L.ResizablePanelGroup>(p => p
            .AddChildContent("content"));

        var div = cut.Find("div");
        var id = div.GetAttribute("id");
        Assert.NotNull(id);
        Assert.StartsWith("resizable-group-", id);
    }

    [Fact]
    public void PanelGroup_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.ResizablePanelGroup>(p => p
            .Add(b => b.Class, "my-group")
            .AddChildContent("content"));

        var div = cut.Find("div");
        Assert.Contains("my-group", div.GetAttribute("class"));
        Assert.Contains("flex", div.GetAttribute("class"));
    }

    [Fact]
    public void PanelGroup_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.ResizablePanelGroup>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "panel-group"
            })
            .AddChildContent("content"));

        Assert.Equal("panel-group", cut.Find("div").GetAttribute("data-testid"));
    }

    // --- ResizableDirection enum ---

    [Fact]
    public void ResizableDirection_Enum_Has_Horizontal_And_Vertical()
    {
        var values = Enum.GetValues<L.ResizablePanelGroup.ResizableDirection>();
        Assert.Contains(L.ResizablePanelGroup.ResizableDirection.Horizontal, values);
        Assert.Contains(L.ResizablePanelGroup.ResizableDirection.Vertical, values);
    }
}
