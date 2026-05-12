using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.ButtonGroup;

public class ButtonGroupTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ButtonGroupTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Wrapper_Div()
    {
        var cut = _ctx.Render<Lumeo.ButtonGroup>(p => p
            .AddChildContent("<button>A</button>"));

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<Lumeo.ButtonGroup>(p => p
            .Add(b => b.Class, "my-group-class")
            .AddChildContent("<button>A</button>"));

        var div = cut.Find("div");
        Assert.Contains("my-group-class", div.GetAttribute("class"));
    }

    [Fact]
    public void Default_Orientation_Is_Horizontal()
    {
        var cut = _ctx.Render<Lumeo.ButtonGroup>(p => p
            .AddChildContent("<button>A</button>"));

        var div = cut.Find("div");
        Assert.Contains("flex-row", div.GetAttribute("class"));
    }

    [Fact]
    public void Vertical_Orientation_Applies_Flex_Col()
    {
        var cut = _ctx.Render<Lumeo.ButtonGroup>(p => p
            .Add(b => b.Orientation, Lumeo.ButtonGroup.ButtonGroupOrientation.Vertical)
            .AddChildContent("<button>A</button>"));

        var div = cut.Find("div");
        Assert.Contains("flex-col", div.GetAttribute("class"));
    }

    [Fact]
    public void Horizontal_Applies_Child_Border_Collapse_Classes()
    {
        var cut = _ctx.Render<Lumeo.ButtonGroup>(p => p
            .Add(b => b.Orientation, Lumeo.ButtonGroup.ButtonGroupOrientation.Horizontal)
            .AddChildContent("<button>A</button>"));

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("-ml-px", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<Lumeo.ButtonGroup>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "btn-group",
                ["aria-label"] = "Action group"
            })
            .AddChildContent("<button>A</button>"));

        var div = cut.Find("div");
        Assert.Equal("btn-group", div.GetAttribute("data-testid"));
        Assert.Equal("Action group", div.GetAttribute("aria-label"));
    }

    [Fact]
    public void Renders_Child_Content()
    {
        var cut = _ctx.Render<Lumeo.ButtonGroup>(p => p
            .AddChildContent("<button class=\"btn-a\">A</button><button class=\"btn-b\">B</button>"));

        Assert.NotNull(cut.Find(".btn-a"));
        Assert.NotNull(cut.Find(".btn-b"));
    }
}
