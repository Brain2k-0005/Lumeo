using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.ScrollArea;

/// <summary>
/// Product fix, not merely a pinning test (#a11y-depth SPECIAL list): verified the
/// viewport div carried no tabindex/@onkeydown of its own, so a keyboard-only user had
/// no way to reach or scroll it at all (WCAG 2.1.1 — mouse-drag scrollbars are the sole
/// interaction path). Fixed per the WAI-ARIA APG scrollable-region practice by adding
/// tabindex="0" unconditionally (overflow depends on runtime layout that can change
/// after mount, so the pattern keeps the stop present rather than trying to detect
/// overflow via JS — a non-overflowing ScrollArea is just a harmless no-op tab stop,
/// same as the W3C APG example) plus an opt-in AriaLabel for the accessible name.
/// </summary>
public class ScrollAreaKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public ScrollAreaKeyboardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Viewport_Is_In_The_Native_Tab_Order()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p.AddChildContent("content"));

        Assert.Equal("0", cut.Find("div").GetAttribute("tabindex"));
    }

    [Fact]
    public void AriaLabel_Renders_As_The_Accessible_Name()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p
            .Add(s => s.AriaLabel, "Changelog")
            .AddChildContent("content"));

        Assert.Equal("Changelog", cut.Find("div").GetAttribute("aria-label"));
    }

    [Fact]
    public void AriaLabel_Is_Omitted_When_Not_Supplied()
    {
        var cut = _ctx.Render<L.ScrollArea>(p => p.AddChildContent("content"));

        Assert.False(cut.Find("div").HasAttribute("aria-label"));
    }
}
