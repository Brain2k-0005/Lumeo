using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Tests.Components.Badge;

public class BadgeRemovableTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BadgeRemovableTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // --- Bug #3 (keyboard-a11y): remove button must keep a visible focus ring ---

    [Fact]
    public void Removable_Button_Has_Visible_FocusVisible_Ring()
    {
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.IsRemovable, true)
            .AddChildContent("Tag"));

        var cls = cut.Find("button").GetAttribute("class") ?? "";

        // The fix replaces bare focus:outline-none with a focus-visible ring.
        Assert.Contains("focus-visible:ring-2", cls);
        Assert.Contains("focus-visible:ring-ring", cls);
        Assert.Contains("focus-visible:ring-offset-1", cls);
        // Must NOT strip the outline unconditionally (bare focus:outline-none).
        Assert.DoesNotContain("focus:outline-none", cls);
    }

    // --- Bug #4 (state-on-data-change): no internal _isRemoved latch ---

    [Fact]
    public void Remove_Click_Invokes_OnRemove_Without_SelfHiding()
    {
        var removed = false;
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.IsRemovable, true)
            .Add(b => b.OnRemove, EventCallback.Factory.Create(this, () => removed = true))
            .AddChildContent("Tag"));

        cut.Find("button").Click();

        // Consumer is notified...
        Assert.True(removed);
        // ...but visibility is driven by data, not an internal latch: the badge
        // content stays rendered until the consumer removes it from their model.
        Assert.Contains("Tag", cut.Markup);
        Assert.NotNull(cut.Find("div"));
    }

    [Fact]
    public void Reused_Instance_Shows_New_Content_After_Prior_Remove()
    {
        // Simulates an unkeyed list recycling this component instance for a
        // different item: a stale _isRemoved latch would hide the new badge.
        var cut = _ctx.Render<Lumeo.Badge>(p => p
            .Add(b => b.IsRemovable, true)
            .Add(b => b.OnRemove, EventCallback.Factory.Create(this, () => { }))
            .AddChildContent("First"));

        cut.Find("button").Click();

        // Re-render the same instance with new bound content (parameter change).
        cut.Render(p => p
            .Add(b => b.IsRemovable, true)
            .Add(b => b.OnRemove, EventCallback.Factory.Create(this, () => { }))
            .AddChildContent("Second"));

        Assert.Contains("Second", cut.Markup);
    }
}
