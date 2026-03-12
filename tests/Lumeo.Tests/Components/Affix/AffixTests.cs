using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Affix;

public class AffixTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AffixTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Container_Div()
    {
        var cut = _ctx.Render<Lumeo.Affix>(p => p
            .AddChildContent("Sticky content"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Renders_Child_Content()
    {
        var cut = _ctx.Render<Lumeo.Affix>(p => p
            .AddChildContent("<nav id='nav'>Navigation</nav>"));

        Assert.Equal("Navigation", cut.Find("#nav").TextContent);
    }

    [Fact]
    public void Container_Has_Id_Attribute()
    {
        var cut = _ctx.Render<Lumeo.Affix>(p => p
            .AddChildContent("Content"));

        var id = cut.Find("div").GetAttribute("id");
        Assert.NotNull(id);
        Assert.StartsWith("affix-", id);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Affix>(p => p
            .Add(a => a.Class, "my-affix")
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-affix", cls);
    }
}
