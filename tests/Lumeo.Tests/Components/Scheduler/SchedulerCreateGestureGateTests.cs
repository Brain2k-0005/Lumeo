using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// The drag-create gesture must only be offered where something can receive it.
///
/// <para>
/// Reported as "danach passiert nichts": dragging across empty time in a view whose
/// <c>OnDateSelect</c> is unwired drew a ghost rectangle, and releasing it did nothing —
/// <c>CommitCreate</c> reaches a callback with no delegate and drops the range. An
/// affordance that cannot do anything reads as a broken component.
/// </para>
///
/// <para>
/// Asserted on the options pushed to JS rather than by driving a synthetic pointer: a
/// browser-level attempt produced zero ghosts both with and without the fix, so it proved
/// nothing in either direction.
/// </para>
/// </summary>
public class SchedulerCreateGestureGateTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly DateTime Day = new(2026, 6, 15);

    private static bool SelectableIn(object? options) =>
        (bool)Assert.IsType<Dictionary<string, object?>>(options)["selectable"]!;

    [Fact]
    public void The_time_grid_offers_no_create_gesture_without_a_listener()
    {
        _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, Day)
            .Add(c => c.Days, 1)
            .Add(c => c.Selectable, true));

        Assert.False(SelectableIn(_interop.LastSchedulerViewsTimeGridDragOptions));
    }

    [Fact]
    public void The_time_grid_offers_it_as_soon_as_someone_listens()
    {
        _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, Day)
            .Add(c => c.Days, 1)
            .Add(c => c.Selectable, true)
            .Add(c => c.OnDateSelect, (L.SchedulerDateRange _) => { }));

        Assert.True(SelectableIn(_interop.LastSchedulerViewsTimeGridDragOptions));
    }

    [Fact]
    public void Selectable_false_still_wins_over_a_listener()
    {
        // The explicit opt-out must not be overridden by merely having a callback.
        _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, Day)
            .Add(c => c.Days, 1)
            .Add(c => c.Selectable, false)
            .Add(c => c.OnDateSelect, (L.SchedulerDateRange _) => { }));

        Assert.False(SelectableIn(_interop.LastSchedulerViewsTimeGridDragOptions));
    }

    [Fact]
    public void The_month_view_gates_the_same_way()
    {
        _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, Day)
            .Add(c => c.Selectable, true));

        Assert.False(SelectableIn(_interop.LastSchedulerViewsMonthDragOptions));
    }

    [Fact]
    public void The_month_view_offers_it_with_a_listener()
    {
        _ctx.Render<L.SchedulerMonthView>(p => p
            .Add(c => c.AnchorDate, Day)
            .Add(c => c.Selectable, true)
            .Add(c => c.OnDateSelect, (L.SchedulerDateRange _) => { }));

        Assert.True(SelectableIn(_interop.LastSchedulerViewsMonthDragOptions));
    }
}
