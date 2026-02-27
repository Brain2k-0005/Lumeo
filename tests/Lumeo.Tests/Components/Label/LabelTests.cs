using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Label;

public class LabelTests : IDisposable
{
    private readonly BunitContext _ctx = new();

    public LabelTests()
    {
        _ctx.AddLumeoServices();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Renders_Label_Element()
    {
        var cut = _ctx.Render<Lumeo.Label>(p => p
            .AddChildContent("Username"));

        Assert.NotNull(cut.Find("label"));
    }

    [Fact]
    public void Renders_Child_Content()
    {
        var cut = _ctx.Render<Lumeo.Label>(p => p
            .AddChildContent("Email Address"));

        Assert.Equal("Email Address", cut.Find("label").TextContent.Trim());
    }

    [Fact]
    public void Has_Base_Classes()
    {
        var cut = _ctx.Render<Lumeo.Label>(p => p
            .AddChildContent("Label"));

        var cls = cut.Find("label").GetAttribute("class");
        Assert.Contains("text-sm", cls);
        Assert.Contains("font-medium", cls);
        Assert.Contains("leading-none", cls);
    }

    [Fact]
    public void Has_Peer_Disabled_Classes()
    {
        var cut = _ctx.Render<Lumeo.Label>(p => p
            .AddChildContent("Label"));

        var cls = cut.Find("label").GetAttribute("class");
        Assert.Contains("peer-disabled:cursor-not-allowed", cls);
        Assert.Contains("peer-disabled:opacity-70", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.Label>(p => p
            .Add(b => b.Class, "my-label-class")
            .AddChildContent("Styled"));

        var cls = cut.Find("label").GetAttribute("class");
        Assert.Contains("my-label-class", cls);
        Assert.Contains("text-sm", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.Label>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-label",
                ["for"] = "input-id"
            })
            .AddChildContent("Label"));

        var label = cut.Find("label");
        Assert.Equal("my-label", label.GetAttribute("data-testid"));
        Assert.Equal("input-id", label.GetAttribute("for"));
    }

    [Fact]
    public void Renders_Empty_Without_Child_Content()
    {
        var cut = _ctx.Render<Lumeo.Label>();

        var label = cut.Find("label");
        Assert.NotNull(label);
        Assert.Equal("", label.TextContent.Trim());
    }
}
