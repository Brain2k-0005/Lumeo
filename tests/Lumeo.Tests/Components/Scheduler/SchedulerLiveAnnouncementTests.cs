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
            .Add(c => c.Events, new[] { Standup }));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("e1", "2026-03-12"));

        var text = cut.Find("[data-testid='scheduler-live-region']").TextContent;
        Assert.Contains("Standup", text);
        // The announced date must be the one actually committed, spelled out
        // rather than the terse cell label ("12" alone says nothing aloud).
        Assert.Contains(new DateTime(2026, 3, 12).ToString("D"), text);
    }

    [Fact]
    public async Task A_Rejected_Month_Move_Is_Announced_Rather_Than_Failing_Silently()
    {
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.Events, new[] { Standup })
            .Add(c => c.CanDrop, (L.SchedulerEvent _, L.SchedulerScheduleDropContext _)
                => L.SchedulerDropResult.Reject));

        await cut.InvokeAsync(() => cut.Instance.CommitDrag("e1", "2026-03-12"));

        var text = cut.Find("[data-testid='scheduler-live-region']").TextContent;
        Assert.Contains("Standup", text);
        // Predicted-wrong result before the fix: empty — the drag was rejected
        // and nothing was said.
        Assert.NotEqual(string.Empty, text.Trim());
        // And it must NOT claim a successful move.
        Assert.DoesNotContain(new DateTime(2026, 3, 12).ToString("D"), text);
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
