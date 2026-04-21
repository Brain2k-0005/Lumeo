using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Delta;

public class DeltaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public DeltaTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Positive_Value_Renders_Plus_Sign_And_Percent()
    {
        var cut = _ctx.Render<Lumeo.Delta>(p => p
            .Add(d => d.Value, 12.3));

        Assert.Contains("+12.3%", cut.Markup);
    }

    [Fact]
    public void Negative_Value_Renders_Minus_Without_Extra_Prefix()
    {
        var cut = _ctx.Render<Lumeo.Delta>(p => p
            .Add(d => d.Value, -4.0));

        Assert.Contains("-4%", cut.Markup);
        Assert.DoesNotContain("+-", cut.Markup);
    }

    [Fact]
    public void Zero_Value_Renders_Neutral_Colors()
    {
        var cut = _ctx.Render<Lumeo.Delta>(p => p
            .Add(d => d.Value, 0));

        var cls = cut.Find("span").GetAttribute("class");
        Assert.Contains("bg-muted", cls);
        Assert.Contains("text-muted-foreground", cls);
    }

    [Fact]
    public void Format_Absolute_Omits_Percent_Sign()
    {
        var cut = _ctx.Render<Lumeo.Delta>(p => p
            .Add(d => d.Value, 42)
            .Add(d => d.Format, Lumeo.Delta.DeltaFormat.Absolute));

        Assert.Contains("+42", cut.Markup);
        Assert.DoesNotContain("+42%", cut.Markup);
    }

    [Fact]
    public void ShowArrow_True_Renders_Svg_When_Value_Nonzero()
    {
        var cut = _ctx.Render<Lumeo.Delta>(p => p
            .Add(d => d.Value, 5)
            .Add(d => d.ShowArrow, true));

        Assert.NotEmpty(cut.FindAll("svg"));
    }

    [Fact]
    public void ShowArrow_False_Hides_Svg()
    {
        var cut = _ctx.Render<Lumeo.Delta>(p => p
            .Add(d => d.Value, 5)
            .Add(d => d.ShowArrow, false));

        Assert.Empty(cut.FindAll("svg"));
    }

    [Fact]
    public void Positive_Good_Positive_Value_Uses_Emerald_Text()
    {
        var cut = _ctx.Render<Lumeo.Delta>(p => p
            .Add(d => d.Value, 1.0)
            .Add(d => d.Positive, Lumeo.Delta.DeltaDirection.Good));

        Assert.Contains("text-emerald-600", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Positive_Good_Negative_Value_Uses_Rose_Text()
    {
        var cut = _ctx.Render<Lumeo.Delta>(p => p
            .Add(d => d.Value, -1.0)
            .Add(d => d.Positive, Lumeo.Delta.DeltaDirection.Good));

        Assert.Contains("text-rose-600", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Positive_Bad_Inverts_Colors_For_Positive_Value()
    {
        var cut = _ctx.Render<Lumeo.Delta>(p => p
            .Add(d => d.Value, 1.0)
            .Add(d => d.Positive, Lumeo.Delta.DeltaDirection.Bad));

        // Up is bad when Positive=Bad → rose
        Assert.Contains("text-rose-600", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Positive_Bad_Negative_Value_Is_Good_Emerald()
    {
        var cut = _ctx.Render<Lumeo.Delta>(p => p
            .Add(d => d.Value, -1.0)
            .Add(d => d.Positive, Lumeo.Delta.DeltaDirection.Bad));

        Assert.Contains("text-emerald-600", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Delta>(p => p
            .Add(d => d.Value, 1.0)
            .Add(d => d.Class, "delta-x"));

        Assert.Contains("delta-x", cut.Find("span").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.Delta>(p => p
            .Add(d => d.Value, 1.0)
            .Add(d => d.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "delta"
            }));

        Assert.Equal("delta", cut.Find("span").GetAttribute("data-testid"));
    }
}
