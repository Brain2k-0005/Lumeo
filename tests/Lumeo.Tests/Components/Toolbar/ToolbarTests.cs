using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Toolbar;

public class ToolbarTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToolbarTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_With_Role_Toolbar()
    {
        var cut = _ctx.Render<Lumeo.Toolbar>(p => p
            .AddChildContent("toolbar content"));

        var el = cut.Find("[role='toolbar']");
        Assert.NotNull(el);
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<Lumeo.Toolbar>(p => p
            .Add(t => t.Class, "my-toolbar")
            .AddChildContent("content"));

        var cls = cut.Find("[role='toolbar']").GetAttribute("class");
        Assert.Contains("my-toolbar", cls);
    }

    [Fact]
    public void Renders_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.Toolbar>(p => p
            .AddChildContent("<span>Action</span>"));

        Assert.Contains("Action", cut.Markup);
    }

    [Fact]
    public void Separator_Has_Separator_Role()
    {
        var cut = _ctx.Render<Lumeo.ToolbarSeparator>();

        var el = cut.Find("[role='separator']");
        Assert.NotNull(el);
    }

    [Fact]
    public void Spacer_Has_Flex1_Class()
    {
        var cut = _ctx.Render<Lumeo.ToolbarSpacer>();

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("flex-1", cls);
    }

    [Fact]
    public void Group_Renders_ChildContent()
    {
        var cut = _ctx.Render<Lumeo.ToolbarGroup>(p => p
            .AddChildContent("<span>grouped</span>"));

        Assert.Contains("grouped", cut.Markup);
    }

    /// <summary>
    /// Overflow=true with VisibleCount=-1 (auto-measure mode) should render without
    /// crashing. In bUnit there is no real browser layout so the JS ResizeObserver
    /// never fires — the toolbar simply renders in "no items measured yet" state.
    /// We verify the toolbar container is present and child content is reachable.
    /// </summary>
    [Fact]
    public void Overflow_AutoMeasure_Renders_Without_Error()
    {
        var cut = _ctx.Render<Lumeo.Toolbar>(p => p
            .Add(t => t.Overflow, true)
            // VisibleCount defaults to -1 (auto-measure) — do not set it
            .AddChildContent("<button>Bold</button><button>Italic</button><button>Underline</button>"));

        // The toolbar container must be present
        var toolbar = cut.Find("[role='toolbar']");
        Assert.NotNull(toolbar);

        // Because _autoVisibleCount == -1 the overflow trigger condition
        // (_overflowCtx.VisibleCount >= 0) is false, so child content is still rendered
        Assert.Contains("Bold", cut.Markup);
    }

    /// <summary>
    /// When VisibleCount is set explicitly to a positive integer, that value wins
    /// over auto-measurement and the overflow trigger is rendered immediately with
    /// the data-toolbar-overflow-trigger attribute that tells JS to skip it.
    /// </summary>
    [Fact]
    public void Overflow_ManualVisibleCount_Shows_Trigger_With_DataAttribute()
    {
        var cut = _ctx.Render<Lumeo.Toolbar>(p => p
            .Add(t => t.Overflow, true)
            .Add(t => t.VisibleCount, 2)
            .AddChildContent("<button>Bold</button><button>Italic</button><button>Underline</button>"));

        // With VisibleCount=2 and 3 children, the overflow trigger should be rendered.
        // The trigger wrapper carries data-toolbar-overflow-trigger so the JS skips it.
        var trigger = cut.Find("[data-toolbar-overflow-trigger]");
        Assert.NotNull(trigger);
    }
}
