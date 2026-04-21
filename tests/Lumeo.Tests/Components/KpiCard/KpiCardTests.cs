using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.KpiCard;

public class KpiCardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public KpiCardTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Label_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.KpiCard>(p => p
            .Add(k => k.Label, "Revenue"));

        Assert.Contains("Revenue", cut.Markup);
    }

    [Fact]
    public void Renders_Value()
    {
        var cut = _ctx.Render<Lumeo.KpiCard>(p => p
            .Add(k => k.Value, "$12,345"));

        Assert.Contains("$12,345", cut.Markup);
    }

    [Fact]
    public void Delta_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.KpiCard>(p => p
            .Add(k => k.Value, "100")
            .Add(k => k.Delta, 5.0));

        Assert.Contains("+5%", cut.Markup);
    }

    [Fact]
    public void Delta_Not_Rendered_When_Null()
    {
        var cut = _ctx.Render<Lumeo.KpiCard>(p => p
            .Add(k => k.Value, "100"));

        Assert.DoesNotContain("+", cut.Markup);
        Assert.DoesNotContain("%", cut.Markup);
    }

    [Fact]
    public void Positive_Delta_With_Good_Direction_Shows_Emerald()
    {
        var cut = _ctx.Render<Lumeo.KpiCard>(p => p
            .Add(k => k.Delta, 10.0)
            .Add(k => k.DeltaPositive, Lumeo.KpiCard.KpiDeltaDirection.Good));

        Assert.Contains("text-emerald-600", cut.Markup);
    }

    [Fact]
    public void Positive_Delta_With_Bad_Direction_Shows_Rose()
    {
        var cut = _ctx.Render<Lumeo.KpiCard>(p => p
            .Add(k => k.Delta, 10.0)
            .Add(k => k.DeltaPositive, Lumeo.KpiCard.KpiDeltaDirection.Bad));

        Assert.Contains("text-rose-600", cut.Markup);
    }

    [Fact]
    public void Negative_Delta_With_Good_Direction_Shows_Rose()
    {
        var cut = _ctx.Render<Lumeo.KpiCard>(p => p
            .Add(k => k.Delta, -2.0)
            .Add(k => k.DeltaPositive, Lumeo.KpiCard.KpiDeltaDirection.Good));

        Assert.Contains("text-rose-600", cut.Markup);
    }

    [Fact]
    public void Absolute_Delta_Format_Does_Not_Show_Percent()
    {
        var cut = _ctx.Render<Lumeo.KpiCard>(p => p
            .Add(k => k.Delta, 7.0)
            .Add(k => k.DeltaFormat, Lumeo.KpiCard.KpiDeltaFormat.Absolute));

        Assert.Contains("+7", cut.Markup);
        Assert.DoesNotContain("+7%", cut.Markup);
    }

    [Fact]
    public void IconContent_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.KpiCard>(p => p
            .Add(k => k.IconContent, (RenderFragment)(b =>
            {
                b.OpenElement(0, "svg");
                b.AddAttribute(1, "data-testid", "ic");
                b.CloseElement();
            })));

        Assert.NotNull(cut.Find("[data-testid='ic']"));
    }

    [Fact]
    public void SparkContent_Renders_When_Provided()
    {
        var cut = _ctx.Render<Lumeo.KpiCard>(p => p
            .Add(k => k.Delta, 1.0)
            .Add(k => k.SparkContent, (RenderFragment)(b =>
            {
                b.OpenElement(0, "div");
                b.AddAttribute(1, "data-testid", "spark");
                b.CloseElement();
            })));

        Assert.NotNull(cut.Find("[data-testid='spark']"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.KpiCard>(p => p
            .Add(k => k.Class, "kpi-x"));

        Assert.Contains("kpi-x", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.KpiCard>(p => p
            .Add(k => k.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "kpi"
            }));

        Assert.Equal("kpi", cut.Find("div").GetAttribute("data-testid"));
    }
}
