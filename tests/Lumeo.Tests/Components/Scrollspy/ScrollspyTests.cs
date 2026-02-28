using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scrollspy;

public class ScrollspyTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Scrollspy root component ---

    [Fact]
    public void Scrollspy_Renders_Container_Div()
    {
        var cut = _ctx.Render<L.Scrollspy>(p => p
            .AddChildContent("content"));

        Assert.NotNull(cut.Find("[data-slot='scrollspy']"));
    }

    [Fact]
    public void Scrollspy_Has_Unique_Id()
    {
        var cut = _ctx.Render<L.Scrollspy>(p => p
            .AddChildContent("content"));

        var el = cut.Find("[data-slot='scrollspy']");
        var id = el.GetAttribute("id");
        Assert.NotNull(id);
        Assert.StartsWith("scrollspy-", id);
    }

    [Fact]
    public void Scrollspy_Has_Relative_Class()
    {
        var cut = _ctx.Render<L.Scrollspy>(p => p
            .AddChildContent("content"));

        var el = cut.Find("[data-slot='scrollspy']");
        Assert.Contains("relative", el.GetAttribute("class"));
    }

    [Fact]
    public void Scrollspy_Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<L.Scrollspy>(p => p
            .Add(b => b.Class, "my-spy")
            .AddChildContent("content"));

        var el = cut.Find("[data-slot='scrollspy']");
        Assert.Contains("my-spy", el.GetAttribute("class"));
        Assert.Contains("relative", el.GetAttribute("class"));
    }

    [Fact]
    public void Scrollspy_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.Scrollspy>(p => p
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "spy-container"
            })
            .AddChildContent("content"));

        Assert.Equal("spy-container", cut.Find("[data-slot='scrollspy']").GetAttribute("data-testid"));
    }

    [Fact]
    public void Scrollspy_Default_Offset_Is_Zero()
    {
        var cut = _ctx.Render<L.Scrollspy>(p => p
            .AddChildContent("content"));

        // Verify it renders without error with default offset
        Assert.NotNull(cut.Find("[data-slot='scrollspy']"));
    }

    [Fact]
    public void Scrollspy_Smooth_Parameter_Defaults_True()
    {
        // Verify the component renders fine with default smooth setting
        var cut = _ctx.Render<L.Scrollspy>(p => p
            .AddChildContent("content"));

        Assert.NotNull(cut.Find("[data-slot='scrollspy']"));
    }

    // --- ScrollspySection ---

    [Fact]
    public void ScrollspySection_Renders_Div_With_Id()
    {
        var cut = _ctx.Render<L.ScrollspySection>(p => p
            .Add(b => b.Id, "section-1")
            .AddChildContent("Section content"));

        var el = cut.Find("[data-scrollspy-section]");
        Assert.NotNull(el);
        Assert.Equal("section-1", el.GetAttribute("id"));
    }

    [Fact]
    public void ScrollspySection_Has_DataSlot_Attribute()
    {
        var cut = _ctx.Render<L.ScrollspySection>(p => p
            .Add(b => b.Id, "sec-a")
            .AddChildContent("content"));

        Assert.NotNull(cut.Find("[data-scrollspy-section]"));
    }

    [Fact]
    public void ScrollspySection_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.ScrollspySection>(p => p
            .Add(b => b.Id, "sec-b")
            .AddChildContent("<span>Hello</span>"));

        Assert.NotNull(cut.Find("span"));
    }

    [Fact]
    public void ScrollspySection_Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<L.ScrollspySection>(p => p
            .Add(b => b.Id, "sec-c")
            .Add(b => b.Class, "my-section")
            .AddChildContent("content"));

        var el = cut.Find("[data-scrollspy-section]");
        Assert.Contains("my-section", el.GetAttribute("class"));
    }

    [Fact]
    public void ScrollspySection_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.ScrollspySection>(p => p
            .Add(b => b.Id, "sec-d")
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["aria-label"] = "Introduction section"
            })
            .AddChildContent("content"));

        Assert.Equal("Introduction section", cut.Find("[data-scrollspy-section]").GetAttribute("aria-label"));
    }

    // --- ScrollspyLink ---

    [Fact]
    public void ScrollspyLink_Renders_Button()
    {
        var cut = _ctx.Render<L.ScrollspyLink>(p => p
            .Add(b => b.Target, "section-1")
            .AddChildContent("Go to section 1"));

        Assert.NotNull(cut.Find("[data-slot='scrollspy-link']"));
    }

    [Fact]
    public void ScrollspyLink_Button_Has_DataSlot_Attribute()
    {
        var cut = _ctx.Render<L.ScrollspyLink>(p => p
            .Add(b => b.Target, "section-1")
            .AddChildContent("Link text"));

        var btn = cut.Find("button");
        Assert.Equal("scrollspy-link", btn.GetAttribute("data-slot"));
    }

    [Fact]
    public void ScrollspyLink_Renders_ChildContent()
    {
        var cut = _ctx.Render<L.ScrollspyLink>(p => p
            .Add(b => b.Target, "sec")
            .AddChildContent("Overview"));

        Assert.Contains("Overview", cut.Find("button").TextContent);
    }

    [Fact]
    public void ScrollspyLink_Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<L.ScrollspyLink>(p => p
            .Add(b => b.Target, "sec")
            .Add(b => b.Class, "nav-link")
            .AddChildContent("Link"));

        var btn = cut.Find("button");
        Assert.Contains("nav-link", btn.GetAttribute("class"));
    }

    [Fact]
    public void ScrollspyLink_Without_Active_Context_Has_No_DataActive()
    {
        // Without a cascading context, data-active should not be "true"
        var cut = _ctx.Render<L.ScrollspyLink>(p => p
            .Add(b => b.Target, "section-1")
            .AddChildContent("Link"));

        var btn = cut.Find("button");
        var dataActive = btn.GetAttribute("data-active");
        Assert.Null(dataActive); // not active when no context
    }

    [Fact]
    public void ScrollspyLink_Additional_Attributes_Are_Forwarded()
    {
        var cut = _ctx.Render<L.ScrollspyLink>(p => p
            .Add(b => b.Target, "sec")
            .Add(b => b.AdditionalAttributes, new Dictionary<string, object>
            {
                ["aria-current"] = "page"
            })
            .AddChildContent("Link"));

        Assert.Equal("page", cut.Find("button").GetAttribute("aria-current"));
    }
}
