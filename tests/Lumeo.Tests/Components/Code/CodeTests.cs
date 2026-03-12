using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Code;

public class CodeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CodeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Code_Element()
    {
        var cut = _ctx.Render<Lumeo.Code>(p => p
            .AddChildContent("var x = 1;"));

        Assert.NotNull(cut.Find("code"));
        Assert.Equal("var x = 1;", cut.Find("code").TextContent);
    }

    [Fact]
    public void Default_Inline_Variant_Has_Rounded_Bg_Muted()
    {
        var cut = _ctx.Render<Lumeo.Code>(p => p
            .AddChildContent("inline code"));

        var cls = cut.Find("code").GetAttribute("class");
        Assert.Contains("bg-muted", cls);
        Assert.Contains("font-mono", cls);
    }

    [Fact]
    public void Block_Variant_Has_Block_And_Padding_Classes()
    {
        var cut = _ctx.Render<Lumeo.Code>(p => p
            .Add(c => c.Variant, "block")
            .AddChildContent("block code"));

        var cls = cut.Find("code").GetAttribute("class");
        Assert.Contains("block", cls);
        Assert.Contains("rounded-lg", cls);
        Assert.Contains("p-4", cls);
    }

    [Fact]
    public void Size_Parameter_Adds_Text_Size_Class()
    {
        var cut = _ctx.Render<Lumeo.Code>(p => p
            .Add(c => c.Size, "xs")
            .AddChildContent("small code"));

        var cls = cut.Find("code").GetAttribute("class");
        Assert.Contains("text-xs", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Code>(p => p
            .Add(c => c.Class, "my-code")
            .AddChildContent("code"));

        var cls = cut.Find("code").GetAttribute("class");
        Assert.Contains("my-code", cls);
    }
}
