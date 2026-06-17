using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.AgentMessageList;

public class AgentMessageListTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public AgentMessageListTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Root_Div_With_Scroll_Container_Classes()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("flex-col", cls);
        Assert.Contains("overflow-y-auto", cls);
    }

    [Fact]
    public void ChildContent_Wrapped_In_Flex_Col_Gap()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .AddChildContent("<span data-testid='msg'>m</span>"));

        var inner = cut.Find("div > div");
        var cls = inner.GetAttribute("class");
        Assert.Contains("flex", cls);
        Assert.Contains("flex-col", cls);
        Assert.Contains("gap-4", cls);
    }

    [Fact]
    public void ChildContent_Renders()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .AddChildContent("<span data-testid='m'>msg</span>"));

        Assert.NotNull(cut.Find("[data-testid='m']"));
    }

    [Fact]
    public void AutoScroll_Default_Is_True()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>();

        Assert.True(cut.Instance.AutoScroll);
    }

    [Fact]
    public void AutoScroll_Can_Be_Set_False()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.AutoScroll, false));

        Assert.False(cut.Instance.AutoScroll);
    }

    [Fact]
    public void Custom_Class_Is_Appended()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.Class, "aml-x"));

        Assert.Contains("aml-x", cut.Find("div").GetAttribute("class"));
    }

    [Fact]
    public void Additional_Attributes_Forward()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.AdditionalAttributes, new Dictionary<string, object>
            {
                ["data-testid"] = "aml"
            }));

        Assert.Equal("aml", cut.Find("div").GetAttribute("data-testid"));
    }

    // ── #303: opt-in virtualization hook ─────────────────────────────────────

    [Fact]
    public void Virtualize_Default_Is_False()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>();

        Assert.False(cut.Instance.Virtualize);
    }

    [Fact]
    public void Virtualized_Path_Renders_Provided_Items()
    {
        var items = new List<Microsoft.AspNetCore.Components.RenderFragment>
        {
            b => b.AddMarkupContent(0, "<span data-testid='v0'>m0</span>"),
            b => b.AddMarkupContent(0, "<span data-testid='v1'>m1</span>"),
        };

        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.Virtualize, true)
            .Add(x => x.Items, items)
            // Small item size keeps rows inside the default Virtualize viewport
            // so the test sees the first one rendered.
            .Add(x => x.ItemSize, 10f));

        Assert.NotNull(cut.Find("[data-testid='v0']"));
    }

    [Fact]
    public void Virtualized_Path_Skips_ChildContent_Flex_Wrapper()
    {
        var items = new List<Microsoft.AspNetCore.Components.RenderFragment>
        {
            b => b.AddMarkupContent(0, "<span>m</span>"),
        };

        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.Virtualize, true)
            .Add(x => x.Items, items));

        // The default path's inner "flex flex-col gap-4" wrapper must not be
        // present in virtualized mode (Virtualize is a direct scroll child).
        Assert.DoesNotContain("gap-4", cut.Markup);
    }

    [Fact]
    public void Virtualize_True_Without_Items_Falls_Back_To_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.AgentMessageList>(p => p
            .Add(x => x.Virtualize, true)
            .AddChildContent("<span data-testid='cc'>fallback</span>"));

        Assert.NotNull(cut.Find("[data-testid='cc']"));
    }
}
