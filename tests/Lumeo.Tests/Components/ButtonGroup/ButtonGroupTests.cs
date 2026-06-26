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
            .Add(b => b.Orientation, Lumeo.Orientation.Vertical)
            .AddChildContent("<button>A</button>"));

        var div = cut.Find("div");
        Assert.Contains("flex-col", div.GetAttribute("class"));
    }

    [Fact]
    public void Horizontal_Applies_Child_Border_Collapse_Classes()
    {
        var cut = _ctx.Render<Lumeo.ButtonGroup>(p => p
            .Add(b => b.Orientation, Lumeo.Orientation.Horizontal)
            .AddChildContent("<button>A</button>"));

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";
        Assert.Contains("-ms-px", cls);
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

    // EDGE-DATA #111: Link-buttons (<Button Href=...>) render as <a>, so when the
    // segmented border-collapse/rounding selectors are scoped to the `button` element
    // type the rounding/collapse silently breaks for any non-button child. The selectors
    // must target every direct child (`&>*`) so links / AsChild / Toggle children
    // participate. Without the fix the class string carries `[&>button...]` and these
    // assertions fail.
    [Fact]
    public void Horizontal_Child_Selectors_Are_Element_Agnostic_Not_Button_Scoped()
    {
        var cut = _ctx.Render<Lumeo.ButtonGroup>(p => p
            .Add(b => b.Orientation, Lumeo.Orientation.Horizontal)
            // Mixed children: a non-button link first child + a button.
            .AddChildContent("<a class=\"lnk\" href=\"#\">A</a><button class=\"btn\">B</button>"));

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";

        // The collapse/rounding selectors must apply to ALL direct children, not just buttons.
        Assert.Contains("[&>*:not(:first-child)]:rounded-s-none", cls);
        Assert.Contains("[&>*:not(:last-child)]:rounded-e-none", cls);
        Assert.Contains("[&>*:not(:first-child)]:-ms-px", cls);
        // The button-only scoping that broke link-buttons must be gone.
        Assert.DoesNotContain("[&>button", cls);

        // Sanity: both children still render inside the group.
        Assert.NotNull(cut.Find(".lnk"));
        Assert.NotNull(cut.Find(".btn"));
    }

    // EDGE-DATA #200: :first-child/:last-child are evaluated against ALL DOM siblings, so a
    // non-button first/last child shifts the index and mis-rounds the group. The positional
    // pseudo-classes must be computed over the SAME set that is styled — broadening the
    // selectors to `&>*` makes the styled set and the index set identical. Mirror in both
    // orientations. Without the fix the vertical class string carries `[&>button...]` and
    // this assertion fails.
    [Fact]
    public void Vertical_Child_Selectors_Are_Element_Agnostic_Not_Button_Scoped()
    {
        var cut = _ctx.Render<Lumeo.ButtonGroup>(p => p
            .Add(b => b.Orientation, Lumeo.Orientation.Vertical)
            // A non-button element as the first child must not shift the rounding index.
            .AddChildContent("<span class=\"sep\"></span><button class=\"btn\">B</button>"));

        var div = cut.Find("div");
        var cls = div.GetAttribute("class") ?? "";

        Assert.Contains("[&>*:not(:first-child)]:rounded-t-none", cls);
        Assert.Contains("[&>*:not(:last-child)]:rounded-b-none", cls);
        Assert.Contains("[&>*:not(:first-child)]:-mt-px", cls);
        Assert.DoesNotContain("[&>button", cls);
    }
}
