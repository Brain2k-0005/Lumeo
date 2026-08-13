using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Controlled/uncontrolled semantics of Scheduler's <c>Events</c>/<c>EventsChanged</c> pair.
///
/// <para>
/// A drag optimistically merges the new window into the component's own working copy BEFORE
/// invoking <c>EventsChanged</c>. In controlled mode a parent that vetoes the drag — by
/// re-rendering with its original collection unchanged — must see the component roll back,
/// not leave the optimistic drag stuck on screen. In UNCONTROLLED mode the opposite holds: a
/// stale re-render that re-supplies the same collection the parent always passes must not
/// claw back a local edit.
/// </para>
///
/// <para>
/// These used to drive the FullCalendar bridge's <c>JsOnEventChange</c> and assert on the
/// <c>scheduler.setEvents</c> calls that followed. With the wrapper gone there is no bridge
/// and no JS push to count, so they drive the real path — the month view's own
/// <c>CommitDrag</c> — and assert on what the grid actually renders. That is a stronger
/// check than counting interop calls ever was: it pins what the user sees.
/// </para>
/// </summary>
public class SchedulerControlledRollbackTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // A fixed mid-month anchor so both the original and the dragged day are always inside
    // the rendered grid, whatever today happens to be.
    private static readonly DateTime Anchor = new(2026, 6, 15);
    private static readonly DateTime OriginalDay = new(2026, 6, 15);
    private static readonly DateTime DraggedDay = new(2026, 6, 22);

    private static L.SchedulerEvent Meeting =>
        new("e1", "Meeting", OriginalDay.AddHours(9), OriginalDay.AddHours(10));

    private static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The day cell the chip is currently rendered in — the thing a user actually sees.</summary>
    private static string DayOfChip(IRenderedComponent<L.Scheduler> cut, string eventId)
    {
        var chip = cut.FindAll($"[data-event-id='{eventId}']").First();
        var cell = chip.Closest("[data-cell-date]");
        Assert.NotNull(cell);
        return cell!.GetAttribute("data-cell-date")!;
    }

    private static Task DragTo(IRenderedComponent<L.Scheduler> cut, DateTime day) =>
        cut.InvokeAsync(() => cut.FindComponent<L.SchedulerMonthView>().Instance.CommitDrag("e1", Iso(day)));

    // --- Controlled: veto rolls back ---

    [Fact]
    public async Task Controlled_Veto_Rolls_The_Chip_Back_To_The_Bound_Day()
    {
        var original = Meeting;
        IRenderedComponent<L.Scheduler>? cut = null;

        var veto = EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(this, (IEnumerable<L.SchedulerEvent> _) =>
        {
            // The parent rejects the drag by re-rendering with its collection unchanged.
            cut!.Render(p =>
            {
                p.Add(c => c.InitialDate, Anchor);
                p.Add(c => c.Events, new[] { original });
                p.Add(c => c.EventsChanged, EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(this, (IEnumerable<L.SchedulerEvent> _2) => { }));
            });
        });

        cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { original })
            .Add(c => c.EventsChanged, veto));

        await DragTo(cut, DraggedDay);

        // The rollback is what the user sees: the chip is back on the bound day, not
        // stranded on the day the drag optimistically moved it to.
        Assert.Equal(Iso(OriginalDay), DayOfChip(cut, "e1"));
    }

    // --- Controlled: an accepted drag keeps the new day ---

    [Fact]
    public async Task Controlled_Accepted_Drag_Keeps_The_New_Day()
    {
        IRenderedComponent<L.Scheduler>? cut = null;

        var accept = EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(this, (IEnumerable<L.SchedulerEvent> incoming) =>
        {
            cut!.Render(p =>
            {
                p.Add(c => c.InitialDate, Anchor);
                p.Add(c => c.Events, incoming.ToArray());
                p.Add(c => c.EventsChanged, EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(this, (IEnumerable<L.SchedulerEvent> _2) => { }));
            });
        });

        cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { Meeting })
            .Add(c => c.EventsChanged, accept));

        await DragTo(cut, DraggedDay);

        // The parent's echo of our own push must be recognised as exactly that, and not
        // mistaken for an unrelated change that resets the in-flight edit.
        Assert.Equal(Iso(DraggedDay), DayOfChip(cut, "e1"));
    }

    // --- Controlled: a genuinely new collection is adopted ---

    [Fact]
    public void Controlled_Programmatic_Reset_Is_Adopted_Without_A_Prior_Drag()
    {
        var noOp = EventCallback.Factory.Create<IEnumerable<L.SchedulerEvent>>(this, (IEnumerable<L.SchedulerEvent> _) => { });

        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { Meeting })
            .Add(c => c.EventsChanged, noOp));

        var replacement = new L.SchedulerEvent("e2", "Replacement",
            OriginalDay.AddHours(13), OriginalDay.AddHours(14));
        cut.Render(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { replacement })
            .Add(c => c.EventsChanged, noOp));

        Assert.NotEmpty(cut.FindAll("[data-event-id='e2']"));
        Assert.Empty(cut.FindAll("[data-event-id='e1']"));
    }

    // --- Uncontrolled: a stale re-render must not claw back a local edit ---

    [Fact]
    public async Task Uncontrolled_Local_Drag_Survives_A_Rerender_With_The_Same_Events()
    {
        var original = Meeting;

        // No EventsChanged bound — Events is uncontrolled and the component owns the copy.
        var cut = _ctx.Render<L.Scheduler>(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { original }));

        await DragTo(cut, DraggedDay);
        Assert.Equal(Iso(DraggedDay), DayOfChip(cut, "e1"));

        // The parent re-renders for an unrelated reason, re-supplying the SAME collection
        // it always passes — it never tracked the drag.
        cut.Render(p => p
            .Add(c => c.InitialDate, Anchor)
            .Add(c => c.Events, new[] { original }));

        // That stale re-render must not snap the chip back to the pre-drag day.
        Assert.Equal(Iso(DraggedDay), DayOfChip(cut, "e1"));
    }
}
