using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Bento;

public class BentoTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BentoTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_As_Grid_Div()
    {
        var cut = _ctx.Render<Lumeo.Bento>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("grid", cls);
    }

    [Fact]
    public void Default_Columns_Is_Four()
    {
        var cut = _ctx.Render<Lumeo.Bento>();

        Assert.Contains("grid-cols-4", cut.Find("div").GetAttribute("class"));
    }

    [Theory]
    [InlineData(2, "grid-cols-2")]
    [InlineData(3, "grid-cols-3")]
    [InlineData(4, "grid-cols-4")]
    [InlineData(6, "grid-cols-6")]
    public void Columns_Uses_Tailwind_Class_For_Known_Values(int columns, string expectedClass)
    {
        var cut = _ctx.Render<Lumeo.Bento>(p => p
            .Add(b => b.Columns, columns));

        Assert.Contains(expectedClass, cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Columns_Custom_Value_Falls_Back_To_Inline_Style()
    {
        var cut = _ctx.Render<Lumeo.Bento>(p => p
            .Add(b => b.Columns, 5));

        var style = cut.Find("div").GetAttribute("style");
        Assert.Contains("grid-template-columns", style);
        Assert.Contains("repeat(5", style);
    }

    [Fact]
    public void Default_Gap_Is_Medium()
    {
        var cut = _ctx.Render<Lumeo.Bento>();

        Assert.Contains("gap-4", cut.Find("div").GetAttribute("class"));
    }

    [Theory]
    [InlineData(Lumeo.Bento.BentoGap.Sm, "gap-3")]
    [InlineData(Lumeo.Bento.BentoGap.Md, "gap-4")]
    [InlineData(Lumeo.Bento.BentoGap.Lg, "gap-6")]
    public void Gap_Maps_To_Expected_Class(Lumeo.Bento.BentoGap gap, string expectedClass)
    {
        var cut = _ctx.Render<Lumeo.Bento>(p => p
            .Add(b => b.Gap, gap));

        Assert.Contains(expectedClass, cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Container_Query_Inline_Style_Is_Present()
    {
        var cut = _ctx.Render<Lumeo.Bento>();

        Assert.Contains("container-type: inline-size", cut.Find("div").GetAttribute("style"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Bento>(p => p
            .Add(b => b.Class, "my-bento"));

        Assert.Contains("my-bento", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void ChildContent_Renders_Inside_Grid()
    {
        var cut = _ctx.Render<Lumeo.Bento>(p => p
            .AddChildContent("<span data-testid='tile'>t1</span>"));

        Assert.NotNull(cut.Find("[data-testid='tile']"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.Bento>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "grid"
            }));

        Assert.Equal("grid", cut.Find("div").GetAttribute("data-testid"));
    }
}
