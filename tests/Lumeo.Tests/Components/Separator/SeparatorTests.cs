using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Separator;

public class SeparatorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SeparatorTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Div_Element()
    {
        var cut = _ctx.Render<L.Separator>();

        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Has_Role_None()
    {
        var cut = _ctx.Render<L.Separator>();

        Assert.Equal("none", cut.Find("div").GetAttribute("role"));
    }

    [Fact]
    public void Horizontal_Is_Default_Orientation()
    {
        var cut = _ctx.Render<L.Separator>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("h-px", cls);
        Assert.Contains("w-full", cls);
    }

    [Fact]
    public void Horizontal_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.Orientation, L.Orientation.Horizontal));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("shrink-0", cls);
        Assert.Contains("bg-border", cls);
        Assert.Contains("h-px", cls);
        Assert.Contains("w-full", cls);
    }

    [Fact]
    public void Vertical_Has_Correct_Classes()
    {
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.Orientation, L.Orientation.Vertical));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("shrink-0", cls);
        Assert.Contains("bg-border", cls);
        Assert.Contains("h-full", cls);
        Assert.Contains("w-px", cls);
    }

    [Fact]
    public void Vertical_Does_Not_Have_Horizontal_Classes()
    {
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.Orientation, L.Orientation.Vertical));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.DoesNotContain("h-px", cls);
        Assert.DoesNotContain("w-full", cls);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.Class, "my-separator"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-separator", cls);
        Assert.Contains("bg-border", cls);
    }

    [Fact]
    public void Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "my-separator",
                ["aria-orientation"] = "horizontal"
            }));

        var div = cut.Find("div");
        Assert.Equal("my-separator", div.GetAttribute("data-testid"));
        Assert.Equal("horizontal", div.GetAttribute("aria-orientation"));
    }

    // --- Decorative / semantic role (#255) ---

    [Fact]
    public void Decorative_Defaults_True_With_Role_None()
    {
        var cut = _ctx.Render<L.Separator>();

        Assert.Equal("none", cut.Find("div").GetAttribute("role"));
    }

    [Fact]
    public void Decorative_Has_No_Aria_Orientation()
    {
        var cut = _ctx.Render<L.Separator>();

        Assert.Null(cut.Find("div").GetAttribute("aria-orientation"));
    }

    [Fact]
    public void Semantic_Horizontal_Has_Separator_Role_And_Orientation()
    {
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.Decorative, false)
            .Add(s => s.Orientation, L.Orientation.Horizontal));

        var div = cut.Find("div");
        Assert.Equal("separator", div.GetAttribute("role"));
        Assert.Equal("horizontal", div.GetAttribute("aria-orientation"));
    }

    [Fact]
    public void Semantic_Vertical_Has_Separator_Role_And_Vertical_Orientation()
    {
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.Decorative, false)
            .Add(s => s.Orientation, L.Orientation.Vertical));

        var div = cut.Find("div");
        Assert.Equal("separator", div.GetAttribute("role"));
        Assert.Equal("vertical", div.GetAttribute("aria-orientation"));
    }

    [Fact]
    public void Labelled_Separator_Inner_Lines_Stay_Decorative()
    {
        // A separator with ChildContent is a visual label band; its inner
        // hairlines stay role="none" regardless of the Decorative flag.
        var cut = _ctx.Render<L.Separator>(p => p
            .Add(s => s.Decorative, false)
            .AddChildContent("OR"));

        var lines = cut.FindAll("div.bg-border");
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal("none", l.GetAttribute("role")));
    }
}
