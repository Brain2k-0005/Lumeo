using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.SafeArea;

public class SafeAreaTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public SafeAreaTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Child_Content_In_A_Safe_Area_Container()
    {
        var cut = _ctx.Render<L.SafeArea>(p => p.AddChildContent("content"));
        Assert.Contains("content", cut.Markup);
        Assert.Contains("lumeo-safe-area", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Default_Insets_Top_And_Bottom_Only()
    {
        var style = _ctx.Render<L.SafeArea>(p => p.AddChildContent("x")).Find("div").GetAttribute("style") ?? "";
        Assert.Contains("padding-top: env(safe-area-inset-top", style);
        Assert.Contains("padding-bottom: env(safe-area-inset-bottom", style);
        Assert.DoesNotContain("padding-left", style);
        Assert.DoesNotContain("padding-right", style);
    }

    [Fact]
    public void Left_And_Right_Are_Opt_In()
    {
        var style = _ctx.Render<L.SafeArea>(p => p
            .Add(s => s.Top, false)
            .Add(s => s.Bottom, false)
            .Add(s => s.Left, true)
            .Add(s => s.Right, true)
            .AddChildContent("x")).Find("div").GetAttribute("style") ?? "";

        Assert.Contains("padding-left: env(safe-area-inset-left", style);
        Assert.Contains("padding-right: env(safe-area-inset-right", style);
        Assert.DoesNotContain("padding-top", style);
        Assert.DoesNotContain("padding-bottom", style);
    }

    // Regression (triage n=118, edge-data): a caller-supplied style in
    // AdditionalAttributes must NOT wipe the computed safe-area insets; it is
    // merged additively after them.
    [Fact]
    public void Caller_Style_Does_Not_Clobber_Safe_Area_Insets()
    {
        var div = _ctx.Render<L.SafeArea>(p => p
            .AddUnmatched("style", "margin: 4px")
            .AddChildContent("x")).Find("div");

        var style = div.GetAttribute("style") ?? "";
        Assert.Contains("padding-top: env(safe-area-inset-top", style);
        Assert.Contains("padding-bottom: env(safe-area-inset-bottom", style);
        Assert.Contains("margin: 4px", style);
    }

    // Regression (triage n=204, edge-data): a caller-supplied class in
    // AdditionalAttributes must NOT overwrite the base lumeo-safe-area class;
    // it is merged through Cx.Merge.
    [Fact]
    public void Caller_Class_Does_Not_Drop_Base_Class()
    {
        var div = _ctx.Render<L.SafeArea>(p => p
            .AddUnmatched("class", "my-custom")
            .AddChildContent("x")).Find("div");

        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("lumeo-safe-area", cls);
        Assert.Contains("my-custom", cls);
    }
}
