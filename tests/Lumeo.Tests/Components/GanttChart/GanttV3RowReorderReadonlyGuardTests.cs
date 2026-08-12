using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 3, T6 fix-round precedent applied proactively — mirrors
/// <see cref="GanttV3ReadonlyGuardTests"/> EXACTLY (see that file's own class
/// remarks for the full "JS-side registration gating is not enough"
/// rationale, Phase 2's original lesson): <c>GanttTree.CommitRowReorder</c>
/// is reachable independent of whatever gated <c>registerRowReorderDrag</c>'s
/// own registration, because <c>unregisterRowReorderDrag</c> only detaches
/// the delegated <c>pointerdown</c> listener — a reorder gesture ALREADY in
/// flight at the moment of a mid-drag Readonly flip has its own
/// pointermove/pointerup handlers living in gantt-v3.js's per-drag closure
/// (attached directly to the grip element at pointerdown time), which
/// survives the flip untouched and still calls <c>CommitRowReorder</c> on
/// release. Exercises the guards DIRECTLY (invoking the JSInvokable, not
/// simulating a real drag) — the E2E suite
/// (<c>GanttV3RowSelectionReorderTests</c>, <c>Lumeo.Tests.E2E</c>) proves
/// gantt-v3.js itself never CALLS these when readonly; it cannot prove the
/// .NET side would reject a call that reached it anyway.
/// </summary>
public class GanttV3RowReorderReadonlyGuardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public GanttV3RowReorderReadonlyGuardTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // root1 -> [child1, child2] — a real 2-member sibling bucket.
    private static List<L.GanttTask> Tasks() => new()
    {
        new("root1", "Root", D(2026, 3, 1), D(2026, 3, 30)),
        new("child1", "Child One", D(2026, 3, 1), D(2026, 3, 10)) { ParentId = "root1" },
        new("child2", "Child Two", D(2026, 3, 10), D(2026, 3, 20)) { ParentId = "root1" },
    };

    private static IReadOnlyList<GanttVisibleRow> Rows() =>
        GanttRowModel.BuildVisibleRows(Tasks(), new HashSet<string>());

    [Fact]
    public async Task CommitRowReorder_NoOps_When_Readonly()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTree>(p => p
            .Add(c => c.Rows, Rows())
            .Add(c => c.Tasks, Tasks())
            .Add(c => c.AllowRowReorder, true)
            .Add(c => c.Readonly, true)
            .Add(c => c.OnRowReorder, (GanttRowReorder _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitRowReorder("child2", 0));

        Assert.False(fired);
    }

    [Fact]
    public async Task CommitRowReorder_NoOps_When_AllowRowReorder_False_Even_Without_Readonly()
    {
        var fired = false;
        var cut = _ctx.Render<L.GanttTree>(p => p
            .Add(c => c.Rows, Rows())
            .Add(c => c.Tasks, Tasks())
            .Add(c => c.AllowRowReorder, false)
            .Add(c => c.OnRowReorder, (GanttRowReorder _) => { fired = true; }));

        await cut.InvokeAsync(() => cut.Instance.CommitRowReorder("child2", 0));

        Assert.False(fired);
    }

    [Fact]
    public void ValidateRowDrop_Returns_True_When_Readonly_Even_When_CanDropRow_Would_Reject()
    {
        var canDropInvoked = false;
        var cut = _ctx.Render<L.GanttTree>(p => p
            .Add(c => c.Rows, Rows())
            .Add(c => c.Tasks, Tasks())
            .Add(c => c.AllowRowReorder, true)
            .Add(c => c.Readonly, true)
            .Add(c => c.CanDropRow, (L.GanttTask _, GanttDropContext _) => { canDropInvoked = true; return false; }));

        var result = cut.Instance.ValidateRowDrop("child2", 0);

        Assert.True(result);
        Assert.False(canDropInvoked);
    }

    [Fact]
    public async Task Mid_Drag_Readonly_Flip_CommitRowReorder_Still_NoOps()
    {
        // Real reachable path: a reorder drag starts while interactive, the
        // chart flips Readonly mid-drag, and the in-flight drag's own JS
        // closure still calls CommitRowReorder on release — this must no-op
        // just like starting from Readonly=true (see this file's own class
        // remarks — the Phase-2 lesson applied proactively to a NEW JS
        // registration channel).
        var fired = false;
        var cut = _ctx.Render<L.GanttTree>(p => p
            .Add(c => c.Rows, Rows())
            .Add(c => c.Tasks, Tasks())
            .Add(c => c.AllowRowReorder, true)
            .Add(c => c.Readonly, false)
            .Add(c => c.OnRowReorder, (GanttRowReorder _) => { fired = true; }));

        cut.Render(p => p
            .Add(c => c.Rows, Rows())
            .Add(c => c.Tasks, Tasks())
            .Add(c => c.AllowRowReorder, true)
            .Add(c => c.Readonly, true));

        await cut.InvokeAsync(() => cut.Instance.CommitRowReorder("child2", 0));

        Assert.False(fired);
    }

    [Fact]
    public void Mid_Drag_Readonly_Flip_ValidateRowDrop_Also_Permits_Unconditionally()
    {
        var canDropInvoked = false;
        var cut = _ctx.Render<L.GanttTree>(p => p
            .Add(c => c.Rows, Rows())
            .Add(c => c.Tasks, Tasks())
            .Add(c => c.AllowRowReorder, true)
            .Add(c => c.Readonly, false)
            .Add(c => c.CanDropRow, (L.GanttTask _, GanttDropContext _) => { canDropInvoked = true; return false; }));

        cut.Render(p => p
            .Add(c => c.Rows, Rows())
            .Add(c => c.Tasks, Tasks())
            .Add(c => c.AllowRowReorder, true)
            .Add(c => c.Readonly, true)
            .Add(c => c.CanDropRow, (L.GanttTask _, GanttDropContext _) => { canDropInvoked = true; return false; }));

        var result = cut.Instance.ValidateRowDrop("child2", 0);

        Assert.True(result);
        Assert.False(canDropInvoked);
    }
}
