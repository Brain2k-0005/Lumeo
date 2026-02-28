using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Skeleton;

public class SkeletonTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public SkeletonTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Div_Element()
    {
        var cut = _ctx.Render<L.Skeleton>();

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Has_Animate_Pulse_Class()
    {
        var cut = _ctx.Render<L.Skeleton>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("animate-pulse", cls);
    }

    [Fact]
    public void Has_Rounded_Class()
    {
        var cut = _ctx.Render<L.Skeleton>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("rounded-md", cls);
    }

    [Fact]
    public void Has_Background_Class()
    {
        var cut = _ctx.Render<L.Skeleton>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("bg-primary/10", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.Skeleton>(p => p
            .Add(s => s.Class, "h-4 w-32"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("h-4", cls);
        Assert.Contains("w-32", cls);
        Assert.Contains("animate-pulse", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.Skeleton>(p => p
            .Add(s => s.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-skeleton",
                ["aria-hidden"] = "true"
            }));

        var div = cut.Find("div");
        Assert.Equal("my-skeleton", div.GetAttribute("data-testid"));
        Assert.Equal("true", div.GetAttribute("aria-hidden"));
    }
}
