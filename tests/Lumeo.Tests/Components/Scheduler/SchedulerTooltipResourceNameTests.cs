using Bunit;
using Lumeo.SchedulerKernel;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Field report #464, finding D: the appointment hover tooltip showed the raw
/// <see cref="L.SchedulerEvent.ResourceId"/> (a database-shaped key, e.g. "room-3") instead of
/// its display text — <see cref="L.SchedulerMonthView"/>/<see cref="L.SchedulerTimeGridView"/>'s
/// own doc comments on <c>TooltipLabel</c> even documented this as a known limitation before the
/// fix: "neither view resolves a resource DISPLAY NAME, only the raw ResourceId".
///
/// <see cref="L.SchedulerMonthView.ResolveEventResourceName"/> is the new seam — wired by
/// <see cref="L.Scheduler"/> to its own <c>Resources</c> lookup — that lets these views show the
/// resource's <see cref="L.SchedulerResource.Title"/> instead. <c>TooltipLabel</c> itself is
/// <c>internal</c> (not <c>private</c>) specifically so these tests can drive the exact string it
/// builds directly, rather than reproducing the real 400ms hover-open + position-fixed dance
/// <see cref="L.Tooltip"/>/<see cref="L.TooltipContent"/> require to mount into the DOM.
/// </summary>
public class SchedulerTooltipResourceNameTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public SchedulerTooltipResourceNameTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly L.SchedulerEvent Booking =
        new("e1", "Standup", new DateTime(2026, 3, 10, 9, 0, 0), new DateTime(2026, 3, 10, 9, 30, 0), ResourceId: "room-3");

    private static readonly SchedulerEventInstance Instance =
        new("e1", new DateTime(2026, 3, 10, 9, 0, 0), new DateTime(2026, 3, 10, 9, 30, 0));

    // ── SchedulerMonthView ──────────────────────────────────────────────────

    [Fact]
    public void Month_View_Tooltip_Shows_The_Resolved_Resource_Title_Not_The_Raw_Id()
    {
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.Events, new[] { Booking })
            .Add(c => c.ResolveEventResourceName, (L.SchedulerEvent ev) => ev.ResourceId == "room-3" ? "Conference Room 3" : null));

        var label = cut.Instance.TooltipLabel(Booking, Instance);

        Assert.Contains("Conference Room 3", label, StringComparison.Ordinal);
        Assert.DoesNotContain("room-3", label, StringComparison.Ordinal);
    }

    [Fact]
    public void Month_View_Tooltip_Falls_Back_To_The_Raw_Id_When_No_Resolver_Is_Wired()
    {
        // Pre-existing behaviour preserved: a host that never supplies Resources (or a resolver)
        // still sees SOMETHING rather than nothing — just the raw id, exactly as before this fix.
        var cut = _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 15))
            .Add(c => c.Events, new[] { Booking }));

        var label = cut.Instance.TooltipLabel(Booking, Instance);

        Assert.Contains("room-3", label, StringComparison.Ordinal);
    }

    // ── SchedulerTimeGridView ────────────────────────────────────────────────

    [Fact]
    public void Time_Grid_View_Tooltip_Shows_The_Resolved_Resource_Title_Not_The_Raw_Id()
    {
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 9))
            .Add(c => c.Days, 7)
            .Add(c => c.Events, new[] { Booking })
            .Add(c => c.ResolveEventResourceName, (L.SchedulerEvent ev) => ev.ResourceId == "room-3" ? "Conference Room 3" : null));

        var label = cut.Instance.TooltipLabel(Booking, Instance);

        Assert.Contains("Conference Room 3", label, StringComparison.Ordinal);
        Assert.DoesNotContain("room-3", label, StringComparison.Ordinal);
    }

    [Fact]
    public void Time_Grid_View_Tooltip_Falls_Back_To_The_Raw_Id_When_No_Resolver_Is_Wired()
    {
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, new DateTime(2026, 3, 9))
            .Add(c => c.Days, 7)
            .Add(c => c.Events, new[] { Booking }));

        var label = cut.Instance.TooltipLabel(Booking, Instance);

        Assert.Contains("room-3", label, StringComparison.Ordinal);
    }

    // ── Full Scheduler wiring ────────────────────────────────────────────────

    [Fact]
    public void The_Full_Scheduler_Wires_Its_Resources_Titles_Into_The_Month_View_Tooltip()
    {
        // End-to-end through the public component: Scheduler.razor supplies Resources, and its
        // own ResolveFirstPartyResourceName lookup must reach the view's TooltipLabel.
        var resources = new[] { new L.SchedulerResource("room-3", "Conference Room 3") };

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialView, L.SchedulerView.Month)
            .Add(c => c.InitialDate, new DateTime(2026, 3, 15))
            .Add(c => c.Events, new[] { Booking })
            .Add(c => c.Resources, resources));

        var monthView = cut.FindComponent<L.SchedulerMonthView>();
        var label = monthView.Instance.TooltipLabel(Booking, Instance);

        Assert.Contains("Conference Room 3", label, StringComparison.Ordinal);
        Assert.DoesNotContain("room-3", label, StringComparison.Ordinal);
    }
}
