using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 3, T1 — foundations: perf + options plumbing. Three
/// deliverables, each covered here at the level bUnit's headless DOM can
/// actually prove (real virtualization/interop-call-suppression under an
/// ACTUAL scroll viewport is E2E-only — see
/// <c>GanttV3ArrowVirtualizationTests</c>'s own remarks on this same
/// limitation — bUnit renders every <c>&lt;Virtualize&gt;</c> item
/// unconditionally):
///
/// <list type="bullet">
/// <item><b>RowTrackItems virtualization</b>: the per-task-row drag-create
/// hit target used to be a separate, un-virtualized <c>@foreach</c> loop
/// rendered BEFORE the row canvas's <c>&lt;Virtualize&gt;</c> block. It now
/// renders INSIDE the same virtualized item as its own row's bar — proven
/// here structurally (the track div is now a DOM DESCENDANT of the
/// <c>.lumeo-gantt-v3-row-item</c> wrapper, not a preceding sibling), with
/// the actual "fewer than total materialize" proof living in
/// <c>GanttV3RowTrackVirtualizationTests</c> (E2E, mirrors
/// <c>GanttV3StickyHeaderTests</c>' identical bar/tree hard-count pattern).</item>
/// <item><b>GanttInteropOptions</b>: replaces a growing <c>HashCode.Combine</c>
/// options-hash with a record — proven here via the INTEROP CALL COUNT
/// (<c>TrackingInteropService.GanttV3RegisterDragCallCount</c>), the same
/// observable the old hash-based gate was already keeping low: an unchanged
/// options snapshot across a re-render must not re-invoke
/// <c>GanttV3RegisterDragAsync</c>, and a genuinely changed one must.</item>
/// <item><b>Task-index dictionary</b>: replaces <c>FindTask</c>'s O(rows) LINQ
/// scan with a <c>Dictionary&lt;string, GanttTask&gt;</c> rebuilt in
/// <c>OnParametersSet</c> — proven here by mutating <c>Tasks</c> across a
/// re-render (add/remove) and asserting a JSInvokable (<c>CommitProgress</c>)
/// resolves against the NEW set, not a stale cached one.</item>
/// </list>
/// </summary>
public class GanttV3Phase3FoundationsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3Phase3FoundationsTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);
    private static L.GanttTask Task1 => new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6));

    // ── RowTrackItems virtualization (structural surface) ────────────────────

    [Fact]
    public void Task_Row_Track_Div_Is_Nested_Inside_Its_Own_Virtualized_Row_Item_And_Precedes_The_Bar()
    {
        // Predicted wrong value if this regressed to the old separate-loop
        // shape: the track div's ParentElement would be the row-canvas'
        // top-level ".relative" wrapper (RowsContainerStyle's own ancestor),
        // never carrying "lumeo-gantt-v3-row-item" at all — proven by
        // temporarily reverting this exact change (see the T1 report's
        // disable-check evidence).
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.AllowCreate, true));

        var track = cut.Find("[data-gantt-row-track][data-row-key='task:t1']");
        var parent = track.ParentElement;
        Assert.NotNull(parent);
        Assert.Contains("lumeo-gantt-v3-row-item", parent!.GetAttribute("class") ?? "");

        // The bar for the SAME row must be the track div's own sibling
        // (both direct children of the same per-row Virtualize item wrapper)
        // and must appear LATER in DOM order — paint order in the shared
        // stacking context (RowsContainerStyle's own `.relative` ancestor;
        // this wrapper establishes no positioning context of its own) is
        // what makes the bar win the hit-test over its own row's track.
        var bar = cut.Find("[data-task-id='t1']");
        Assert.Same(parent, bar.ParentElement);

        var html = parent.InnerHtml;
        var trackIndex = html.IndexOf("data-gantt-row-track", StringComparison.Ordinal);
        var barIndex = html.IndexOf("data-task-id=\"t1\"", StringComparison.Ordinal);
        Assert.True(trackIndex >= 0 && barIndex >= 0 && trackIndex < barIndex,
            $"expected the row-track div ({trackIndex}) to precede the bar ({barIndex}) in DOM order");
    }

    // ── GanttInteropOptions (equality-based re-registration) ─────────────────

    [Fact]
    public void Unchanged_Options_Across_A_ReRender_Do_Not_Reregister_The_Drag_Listener()
    {
        // Predicted wrong value if the record-equality gate were removed
        // (always re-registering): CallCount would read 2, not 1, after the
        // second render below — verified by temporarily disabling the gate
        // (see the T1 report's disable-check evidence).
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.AllowCreate, true));

        Assert.Equal(1, _interop.GanttV3RegisterDragCallCount);

        // A re-render driven by something OTHER than a drag-relevant
        // parameter (mirrors an ancestor's unrelated StateHasChanged) — every
        // field GanttInteropOptions carries is re-supplied with the IDENTICAL
        // value. SyncDragRegistrationAsync runs again on this pass (every
        // OnAfterRenderAsync, not just firstRender — see its own remarks),
        // but the options snapshot is structurally equal to the last
        // registered one.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.AllowCreate, true));

        Assert.Equal(1, _interop.GanttV3RegisterDragCallCount);
    }

    [Fact]
    public void A_ColumnWidth_Change_Across_A_ReRender_Does_Reregister_With_The_New_Value()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        Assert.Equal(1, _interop.GanttV3RegisterDragCallCount);

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.ColumnWidth, 99));

        Assert.Equal(2, _interop.GanttV3RegisterDragCallCount);
        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastGanttV3DragOptions);
        Assert.Equal(99, options["columnWidth"]);
    }

    [Fact]
    public void A_CanDrop_Null_To_NonNull_Transition_Across_A_ReRender_Does_Reregister_With_HasCanDrop_True()
    {
        // CanDrop's null-ness (not any date it might later reject) is the
        // options-relevant fact — folded into GanttInteropOptions.HasCanDrop
        // (a bool), unlike EffectiveColumnWidth/PixelsPerDay/Origin which are
        // numeric/DateTime. Exercising this field specifically guards against
        // a record migration that silently dropped it (a record's positional
        // constructor makes an omitted field a compile error, but a copy-paste
        // mistake reusing the SAME source expression for two fields would not
        // be caught by the compiler).
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        Assert.Equal(1, _interop.GanttV3RegisterDragCallCount);
        var firstOptions = Assert.IsType<Dictionary<string, object?>>(_interop.LastGanttV3DragOptions);
        Assert.Equal(false, firstOptions["hasCanDrop"]);

        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.CanDrop, (L.GanttTask _, GanttScheduleDropContext _) => true));

        Assert.Equal(2, _interop.GanttV3RegisterDragCallCount);
        var secondOptions = Assert.IsType<Dictionary<string, object?>>(_interop.LastGanttV3DragOptions);
        Assert.Equal(true, secondOptions["hasCanDrop"]);
    }

    // ── Task-index dictionary (rebuilt alongside Rows) ────────────────────────

    [Fact]
    public async Task FindTask_Resolves_A_Task_Added_By_A_Later_Parameter_Pass()
    {
        // Predicted wrong value if the index were only built once (e.g. in
        // OnInitialized rather than OnParametersSet): CommitProgress below
        // would silently no-op (FindTask returns null for "t2", the original
        // JSInvokable guard's own "any task with an active drag is, by
        // definition, currently rendered" contract broken) — verified by
        // temporarily moving the rebuild call out of OnParametersSet (see
        // the T1 report's disable-check evidence).
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 20))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        var task2 = new L.GanttTask("t2", "Build", D(2026, 1, 8), D(2026, 1, 12));
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1, task2 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 20))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => { received = u; }));

        await cut.InvokeAsync(() => cut.Instance.CommitProgress("t2", 55));

        Assert.NotNull(received);
        Assert.Equal("t2", received!.Task.Id);
        Assert.Equal(55, received.Task.Progress);
    }

    [Fact]
    public async Task FindTask_Stops_Resolving_A_Task_Removed_By_A_Later_Parameter_Pass()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => { fired = true; }));

        // t1 removed entirely — the index must drop it, not keep resolving
        // against a stale snapshot from the first render.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitProgress("t1", 55));

        Assert.False(fired);
    }
}
