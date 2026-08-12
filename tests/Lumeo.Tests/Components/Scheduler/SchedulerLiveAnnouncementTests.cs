using Bunit;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// ReUI parity — its event calendar lists "live-region announcements for
/// gestures" (https://reui.io/components/event-calendar, Accessibility).
/// Lumeo's first-party views had no <c>aria-live</c> region anywhere, so a
/// pointer drag produced no screen-reader signal at all: a committed move was
/// silent, and a REJECTED move — where the event visibly springs back — was
/// equally silent, leaving a non-sighted user with no way to tell that nothing
/// happened.
///
/// Every test below drives the real <c>[JSInvokable]</c> commit path the drag
/// engine calls, never a field or parameter, so a regression that removes the
/// announce call from that path cannot pass.
/// </summary>
public class SchedulerLiveAnnouncementTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerLiveAnnouncementTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly L.SchedulerEvent Standup =
        new("e1", "Standup", new DateTime(2026, 3, 10, 9, 0, 0), new DateTime(2026, 3, 10, 9, 30, 0));

    [Fact]
    public void The_Month_View_Renders_An_Empty_Polite_Live_Region_At_Mount()
    {
        // The region must EXIST before the first announcement: assistive tech
        // has to be observing the node when the text lands, so one created at
        // announce time is routinely missed.
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.Events, new[] { Standup }));

        var region = cut.Find("[data-testid='scheduler-live-region']");
        Assert.Equal("polite", region.GetAttribute("aria-live"));
        Assert.Equal("true", region.GetAttribute("aria-atomic"));
        Assert.Equal(string.Empty, region.TextContent.Trim());
    }

    [Fact]
    public async Task A_Committed_Month_Move_Is_Announced_With_The_New_Date()
    {
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.Events, new[] { Standup })
            .Add(c => c.OnEventChange, (L.SchedulerEvent _) => { }));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("e1", "2026-03-12"));

        var text = cut.Find("[data-testid='scheduler-live-region']").TextContent;
        Assert.Contains("Standup", text);
        // The announced date must be the one actually committed, spelled out
        // rather than the terse cell label ("12" alone says nothing aloud).
        Assert.Contains(new DateTime(2026, 3, 12).ToString("D"), text);
    }

    [Fact]
    public async Task A_Rejected_Month_Move_Is_Announced_Through_The_Path_The_Drag_Engine_Actually_Takes()
    {
        // Regression (Codex review of this PR, P1): the first version announced
        // from CommitDrag, which an ordinary rejection NEVER reaches — the JS
        // awaits ValidateDrop and returns without calling it. The original test
        // called CommitDrag directly and so drove a path the real engine does
        // not take for rejections, which is exactly why it passed while the
        // feature did not work. This drives NotifyDropRejected, the seam the
        // engine actually calls.
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.Events, new[] { Standup }));

        await cut.InvokeAsync(() => cut.Instance.NotifyDropRejected("e1"));

        var text = cut.Find("[data-testid='scheduler-live-region']").TextContent;
        Assert.Contains("Standup", text);
        Assert.DoesNotContain(new DateTime(2026, 3, 12).ToString("D"), text);
    }

    [Fact]
    public async Task A_Rejected_TimeGrid_Resize_Is_Not_Announced_As_A_Failed_Move()
    {
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 10))
            .Add(c => c.Events, new[] { Standup }));

        await cut.InvokeAsync(() => cut.Instance.NotifyDropRejected("e1|2026-03-10T09:00:00", "resize-end"));
        var resizeText = cut.Find("[data-testid='scheduler-live-region']").TextContent;

        await cut.InvokeAsync(() => cut.Instance.NotifyDropRejected("e1|2026-03-10T09:00:00", "move"));
        var moveText = cut.Find("[data-testid='scheduler-live-region']").TextContent;

        // Predicted-wrong result before the fix: identical strings, because the
        // one branch handled Move, ResizeStart and ResizeEnd alike.
        Assert.NotEqual(resizeText, moveText);
    }

    [Fact]
    public async Task Repeating_The_Same_Outcome_Mutates_The_Text_Without_Replacing_The_Region()
    {
        // Two earlier attempts were wrong here. Clearing and re-setting inside
        // one method does nothing (Blazor coalesces the renders). Keying the
        // element on an epoch does mutate the DOM, but by REPLACING the observed
        // node — and screen readers that only announce changes inside an
        // already-observed region can then miss everything (Codex review of this
        // PR, P2). So: same node, changed text.
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.Events, new[] { Standup }));

        await cut.InvokeAsync(() => cut.Instance.NotifyDropRejected("e1"));
        var first = cut.Find("[data-testid='scheduler-live-region']").TextContent;

        await cut.InvokeAsync(() => cut.Instance.NotifyDropRejected("e1"));
        var second = cut.Find("[data-testid='scheduler-live-region']").TextContent;

        // The text really changed...
        Assert.NotEqual(first, second);
        // ...and only by an inaudible zero-width space, so it still reads the same.
        Assert.Equal(first.TrimEnd('​'), second.TrimEnd('​'));
        // Node identity is deliberately NOT asserted here: bUnit hands back a
        // fresh wrapper per Find, so Assert.Same compares wrappers rather than
        // DOM nodes and would fail even when the element never moved. The
        // "keep the node mounted" half of this fix is enforced by the markup
        // carrying no @key, not by something this test can observe.
        Assert.Single(cut.FindAll("[data-testid='scheduler-live-region']"));
    }

    [Fact]
    public async Task A_Move_Is_Not_Announced_When_There_Is_No_OnEventChange_Handler()
    {
        // Without a handler the drag is ghost-only: the chip snaps back and
        // Events is untouched, so announcing a move would describe something
        // the user can neither see nor keep (Codex review of this PR, P2).
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.Events, new[] { Standup }));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("e1", "2026-03-12"));

        Assert.Equal(string.Empty, cut.Find("[data-testid='scheduler-live-region']").TextContent.Trim());
    }

    [Fact]
    public async Task An_Adjusted_Month_Move_Announces_Where_It_Actually_Landed()
    {
        // A CanDrop adjustment lands the event somewhere other than where the
        // pointer was released, so announcing the PROPOSED date would tell the
        // user something that did not happen.
        var forced = new DateTime(2026, 3, 20, 9, 0, 0);
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.Events, new[] { Standup })
            .Add(c => c.OnEventChange, (L.SchedulerEvent _) => { })
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext _)
                => L.SchedulerDropResult.AcceptWith(new L.SchedulerDropAdjustment(forced, forced.AddMinutes(30)))));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("e1", "2026-03-12"));

        var text = cut.Find("[data-testid='scheduler-live-region']").TextContent;
        Assert.Contains(forced.ToString("D"), text);
        Assert.DoesNotContain(new DateTime(2026, 3, 12).ToString("D"), text);
    }

    [Fact]
    public void The_TimeGrid_View_Renders_The_Same_Live_Region()
    {
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 10))
            .Add(c => c.Events, new[] { Standup }));

        var region = cut.Find("[data-testid='scheduler-live-region']");
        Assert.Equal("polite", region.GetAttribute("aria-live"));
        Assert.Equal(string.Empty, region.TextContent.Trim());
    }
}
