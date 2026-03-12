using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Grid;

public class GridTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public GridTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Default_Grid()
    {
        var cut = _ctx.Render<Lumeo.Grid>(p => p
            .AddChildContent("Content"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Default_Has_Grid_And_SingleColumn()
    {
        var cut = _ctx.Render<Lumeo.Grid>(p => p
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("grid", cls);
        Assert.Contains("grid-cols-1", cls);
    }

    [Fact]
    public void Columns_Parameter_Changes_GridCols_Class()
    {
        var cut = _ctx.Render<Lumeo.Grid>(p => p
            .Add(g => g.Columns, 3)
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("grid-cols-3", cls);
    }

    [Fact]
    public void Gap_Parameter_Adds_Gap_Class()
    {
        var cut = _ctx.Render<Lumeo.Grid>(p => p
            .Add(g => g.Gap, "6")
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("gap-6", cls);
    }

    [Fact]
    public void RowGap_And_ColGap_Add_Respective_Classes()
    {
        var cut = _ctx.Render<Lumeo.Grid>(p => p
            .Add(g => g.Gap, null)
            .Add(g => g.RowGap, "2")
            .Add(g => g.ColGap, "4")
            .AddChildContent("Content"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("gap-y-2", cls);
        Assert.Contains("gap-x-4", cls);
    }
}
