using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.SparkCard;

public class SparkCardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SparkCardTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Label_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.SparkCard>(p => p
            .Add(s => s.Label, "Active Users"));

        Assert.Contains("Active Users", cut.Markup);
    }

    [Fact]
    public void Renders_Value_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.SparkCard>(p => p
            .Add(s => s.Value, "1,024"));

        Assert.Contains("1,024", cut.Markup);
    }

    [Fact]
    public void Data_With_Multiple_Points_Emits_Polyline()
    {
        var cut = _ctx.Render<Lumeo.SparkCard>(p => p
            .Add(s => s.Data, new double[] { 1, 2, 3, 4, 5 }));

        Assert.NotNull(cut.Find("polyline"));
        var points = cut.Find("polyline").GetAttribute("points");
        Assert.False(string.IsNullOrEmpty(points));
    }

    [Fact]
    public void Data_With_Less_Than_Two_Points_Does_Not_Render_Polyline()
    {
        var cut = _ctx.Render<Lumeo.SparkCard>(p => p
            .Add(s => s.Data, new double[] { 1 }));

        Assert.Empty(cut.FindAll("polyline"));
    }

    [Fact]
    public void Data_Null_Does_Not_Render_Svg()
    {
        var cut = _ctx.Render<Lumeo.SparkCard>(p => p
            .Add(s => s.Label, "x"));

        Assert.Empty(cut.FindAll("svg"));
    }

    [Fact]
    public void ChildContent_Takes_Precedence_Over_Data()
    {
        var cut = _ctx.Render<Lumeo.SparkCard>(p => p
            .Add(s => s.Data, new double[] { 1, 2, 3 })
            .Add(s => s.ChildContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span data-testid='custom'>x</span>"))));

        Assert.NotNull(cut.Find("[data-testid='custom']"));
        Assert.Empty(cut.FindAll("polyline"));
    }

    [Fact]
    public void Root_Has_Card_Classes()
    {
        var cut = _ctx.Render<Lumeo.SparkCard>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("rounded-xl", cls);
        Assert.Contains("border", cls);
        Assert.Contains("bg-card", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.SparkCard>(p => p
            .Add(s => s.Class, "spark-x"));

        Assert.Contains("spark-x", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.SparkCard>(p => p
            .Add(s => s.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "spark"
            }));

        Assert.Equal("spark", cut.Find("div").GetAttribute("data-testid"));
    }
}
