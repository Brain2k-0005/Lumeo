using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Progress;

// The track moved down one step in the 5.0 scale alignment: shadcn's progress is h-1.
public class ProgressTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ProgressTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

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
    public void Indicator_Width_Clamps_Negative_Value_To_Zero()
    {
        // width: -N% is invalid CSS the browser drops entirely — the
        // un-sized indicator then painted as a FULL bar.
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, -25)
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
        Assert.Contains("h-1", cls);
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

    // --- Circular variant ---

    [Fact]
    public void Circular_Shape_Renders_SVG_Element()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Shape, Lumeo.Progress.ProgressShape.Circular));

        Assert.NotNull(cut.Find("svg"));
    }

    [Fact]
    public void Circular_Shape_Has_Progressbar_Role()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Shape, Lumeo.Progress.ProgressShape.Circular));

        Assert.NotNull(cut.Find("[role='progressbar']"));
    }

    [Fact]
    public void Circular_ShowValue_Shows_Percentage_Text()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 75)
            .Add(b => b.Shape, Lumeo.Progress.ProgressShape.Circular)
            .Add(b => b.ShowValue, true));

        Assert.Contains("75%", cut.Markup);
    }

    // --- Size (migrated from an untyped string to Lumeo.Size) ---
    // These pin the pre-migration mapping byte-for-byte: old "Small"/"Default"/
    // "Large" string values must render identically under Lumeo.Size.Sm/Md/Lg.

    [Fact]
    public void Size_Sm_Linear_Height_Matches_PreMigration_Small()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p.Add(b => b.Value, 50).Add(b => b.Size, Lumeo.Size.Sm));
        var bar = cut.Find("[role='progressbar']");
        Assert.Equal(
            new[] { "relative", "h-0.75", "w-full", "overflow-hidden", "rounded-full", "bg-primary/20" },
            bar.GetAttribute("class")!.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Size_Md_Linear_Height_Matches_PreMigration_Default()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p.Add(b => b.Value, 50));
        var bar = cut.Find("[role='progressbar']");
        Assert.Equal(
            new[] { "relative", "h-1", "w-full", "overflow-hidden", "rounded-full", "bg-primary/20" },
            bar.GetAttribute("class")!.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Size_Lg_Linear_Height_Matches_PreMigration_Large()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p.Add(b => b.Value, 50).Add(b => b.Size, Lumeo.Size.Lg));
        var bar = cut.Find("[role='progressbar']");
        Assert.Equal(
            new[] { "relative", "h-2", "w-full", "overflow-hidden", "rounded-full", "bg-primary/20" },
            bar.GetAttribute("class")!.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "width: 24px; height: 24px")]
    [InlineData(Lumeo.Size.Xs, "width: 32px; height: 32px")]
    [InlineData(Lumeo.Size.Sm, "width: 40px; height: 40px")]
    [InlineData(Lumeo.Size.Md, "width: 56px; height: 56px")]
    [InlineData(Lumeo.Size.Lg, "width: 80px; height: 80px")]
    [InlineData(Lumeo.Size.Xl, "width: 96px; height: 96px")]
    [InlineData(Lumeo.Size.Xxl, "width: 112px; height: 112px")]
    public void Circular_Size_Sets_Diameter_Style(Lumeo.Size size, string expectedStyle)
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Shape, Lumeo.Progress.ProgressShape.Circular)
            .Add(b => b.Size, size));

        Assert.Equal(expectedStyle, cut.Find("svg").GetAttribute("style"));
    }

    [Fact]
    public void Circular_Size_Sm_Diameter_Matches_PreMigration_Small()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Shape, Lumeo.Progress.ProgressShape.Circular)
            .Add(b => b.Size, Lumeo.Size.Sm));

        Assert.Equal("width: 40px; height: 40px", cut.Find("svg").GetAttribute("style"));
    }

    [Fact]
    public void Circular_Size_Md_Diameter_Matches_PreMigration_Default()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Shape, Lumeo.Progress.ProgressShape.Circular));

        Assert.Equal("width: 56px; height: 56px", cut.Find("svg").GetAttribute("style"));
    }

    [Fact]
    public void Circular_Size_Lg_Diameter_Matches_PreMigration_Large()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Shape, Lumeo.Progress.ProgressShape.Circular)
            .Add(b => b.Size, Lumeo.Size.Lg));

        Assert.Equal("width: 80px; height: 80px", cut.Find("svg").GetAttribute("style"));
    }

    [Fact]
    public void Circular_ShowValue_Text_Size_Md_Matches_PreMigration_Default()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Shape, Lumeo.Progress.ProgressShape.Circular)
            .Add(b => b.ShowValue, true));

        var span = cut.Find("span");
        Assert.Equal(
            new[] { "absolute", "inset-0", "flex", "items-center", "justify-center", "text-foreground", "text-xs", "font-medium" },
            span.GetAttribute("class")!.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Circular_ShowValue_Text_Size_Lg_Matches_PreMigration_Large()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Shape, Lumeo.Progress.ProgressShape.Circular)
            .Add(b => b.ShowValue, true)
            .Add(b => b.Size, Lumeo.Size.Lg));

        var span = cut.Find("span");
        Assert.Equal(
            new[] { "absolute", "inset-0", "flex", "items-center", "justify-center", "text-foreground", "text-sm", "font-semibold" },
            span.GetAttribute("class")!.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Circular_ShowValue_Text_Size_Sm_Matches_PreMigration_Small()
    {
        var cut = _ctx.Render<Lumeo.Progress>(p => p
            .Add(b => b.Value, 50)
            .Add(b => b.Shape, Lumeo.Progress.ProgressShape.Circular)
            .Add(b => b.ShowValue, true)
            .Add(b => b.Size, Lumeo.Size.Sm));

        var span = cut.Find("span");
        Assert.Equal(
            new[] { "absolute", "inset-0", "flex", "items-center", "justify-center", "text-foreground", "text-[10px]", "font-medium" },
            span.GetAttribute("class")!.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Size_Xxs_And_Xs_Linear_Height_Tie_At_H0_5()
    {
        var xxs = _ctx.Render<Lumeo.Progress>(p => p.Add(b => b.Value, 50).Add(b => b.Size, Lumeo.Size.Xxs));
        var xs = _ctx.Render<Lumeo.Progress>(p => p.Add(b => b.Value, 50).Add(b => b.Size, Lumeo.Size.Xs));
        Assert.Contains("h-0.5", xxs.Find("[role='progressbar']").GetAttribute("class"));
        Assert.Contains("h-0.5", xs.Find("[role='progressbar']").GetAttribute("class"));
    }
}
