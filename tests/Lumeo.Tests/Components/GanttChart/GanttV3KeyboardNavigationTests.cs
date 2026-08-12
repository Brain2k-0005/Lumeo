using System.Linq;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Arrow-key navigation — GanttV3's own accessibility gap: before this,
/// EVERY <see cref="L.GanttBar"/> carried an unconditional <c>tabindex="0"</c>
/// (a keyboard user had to Tab through every single bar to reach the last
/// one) and no arrow key did anything at all beyond the pre-existing
/// Enter/Space activation. This suite covers the roving-tabindex focus
/// model (<see cref="L.GanttBar.FocusedTaskId"/>), plain-arrow focus
/// movement (<see cref="L.GanttTimeline"/>'s own <c>MoveBarFocusAsync</c>),
/// and Shift+Arrow schedule nudges routed through the SAME
/// <c>OnTaskUpdate</c> commit gate a mouse drag uses
/// (<c>GanttTaskUpdateSource.Keyboard</c>) — including a mutation-style
/// rejection test proving the gate is a REAL gate, not a bypassed one.
/// </summary>
public class GanttV3KeyboardNavigationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3KeyboardNavigationTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static List<L.GanttTask> Fixture() => new()
    {
        new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)),
        new("t2", "Build", D(2026, 1, 10), D(2026, 1, 20)),
        new("t3", "Ship", D(2026, 1, 22), D(2026, 1, 25)),
    };

    // ── GanttBar: roving tabindex ─────────────────────────────────────────

    [Fact]
    public void FocusedTaskId_Null_Keeps_Every_Bar_At_Tabindex_Zero()
    {
        // Standalone-usage contract: GanttTimeline always supplies a non-null
        // FocusedTaskId once it renders any bar (EffectiveFocusedTaskId's own
        // first-row fallback) — a bUnit test rendering GanttBar in isolation,
        // with FocusedTaskId left at its default (null), must keep the EXACT
        // prior "every bar is tabindex=0" behavior. Predicted wrong value if
        // this regressed: "-1" (roving would incorrectly kick in with no
        // FocusedTaskId set at all).
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 6));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d));

        Assert.Equal("0", cut.Find("[data-task-id='t1'] > div").GetAttribute("tabindex"));
    }

    [Fact]
    public void FocusedTaskId_Set_Gives_Tabindex_Zero_Only_To_The_Matching_Bar()
    {
        // A NON-matching FocusedTaskId ("t2") on a bar rendering "t1" must
        // demote it to -1. Predicted wrong value against the pre-fix
        // behavior: "0" (the old unconditional tabindex).
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 6));
        var notFocused = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.FocusedTaskId, "t2"));
        Assert.Equal("-1", notFocused.Find("[data-task-id='t1'] > div").GetAttribute("tabindex"));

        var focused = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.FocusedTaskId, "t1"));
        Assert.Equal("0", focused.Find("[data-task-id='t1'] > div").GetAttribute("tabindex"));
    }

    [Fact]
    public async Task Arrow_Keydown_Invokes_OnKeyNavigation_Even_Without_OnTaskClick()
    {
        // OnKeyNavigation must be wired independently of OnTaskClick — a bar
        // with no click consumer at all must still respond to arrow keys
        // (GanttTimeline always wires this, whether or not a consumer set
        // OnTaskClick). Predicted wrong value against a naive port that kept
        // onkeydown gated on OnTaskClick.HasDelegate alone: onkeydown would
        // never be wired here at all, and this assert would see `received`
        // still null.
        GanttBarKeyNavigation? received = null;
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 6));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.OnKeyNavigation, (GanttBarKeyNavigation n) => received = n));

        await cut.Find("[data-task-id='t1'] > div")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        Assert.NotNull(received);
        Assert.Same(task, received!.Task);
        Assert.Equal("ArrowRight", received.Key);
        Assert.True(received.ShiftKey);
    }

    [Fact]
    public async Task Enter_Keydown_Still_Only_Invokes_OnTaskClick_Not_OnKeyNavigation()
    {
        // Regression guard: extending onkeydown to also cover arrows must not
        // reroute Enter/Space through OnKeyNavigation.
        L.GanttTask? clicked = null;
        GanttBarKeyNavigation? navigated = null;
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 6));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.OnTaskClick, t => clicked = t)
            .Add(c => c.OnKeyNavigation, (GanttBarKeyNavigation n) => navigated = n));

        await cut.Find("[data-task-id='t1'] > div").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        Assert.Same(task, clicked);
        Assert.Null(navigated);
    }

    [Fact]
    public async Task Mid_Flight_Combination_Drift_Reregisters_With_Live_Rules_Instead_Of_Unregistering()
    {
        // GanttTimeline ALWAYS wires OnKeyNavigation (unlike OnTaskClick,
        // which a consumer controls) — so removing OnTaskClick mid-flight on
        // a GanttTimeline-hosted bar must NOT fully unregister the
        // prevent-default channel the way the round-17 regression test (a
        // STANDALONE GanttBar, OnTaskClick as the sole driver) does; arrow
        // navigation still needs Space... no, still needs its OWN 4 rules.
        // Predicted wrong value under the naive single-flag port: either a
        // stray UnregisterPreventDefaultKeys call (arrow keys would stop
        // being protected), or the STALE rule set (5 rules, Space included)
        // being left in place forever (Space would incorrectly keep
        // scrolling being suppressed after OnTaskClick is gone).
        var rows = new List<GanttVisibleRow> { new(GanttRowKind.Task, Fixture()[0], "Design", 0, false, null, false) };
        var gate = new TaskCompletionSource();
        _interop.RegisterPreventDefaultKeysGate = gate;

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Fixture()[0] })
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.OnTaskClick, EventCallback.Factory.Create<L.GanttTask>(this, _ => { })));

        Assert.Single(_interop.RegisterPreventDefaultKeysElementIds);
        var barId = _interop.RegisterPreventDefaultKeysElementIds[0];
        Assert.Contains(" ", _interop.RegisterPreventDefaultKeysRules[barId].Select(r => r.Key)); // Space rule present at mount

        // Remove OnTaskClick while the mount-time registration is still gated.
        cut.Render(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { Fixture()[0] })
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.OnTaskClick, default(EventCallback<L.GanttTask>)));

        gate.SetResult();
        _interop.RegisterPreventDefaultKeysGate = null;
        for (var i = 0; i < 100 && _interop.RegisterPreventDefaultKeysElementIds.Count < 2; i++)
            await Task.Delay(10);

        // A SECOND register call landed (the drift correction) — never an
        // unregister — and its rule set dropped Space but kept all four
        // arrow keys.
        Assert.Empty(_interop.UnregisterPreventDefaultKeysElementIds);
        Assert.Equal(2, _interop.RegisterPreventDefaultKeysElementIds.Count);
        var finalRules = _interop.RegisterPreventDefaultKeysRules[barId].Select(r => r.Key).ToList();
        Assert.DoesNotContain(" ", finalRules);
        Assert.Equal(new[] { "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight" }, finalRules);
    }

    // ── GanttTimeline: roving focus movement ──────────────────────────────

    [Fact]
    public void First_Task_Row_Is_Focusable_By_Default_Before_Any_Interaction()
    {
        // EffectiveFocusedTaskId's own fallback: Tab must always reach
        // exactly one bar, never zero. Predicted wrong value if the fallback
        // were missing: tabindex="-1" on every bar (no row ever "0").
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31)));

        Assert.Equal("0", cut.Find("[data-task-id='t1'] > div").GetAttribute("tabindex"));
        Assert.Equal("-1", cut.Find("[data-task-id='t2'] > div").GetAttribute("tabindex"));
        Assert.Equal("-1", cut.Find("[data-task-id='t3'] > div").GetAttribute("tabindex"));
    }

    [Fact]
    public async Task ArrowDown_Moves_Roving_Focus_To_The_Next_Visible_Row_And_Calls_FocusBar()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31)));

        await cut.Find("[data-task-id='t1'] > div").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });

        // The JS-focus request must name the NEXT task (t2) — predicted wrong
        // value against a reversed step direction: "t3" or "t1" (unchanged).
        Assert.Equal("t2", Assert.Single(_interop.GanttV3FocusBarCalls));
        Assert.Equal("0", cut.Find("[data-task-id='t2'] > div").GetAttribute("tabindex"));
        Assert.Equal("-1", cut.Find("[data-task-id='t1'] > div").GetAttribute("tabindex"));
    }

    [Fact]
    public async Task ArrowUp_Moves_Roving_Focus_To_The_Previous_Visible_Row()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31)));

        // Move to t2 first (ArrowDown from the default t1), then ArrowUp back.
        await cut.Find("[data-task-id='t1'] > div").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await cut.Find("[data-task-id='t2'] > div").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });

        Assert.Equal(new[] { "t2", "t1" }, _interop.GanttV3FocusBarCalls);
        Assert.Equal("0", cut.Find("[data-task-id='t1'] > div").GetAttribute("tabindex"));
    }

    [Fact]
    public async Task ArrowLeft_And_ArrowRight_Alias_Up_And_Down_For_Focus_Movement()
    {
        // Design decision: GanttV3 rows are one-task-per-row, so Left/Right
        // (no modifier) alias the same "adjacent visible row" move Up/Down
        // perform, rather than being dead keys.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31)));

        await cut.Find("[data-task-id='t1'] > div").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("t2", _interop.GanttV3FocusBarCalls[^1]);

        await cut.Find("[data-task-id='t2'] > div").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.Equal("t1", _interop.GanttV3FocusBarCalls[^1]);
    }

    [Fact]
    public async Task ArrowUp_On_The_First_Row_Does_Not_Move_Focus_Or_Call_FocusBar()
    {
        // Boundary case — no further row in that direction. Predicted wrong
        // value against a missing bounds check: an IndexOutOfRangeException,
        // or a spurious FocusBar("t1") call (focusing the SAME row again).
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31)));

        await cut.Find("[data-task-id='t1'] > div").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });

        Assert.Empty(_interop.GanttV3FocusBarCalls);
        Assert.Equal("0", cut.Find("[data-task-id='t1'] > div").GetAttribute("tabindex")); // unchanged
    }

    // ── GanttTimeline: Shift+Arrow schedule nudge -> OnTaskUpdate gate ─────

    [Fact]
    public async Task ShiftArrowRight_Moves_The_Focused_Task_By_One_Day_Tagged_Keyboard()
    {
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => received = u));

        await cut.Find("[data-task-id='t1'] > div")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        Assert.NotNull(received);
        Assert.Equal(GanttTaskUpdateSource.Keyboard, received!.Source);
        Assert.Equal(D(2026, 1, 3), received.Task.Start); // was Jan 2 -> +1 day
        Assert.Equal(D(2026, 1, 7), received.Task.End);   // was Jan 6 -> +1 day
    }

    [Fact]
    public async Task ShiftArrowLeft_Moves_The_Focused_Task_Back_One_Day()
    {
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => received = u));

        await cut.Find("[data-task-id='t1'] > div")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowLeft", ShiftKey = true });

        Assert.Equal(D(2026, 1, 1), received!.Task.Start);
        Assert.Equal(D(2026, 1, 5), received.Task.End);
    }

    [Fact]
    public async Task ShiftArrowUp_Grows_The_Focused_Task_End_By_One_Day_Start_Unchanged()
    {
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => received = u));

        await cut.Find("[data-task-id='t1'] > div")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowUp", ShiftKey = true });

        Assert.Equal(GanttTaskUpdateSource.Keyboard, received!.Source);
        Assert.Equal(D(2026, 1, 2), received.Task.Start); // unchanged
        Assert.Equal(D(2026, 1, 7), received.Task.End);   // was Jan 6 -> +1 day
    }

    [Fact]
    public async Task ShiftArrowDown_Shrinks_The_Focused_Task_End_By_One_Day()
    {
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => received = u));

        await cut.Find("[data-task-id='t1'] > div")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown", ShiftKey = true });

        Assert.Equal(D(2026, 1, 2), received!.Task.Start); // unchanged
        Assert.Equal(D(2026, 1, 5), received.Task.End);    // was Jan 6 -> -1 day
    }

    [Fact]
    public async Task ShiftArrowDown_Clamps_The_End_At_Start_For_A_Single_Day_Task()
    {
        // Mirrors CommitDrag's own resize-end clamp: End must never cross
        // Start. Predicted wrong value without the clamp: Dec 31 (End one
        // day BEFORE Start), an inverted range.
        var single = new List<L.GanttTask> { new("t1", "Kickoff", D(2026, 1, 1), D(2026, 1, 1)) };
        GanttTaskUpdate? received = null;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, single)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => received = u));

        await cut.Find("[data-task-id='t1'] > div")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown", ShiftKey = true });

        Assert.Equal(D(2026, 1, 1), received!.Task.Start);
        Assert.Equal(D(2026, 1, 1), received.Task.End); // clamped, not Dec 31
    }

    [Fact]
    public async Task Readonly_Suppresses_Keyboard_Timing_Changes_Entirely()
    {
        var updateFired = false;
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 31))
            .Add(c => c.Readonly, true)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => updateFired = true));

        await cut.Find("[data-task-id='t1'] > div")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        Assert.False(updateFired);
    }

    // ── GanttChart: the FULL commit gate — reject / accept / adjust ───────
    // Mutation-test target: forcing GanttChart's own gate evaluation to
    // ignore a Reject verdict (see the accompanying report) makes
    // Keyboard_Nudge_Rejected_By_The_Gate_Never_Commits below FAIL — the
    // task's dates would have moved despite the gate saying no.

    [Fact]
    public async Task Keyboard_Nudge_Rejected_By_The_Gate_Never_Commits()
    {
        var state = new GanttState();
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => GanttUpdateResult.Reject));

        await cut.Find("[data-task-id='t1'] > div")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        // Predicted wrong value under a bypassed gate: Start/End would have
        // moved to Jan 3/Jan 7 regardless of the Reject verdict.
        var t1 = state.Tasks.Single(t => t.Id == "t1");
        Assert.Equal(D(2026, 1, 2), t1.Start);
        Assert.Equal(D(2026, 1, 6), t1.End);
    }

    [Fact]
    public async Task Keyboard_Nudge_Accepted_By_The_Gate_Commits_To_State()
    {
        var state = new GanttState();
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate _) => GanttUpdateResult.Accept));

        await cut.Find("[data-task-id='t1'] > div")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        var t1 = state.Tasks.Single(t => t.Id == "t1");
        Assert.Equal(D(2026, 1, 3), t1.Start);
        Assert.Equal(D(2026, 1, 7), t1.End);
    }

    [Fact]
    public async Task Keyboard_Nudge_Adjustment_Overrides_The_Raw_Proposal()
    {
        // AcceptWith: the gate snaps the committed dates to something OTHER
        // than the raw +1-day proposal — proves the adjustment path (not
        // just accept/reject) works identically for a keyboard-sourced edit.
        var state = new GanttState();
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.OnTaskUpdate, (GanttTaskUpdate u) => GanttUpdateResult.AcceptWith(
                new GanttUpdateAdjustment(Start: D(2026, 1, 20), End: D(2026, 1, 24)))));

        await cut.Find("[data-task-id='t1'] > div")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        var t1 = state.Tasks.Single(t => t.Id == "t1");
        Assert.Equal(D(2026, 1, 20), t1.Start); // the ADJUSTMENT's value, not Jan 3
        Assert.Equal(D(2026, 1, 24), t1.End);
    }

    [Fact]
    public async Task Keyboard_Nudge_With_No_Gate_Wired_Commits_Unconditionally()
    {
        // OnTaskUpdate unset (default null) accepts unconditionally — the
        // same "no consumer gate = every edit lands" contract CommitDrag
        // already documents.
        var state = new GanttState();
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        await cut.Find("[data-task-id='t1'] > div")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });

        var t1 = state.Tasks.Single(t => t.Id == "t1");
        Assert.Equal(D(2026, 1, 3), t1.Start);
        Assert.Equal(D(2026, 1, 7), t1.End);
    }
}
