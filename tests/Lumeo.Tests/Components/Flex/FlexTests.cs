using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Flex;

public class FlexTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FlexTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Default_Flex()
    {
        var cut = _ctx.Render<Lumeo.Flex>(p => p
            .AddChildContent("Content"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Default_Has_Flex_And_Row_Direction()
    {
        var cut = _ctx.Render<Lumeo.Flex>(p => p
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("flex-row", cls);
    }

    [Fact]
    public void Inline_True_Uses_InlineFlex()
    {
        var cut = _ctx.Render<Lumeo.Flex>(p => p
            .Add(f => f.Inline, true)
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("inline-flex", cls);
    }

    [Fact]
    public void Gap_Parameter_Adds_Gap_Class()
    {
        var cut = _ctx.Render<Lumeo.Flex>(p => p
            .Add(f => f.Gap, "4")
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("gap-4", cls);
    }

    [Fact]
    public void Wrap_True_Adds_FlexWrap_Class()
    {
        var cut = _ctx.Render<Lumeo.Flex>(p => p
            .Add(f => f.Wrap, true)
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex-wrap", cls);
    }

    [Fact]
    public void Align_And_Justify_Parameters_Add_Classes()
    {
        var cut = _ctx.Render<Lumeo.Flex>(p => p
            .Add(f => f.Align, "center")
            .Add(f => f.Justify, "between")
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("items-center", cls);
        Assert.Contains("justify-between", cls);
    }
}
