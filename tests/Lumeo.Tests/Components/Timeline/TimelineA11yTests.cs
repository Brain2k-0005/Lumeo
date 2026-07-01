using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Timeline;

// Regression for battle-wave1 finding #66 (Timeline / keyboard-a11y):
// ActiveIndex "you are here" / completed / pending state was conveyed by colour
// only — no aria-current, role, or list semantics. The fix adds role="list" on
// the Timeline root, role="listitem" + aria-label per item, and aria-current="step"
// on the active item (mirroring StepsItem's a11y treatment).
public class TimelineA11yTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public TimelineA11yTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Timeline_Root_Has_Role_List()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "One")));

        // The Timeline root <div> must expose list semantics.
        Assert.Equal("list", cut.Find("[role='list']").GetAttribute("role"));
    }

    [Fact]
    public void Each_Item_Has_Role_ListItem()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "One"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "Two"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "Three")));

        var items = cut.FindAll("[role='listitem']");
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void Active_Item_Carries_AriaCurrent_Step_Others_Do_Not()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Animated, true)
            .Add(t => t.ActiveIndex, 1)
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "One"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "Two"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "Three")));

        // Exactly the item at ActiveIndex gets aria-current="step".
        var current = cut.FindAll("[aria-current='step']");
        Assert.Single(current);

        // And that item is the second one (index 1 = "Two").
        var listItems = cut.FindAll("[role='listitem']");
        Assert.Equal("step", listItems[1].GetAttribute("aria-current"));
        Assert.Null(listItems[0].GetAttribute("aria-current"));
        Assert.Null(listItems[2].GetAttribute("aria-current"));
    }

    [Fact]
    public void AriaLabel_Encodes_Completed_Current_And_Pending_State()
    {
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .Add(t => t.Animated, true)
            .Add(t => t.ActiveIndex, 1)
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "One"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "Two"))
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "Three")));

        var listItems = cut.FindAll("[role='listitem']");
        // index 0 < ActiveIndex => completed
        Assert.Equal("One (completed)", listItems[0].GetAttribute("aria-label"));
        // index 1 == ActiveIndex => current
        Assert.Equal("Two (current)", listItems[1].GetAttribute("aria-label"));
        // index 2 > ActiveIndex => pending
        Assert.Equal("Three (pending)", listItems[2].GetAttribute("aria-label"));
    }

    [Fact]
    public void Legacy_IsActive_Marks_AriaCurrent_Without_ActiveIndex()
    {
        // When ActiveIndex is not driving, the per-item IsActive flag must still
        // surface aria-current="step" (the static-progress fallback path).
        var cut = _ctx.Render<Lumeo.Timeline>(p => p
            .AddChildContent<Lumeo.TimelineItem>(i => i.Add(x => x.Title, "One"))
            .AddChildContent<Lumeo.TimelineItem>(i => i
                .Add(x => x.Title, "Two")
                .Add(x => x.IsActive, true)));

        var current = cut.FindAll("[aria-current='step']");
        Assert.Single(current);
        Assert.Equal("Two (current)", current[0].GetAttribute("aria-label"));
    }
}
