using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Progress;

public class ProgressTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public ProgressTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Progressbar_Element()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50));

        Assert.NotNull(cut.Find("[role='progressbar']"));
    }

    [Fact]
    public void Has_Correct_ARIA_Attributes()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 42)
            .Add(b => b.Max, 100));

        var bar = cut.Find("[role='progressbar']");
        Assert.Equal("progressbar", bar.GetAttribute("role"));
        Assert.Equal("42", bar.GetAttribute("aria-valuenow"));
        Assert.Equal("0", bar.GetAttribute("aria-valuemin"));
        Assert.Equal("100", bar.GetAttribute("aria-valuemax"));
    }

    [Fact]
    public void ARIA_Valuemax_Reflects_Max_Parameter()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 5)
            .Add(b => b.Max, 10));

        var bar = cut.Find("[role='progressbar']");
        Assert.Equal("10", bar.GetAttribute("aria-valuemax"));
        Assert.Equal("5", bar.GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void Indicator_Width_Reflects_Percentage()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Max, 100));

        var indicator = cut.Find("[role='progressbar'] div");
        Assert.Contains("width: 50%", indicator.GetAttribute("style"));
    }

    [Fact]
    public void Indicator_Width_Clamps_At_100_Percent()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 150)
            .Add(b => b.Max, 100));

        var indicator = cut.Find("[role='progressbar'] div");
        Assert.Contains("width: 100%", indicator.GetAttribute("style"));
    }

    [Fact]
    public void Indicator_Width_Is_Zero_When_Value_Is_Zero()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 0)
            .Add(b => b.Max, 100));

        var indicator = cut.Find("[role='progressbar'] div");
        Assert.Contains("width: 0%", indicator.GetAttribute("style"));
    }

    [Fact]
    public void Default_Variant_Indicator_Has_Primary_Class()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50));

        var indicator = cut.Find("[role='progressbar'] div");
        Assert.Contains("bg-primary", indicator.GetAttribute("class"));
    }

    [Fact]
    public void Success_Variant_Indicator_Has_Success_Class()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 75)
            .Add(b => b.Variant, Lumeo.Progress.ProgressVariant.Success));

        var indicator = cut.Find("[role='progressbar'] div");
        Assert.Contains("bg-success", indicator.GetAttribute("class"));
    }

    [Fact]
    public void Warning_Variant_Indicator_Has_Warning_Class()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 60)
            .Add(b => b.Variant, Lumeo.Progress.ProgressVariant.Warning));

        var indicator = cut.Find("[role='progressbar'] div");
        Assert.Contains("bg-warning", indicator.GetAttribute("class"));
    }

    [Fact]
    public void Destructive_Variant_Indicator_Has_Destructive_Class()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 90)
            .Add(b => b.Variant, Lumeo.Progress.ProgressVariant.Destructive));

        var indicator = cut.Find("[role='progressbar'] div");
        Assert.Contains("bg-destructive", indicator.GetAttribute("class"));
    }

    [Fact]
    public void Outer_Div_Has_Base_Classes()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50));

        var bar = cut.Find("[role='progressbar']");
        var cls = bar.GetAttribute("class");
        Assert.Contains("relative", cls);
        Assert.Contains("h-2", cls);
        Assert.Contains("w-full", cls);
        Assert.Contains("overflow-hidden", cls);
        Assert.Contains("rounded-full", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Class, "my-progress-class"));

        var bar = cut.Find("[role='progressbar']");
        var cls = bar.GetAttribute("class");
        Assert.Contains("my-progress-class", cls);
        Assert.Contains("relative", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-progress",
                ["aria-label"] = "Upload progress"
            }));

        var bar = cut.Find("[role='progressbar']");
        Assert.Equal("my-progress", bar.GetAttribute("data-testid"));
        Assert.Equal("Upload progress", bar.GetAttribute("aria-label"));
    }

    [Fact]
    public void Custom_Max_Computes_Percentage_Correctly()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 3)
            .Add(b => b.Max, 4));

        var indicator = cut.Find("[role='progressbar'] div");
        Assert.Contains("width: 75%", indicator.GetAttribute("style"));
    }
}
