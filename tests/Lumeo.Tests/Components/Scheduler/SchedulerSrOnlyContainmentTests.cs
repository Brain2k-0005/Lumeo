using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Field report #464, finding B: the live-announcer <c>sr-only</c> span (see
/// <see cref="L.SchedulerMonthView"/>/<see cref="L.SchedulerTimeGridView"/>'s own live-region
/// remarks) is <c>position:absolute</c> with no offsets. Without a positioned ancestor of its
/// own, it escapes to whatever positioned ancestor happens to sit further up the HOST page's
/// tree instead of staying inside the scheduler's card, and can enlarge THAT ancestor's scroll
/// area — reported as "a dead scroll area" outside the card.
///
/// These tests pin the fix at the only level bUnit can actually observe layout containment:
/// the announcer's nearest ancestor carrying <c>relative</c> (or another non-static position)
/// must be the view's own root, not something further out. A test that only checked the class
/// list of the root for "relative" in isolation would pass on a REGRESSION that added
/// `relative` somewhere else in the tree instead of the root and left the actual escape intact,
/// so this asserts containment structurally: the sr-only span's closest positioned ancestor,
/// walking up from it, IS the documented root element.
/// </summary>
public class SchedulerSrOnlyContainmentTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerSrOnlyContainmentTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Month_View_Root_Is_Positioned_So_The_Live_Region_Cannot_Escape_It()
    {
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

        var region = cut.Find("[data-testid='scheduler-live-region']");
        // The region's PARENT is the view's root (it is the first child rendered under it) —
        // walking to the parent and checking ITS class list is exactly the containing-block
        // check: `sr-only` (position:absolute) is contained by the nearest ANCESTOR carrying a
        // non-static position, and that must be this element, not something the root itself
        // sits inside of.
        var root = region.ParentElement;
        Assert.NotNull(root);
        var classes = root!.ClassList;
        Assert.Contains("relative", classes);
    }

    [Fact]
    public void Time_Grid_View_Root_Is_Positioned_So_The_Live_Region_Cannot_Escape_It()
    {
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.Days, 7)
            .Add(c => c.Events, Array.Empty<L.SchedulerEvent>()));

        var region = cut.Find("[data-testid='scheduler-live-region']");
        var root = region.ParentElement;
        Assert.NotNull(root);
        Assert.Contains("relative", root!.ClassList);
    }

    [Fact]
    public void The_Full_Scheduler_Renders_Both_Positioned_View_Roots()
    {
        // End-to-end through the public component, so a regression that only fixed the
        // standalone views (or vice versa) is caught regardless of which entry point a
        // consumer actually renders.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, new DateTime(2026, 3, 15)));

        var region = cut.Find("[data-testid='scheduler-live-region']");
        Assert.Contains("relative", region.ParentElement!.ClassList);
    }
}
