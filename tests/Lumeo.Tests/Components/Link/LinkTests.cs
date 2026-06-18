using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Link;

public class LinkTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public LinkTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Anchor_Element()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .Add(l => l.Href, "/home")
            .AddChildContent("Home"));

        var a = cut.Find("a");
        Assert.NotNull(a);
        Assert.Equal("Home", a.TextContent);
    }

    [Fact]
    public void Href_Is_Set_On_Anchor()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .Add(l => l.Href, "/about")
            .AddChildContent("About"));

        Assert.Equal("/about", cut.Find("a").GetAttribute("href"));
    }

    [Fact]
    public void Default_Variant_Has_Primary_Text_Class()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .AddChildContent("Link"));

        var cls = cut.Find("a").GetAttribute("class");
        Assert.Contains("text-primary", cls);
    }

    [Fact]
    public void External_True_Adds_Target_Blank()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .Add(l => l.External, true)
            .Add(l => l.Href, "https://example.com")
            .AddChildContent("External"));

        var a = cut.Find("a");
        Assert.Equal("_blank", a.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", a.GetAttribute("rel"));
    }

    [Fact]
    public void Muted_Variant_Has_Muted_Foreground_Class()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .Add(l => l.Variant, "muted")
            .AddChildContent("Muted link"));

        var cls = cut.Find("a").GetAttribute("class");
        Assert.Contains("text-muted-foreground", cls);
    }

    // ── #296: inert/null href, disabled, rel-safety, aria-current ────────────

    [Fact]
    public void Null_Href_Does_Not_Emit_Empty_Href_Attribute()
    {
        // href="" navigates to the page root — a null Href must be inert (no attr).
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .AddChildContent("No destination"));

        var a = cut.Find("a");
        Assert.False(a.HasAttribute("href"));
    }

    [Fact]
    public void Disabled_Link_Is_Inert_And_Removed_From_Tab_Order()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .Add(l => l.Href, "/somewhere")
            .Add(l => l.Disabled, true)
            .AddChildContent("Disabled"));

        var a = cut.Find("a");
        Assert.False(a.HasAttribute("href"));          // can't navigate
        Assert.Equal("true", a.GetAttribute("aria-disabled"));
        Assert.Equal("-1", a.GetAttribute("tabindex"));
        Assert.Contains("pointer-events-none", a.GetAttribute("class"));
    }

    [Fact]
    public void Target_Blank_Via_AdditionalAttributes_Gets_Safe_Rel()
    {
        // A forgotten External flag on a target=_blank link must still be
        // protected against reverse tabnabbing.
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .Add(l => l.Href, "https://example.com")
            .Add(l => l.AdditionalAttributes, new Dictionary<string, object> { ["target"] = "_blank" })
            .AddChildContent("Off-origin"));

        var a = cut.Find("a");
        Assert.Equal("_blank", a.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", a.GetAttribute("rel"));
    }

    [Fact]
    public void Caller_Supplied_Rel_Is_Respected()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .Add(l => l.External, true)
            .Add(l => l.Href, "https://example.com")
            .Add(l => l.AdditionalAttributes, new Dictionary<string, object> { ["rel"] = "noopener" })
            .AddChildContent("Custom rel"));

        Assert.Equal("noopener", cut.Find("a").GetAttribute("rel"));
    }

    [Fact]
    public void AriaCurrent_Is_Emitted_When_Set()
    {
        var cut = _ctx.Render<Lumeo.Link>(p => p
            .Add(l => l.Href, "/active")
            .Add(l => l.AriaCurrent, "page")
            .AddChildContent("Active"));

        Assert.Equal("page", cut.Find("a").GetAttribute("aria-current"));
    }
}
