using Lumeo.GanttV3;
using Xunit;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 3, T4 — the <see cref="GanttState"/>-LEVEL half of the
/// imperative API + attach/detach contract: pure unit tests against a fake
/// <see cref="IGanttStateHost"/>, with no Blazor rendering involved at all (the
/// COMPONENT-level integration — a real <c>Gantt3</c> attaching itself and
/// routing through its own scroll interop/view-mode ownership model — lives in
/// <c>GanttV3Phase3T4Tests</c>). This file exists to pin the attach/detach
/// POLICY precisely (second attach, detach-after-supersession, no-attachment)
/// independent of any component's own lifecycle quirks.
/// </summary>
public class GanttStateImperativeApiTests
{
    private static Lumeo.GanttTask GTask(string id, DateTime start, DateTime end) => new(id, id, start, end);

    /// <summary>Records every call it receives; never throws, always completes synchronously.</summary>
    private sealed class FakeHost : IGanttStateHost
    {
        public List<DateTime> ScrollToDateCalls { get; } = new();
        public List<GanttViewMode> SetViewModeCalls { get; } = new();

        public Task ScrollToDateAsync(DateTime date)
        {
            ScrollToDateCalls.Add(date);
            return Task.CompletedTask;
        }

        public Task SetViewModeAsync(GanttViewMode mode)
        {
            SetViewModeCalls.Add(mode);
            return Task.CompletedTask;
        }
    }

    // ── No attachment at all: documented no-op, not a throw ─────────────────

    [Fact]
    public async Task ScrollToDateAsync_With_Nothing_Attached_Completes_Without_Throwing()
    {
        var state = new GanttState();

        var task = state.ScrollToDateAsync(new DateTime(2026, 3, 1));

        await task; // must not throw/hang
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ScrollToTaskAsync_With_Nothing_Attached_Completes_Without_Throwing()
    {
        var state = new GanttState();
        state.SetTasks(new[] { GTask("t1", new DateTime(2026, 1, 1), new DateTime(2026, 1, 5)) });

        await state.ScrollToTaskAsync("t1");
    }

    [Fact]
    public void SetViewMode_With_Nothing_Attached_Does_Not_Throw()
    {
        var state = new GanttState();

        var ex = Record.Exception(() => state.SetViewMode(GanttViewMode.Month));

        Assert.Null(ex);
    }

    // ── Attach routes the imperative API to the host ────────────────────────

    [Fact]
    public async Task ScrollToDateAsync_After_Attach_Routes_To_The_Host_With_The_Exact_Date()
    {
        var state = new GanttState();
        var host = new FakeHost();
        state.Attach(host);

        var date = new DateTime(2026, 5, 17);
        await state.ScrollToDateAsync(date);

        Assert.Equal(new[] { date }, host.ScrollToDateCalls);
    }

    [Fact]
    public async Task ScrollToTaskAsync_Resolves_The_Tasks_Temporal_Midpoint_And_Routes_To_The_Host()
    {
        var state = new GanttState();
        var host = new FakeHost();
        state.Attach(host);
        // Jan 1 -> Jan 5: midpoint is Jan 3 (4-day span / 2 = 2 days from Start).
        state.SetTasks(new[] { GTask("t1", new DateTime(2026, 1, 1), new DateTime(2026, 1, 5)) });

        await state.ScrollToTaskAsync("t1");

        Assert.Equal(new[] { new DateTime(2026, 1, 3) }, host.ScrollToDateCalls);
    }

    [Fact]
    public async Task ScrollToTaskAsync_For_A_Milestone_Targets_Its_Own_Single_Date()
    {
        var state = new GanttState();
        var host = new FakeHost();
        state.Attach(host);
        var d = new DateTime(2026, 6, 1);
        state.SetTasks(new[] { GTask("m1", d, d) }); // Start == End -> milestone

        await state.ScrollToTaskAsync("m1");

        Assert.Equal(new[] { d }, host.ScrollToDateCalls);
    }

    [Fact]
    public async Task ScrollToTaskAsync_With_An_Unknown_Id_Is_A_No_Op_Not_A_Throw()
    {
        var state = new GanttState();
        var host = new FakeHost();
        state.Attach(host);
        state.SetTasks(new[] { GTask("t1", new DateTime(2026, 1, 1), new DateTime(2026, 1, 5)) });

        await state.ScrollToTaskAsync("does-not-exist");

        Assert.Empty(host.ScrollToDateCalls);
    }

    [Fact]
    public void SetViewMode_After_Attach_Routes_To_The_Host_With_The_Exact_Mode()
    {
        var state = new GanttState();
        var host = new FakeHost();
        state.Attach(host);

        state.SetViewMode(GanttViewMode.Week);

        Assert.Equal(new[] { GanttViewMode.Week }, host.SetViewModeCalls);
    }

    // ── Second attach: last attacher wins (design spec Phase 3, T4, hard part 1) ──

    [Fact]
    public async Task A_Second_Attach_Replaces_The_First_As_The_Imperative_Api_Target()
    {
        var state = new GanttState();
        var hostA = new FakeHost();
        var hostB = new FakeHost();

        state.Attach(hostA);
        state.Attach(hostB); // supersedes A — no error, A is simply no longer the target

        await state.ScrollToDateAsync(new DateTime(2026, 1, 1));
        state.SetViewMode(GanttViewMode.Year);

        Assert.Empty(hostA.ScrollToDateCalls);
        Assert.Empty(hostA.SetViewModeCalls);
        Assert.Single(hostB.ScrollToDateCalls);
        Assert.Single(hostB.SetViewModeCalls);
    }

    // ── Detach: only removes if still the current attachment ───────────────

    [Fact]
    public async Task Detach_Of_A_Superseded_Host_Does_Not_Clear_The_Newer_Attachment()
    {
        // "A component that is disposed and replaced" (design spec Phase 3, T4,
        // hard part 1): hostA is superseded by hostB, THEN hostA's own teardown
        // calls Detach(hostA) — this must be a no-op, since hostA is no longer
        // the current attachment; hostB must remain fully functional.
        var state = new GanttState();
        var hostA = new FakeHost();
        var hostB = new FakeHost();
        state.Attach(hostA);
        state.Attach(hostB);

        state.Detach(hostA);
        await state.ScrollToDateAsync(new DateTime(2026, 2, 2));

        Assert.Single(hostB.ScrollToDateCalls);
    }

    [Fact]
    public async Task Detach_Of_The_Current_Host_Makes_The_Imperative_Api_A_No_Op_Again()
    {
        var state = new GanttState();
        var host = new FakeHost();
        state.Attach(host);

        state.Detach(host);
        var task = state.ScrollToDateAsync(new DateTime(2026, 3, 3));
        state.SetViewMode(GanttViewMode.Month);

        await task;
        Assert.Empty(host.ScrollToDateCalls);
        Assert.Empty(host.SetViewModeCalls);
    }

    [Fact]
    public async Task A_Detached_Host_Cannot_Be_Resurrected_By_A_Later_Detach_Of_A_Different_Host()
    {
        // Guards the "cannot be resurrected" half of the contract explicitly:
        // once hostA is genuinely detached, a LATER, unrelated Detach call (of a
        // host that was never even attached) must not somehow restore it.
        var state = new GanttState();
        var hostA = new FakeHost();
        var unrelated = new FakeHost();
        state.Attach(hostA);
        state.Detach(hostA);

        state.Detach(unrelated); // never attached — must be a harmless no-op

        await state.ScrollToDateAsync(new DateTime(2026, 4, 4));
        Assert.Empty(hostA.ScrollToDateCalls);
    }

    [Fact]
    public async Task Re_Attaching_After_A_Genuine_Detach_Works_As_A_Fresh_Attach()
    {
        var state = new GanttState();
        var host = new FakeHost();
        state.Attach(host);
        state.Detach(host);

        state.Attach(host); // a legitimate NEW attach, not a resurrection of the old one
        await state.ScrollToDateAsync(new DateTime(2026, 5, 5));

        Assert.Single(host.ScrollToDateCalls);
    }
}
