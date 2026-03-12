using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Descriptions;

public class DescriptionsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DescriptionsTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Title_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .Add(d => d.Title, "User Details")
            .AddChildContent("Items here"));

        Assert.Contains("User Details", cut.Markup);
        var h3 = cut.Find("h3");
        Assert.Equal("User Details", h3.TextContent);
    }

    [Fact]
    public void Default_Column_Count_Is_Three()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .AddChildContent("Items"));

        Assert.Contains("grid-cols-3", cut.Markup);
    }

    [Fact]
    public void Column_Parameter_Changes_GridCols()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .Add(d => d.Column, 2)
            .AddChildContent("Items"));

        Assert.Contains("grid-cols-2", cut.Markup);
    }

    [Fact]
    public void Bordered_True_Renders_Border_Container()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .Add(d => d.Bordered, true)
            .AddChildContent("Items"));

        Assert.Contains("rounded-lg", cut.Markup);
        Assert.Contains("border", cut.Markup);
    }

    [Fact]
    public void Non_Bordered_Uses_Gap_Classes()
    {
        var cut = _ctx.Render<Lumeo.Descriptions>(p => p
            .Add(d => d.Bordered, false)
            .AddChildContent("Items"));

        Assert.Contains("gap-4", cut.Markup);
    }
}
