using System.Globalization;
using System.Linq;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 4 (PR #379, feat/gantt-v3) — 10 P2 refinement findings (no P1):
/// 3 RTL corners, a11y, arrow-layer virtualization, locale completeness (see
/// P2LocalizationSweepTests' extended NewKeys array), a v2-parity gap
/// (CustomClass), and a view-mode-switch recenter gap. See
/// docs/superpowers/gantt-v3-cx4-report.md for the full per-finding writeup.
/// Findings #1 (tree spacer sticky), #2 (tree-width scroll offset), #9 (RTL
/// header order), and #10 (RTL tree pinning) are covered by E2E specs — all
/// four are fundamentally real-browser layout/CSSOM concerns (sticky
/// positioning, computed direction, actual pixel geometry after a real
/// scroll) that bUnit's headless DOM can't exercise meaningfully.
/// </summary>
public class GanttV3CodexRound4Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound4Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── GanttArrowLayer: row-index virtualization (finding #3) ──────────────

    [Fact]
    public void GanttArrowLayer_Culls_An_Arrow_Whose_Endpoints_Are_Both_Outside_The_Visible_Row_Range()
    {
        var upstream = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4));
        var downstream = new L.GanttTask("t2", "Build", D(2026, 1, 6), D(2026, 1, 8), Dependencies: new[] { "t1" });
        var rows = GanttRowModel.BuildVisibleRows(new[] { upstream, downstream }, new HashSet<string>());
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, D(2026, 1, 1), D(2026, 1, 12))[0];

        // t1/t2 sit at row indices 0/1 — a visible window of [10, 20] excludes both.
        var cut = _ctx.Render<L.GanttArrowLayer>(p => p
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Origin, origin)
            .Add(c => c.ColumnWidth, GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth)
            .Add(c => c.BarHeight, GanttScale.DefaultBarHeight)
            .Add(c => c.Width, 2000d)
            .Add(c => c.Height, 200d)
            .Add(c => c.VisibleRowRange, ((int Start, int End)?)(10, 20)));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-arrow"));
    }

    [Fact]
    public void GanttArrowLayer_Keeps_An_Arrow_With_One_Endpoint_Inside_The_Visible_Row_Range()
    {
        // Same fixture, but the range [0, 0] includes ONLY the source row (t1) —
        // the arrow must still render (see VisibleRowRange's own remarks: a
        // half-visible connection still needs a line leading to its visible end).
        var upstream = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4));
        var downstream = new L.GanttTask("t2", "Build", D(2026, 1, 6), D(2026, 1, 8), Dependencies: new[] { "t1" });
        var rows = GanttRowModel.BuildVisibleRows(new[] { upstream, downstream }, new HashSet<string>());
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, D(2026, 1, 1), D(2026, 1, 12))[0];

        var cut = _ctx.Render<L.GanttArrowLayer>(p => p
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Origin, origin)
            .Add(c => c.ColumnWidth, GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth)
            .Add(c => c.BarHeight, GanttScale.DefaultBarHeight)
            .Add(c => c.Width, 2000d)
            .Add(c => c.Height, 200d)
            .Add(c => c.VisibleRowRange, ((int Start, int End)?)(0, 0)));

        Assert.Single(cut.FindAll(".lumeo-gantt-v3-arrow"));
    }

    [Fact]
    public void GanttArrowLayer_Shows_Every_Arrow_When_VisibleRowRange_Is_Null()
    {
        // Default (unchanged prior behavior) — no culling until a real scroll
        // position has actually been reported.
        var upstream = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4));
        var downstream = new L.GanttTask("t2", "Build", D(2026, 1, 6), D(2026, 1, 8), Dependencies: new[] { "t1" });
        var rows = GanttRowModel.BuildVisibleRows(new[] { upstream, downstream }, new HashSet<string>());
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, D(2026, 1, 1), D(2026, 1, 12))[0];

        var cut = _ctx.Render<L.GanttArrowLayer>(p => p
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Origin, origin)
            .Add(c => c.ColumnWidth, GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth)
            .Add(c => c.BarHeight, GanttScale.DefaultBarHeight)
            .Add(c => c.Width, 2000d)
            .Add(c => c.Height, 200d));

        Assert.Single(cut.FindAll(".lumeo-gantt-v3-arrow"));
    }

    [Fact]
    public async Task GanttTimeline_Registers_Vertical_Scroll_Tracking_On_Mount_And_Unregisters_On_Dispose()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));

        Assert.Equal(1, _interop.GanttV3RegisterVerticalScrollTrackingCallCount);
        Assert.Equal(0, _interop.GanttV3UnregisterVerticalScrollTrackingCallCount);

        await cut.Instance.DisposeAsync();

        Assert.Equal(1, _interop.GanttV3UnregisterVerticalScrollTrackingCallCount);
    }

    [Fact]
    public async Task GanttTimeline_Computes_A_Culled_VisibleRowRange_From_A_Simulated_Scroll_Report()
    {
        // 60 flat tasks, one per row (RowHeight=36px) — simulate a scroll
        // report landing partway down and assert SOME (but not all) bars
        // remain, proving the reported scrollTop/clientHeight actually drives
        // GanttArrowLayer's culling (a full end-to-end assertion via the
        // rendered bar count for the tasks the arrows connect, since
        // GanttArrowLayer's own VisibleRowRange isn't otherwise observable
        // from outside GanttTimeline).
        var tasks = new List<L.GanttTask>();
        for (var i = 0; i < 60; i++)
        {
            var dependsOn = i > 0 ? new[] { $"t{i - 1}" } : null;
            tasks.Add(new L.GanttTask($"t{i}", $"Task {i}", D(2026, 1, 1).AddDays(i), D(2026, 1, 1).AddDays(i + 1), Dependencies: dependsOn));
        }
        var rows = GanttRowModel.BuildVisibleRows(tasks, new HashSet<string>());

        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 3, 1)));

        var arrowsBefore = cut.FindAll(".lumeo-gantt-v3-arrow").Count;
        Assert.Equal(59, arrowsBefore); // unculled: one arrow per consecutive pair, VisibleRowRange still null

        // Simulate scrolling to row ~30 with a small viewport (a few rows tall).
        // cut.InvokeAsync marshals onto bUnit's render dispatcher — OnGanttV3VerticalScroll
        // calls StateHasChanged, which (like any Blazor component method) requires
        // running on that dispatcher rather than the test's own thread.
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 30 * GanttScale.RowHeight, clientHeight: 3 * GanttScale.RowHeight));

        var arrowsAfter = cut.FindAll(".lumeo-gantt-v3-arrow").Count;
        Assert.True(arrowsAfter > 0 && arrowsAfter < arrowsBefore,
            $"expected the scroll report to cull SOME (but not all) arrows, got {arrowsAfter} (was {arrowsBefore})");
    }

    // ── GanttBar: keyboard accessibility (finding #5) ───────────────────────

    [Fact]
    public void GanttBar_Is_Keyboard_Focusable_With_An_Aria_Label_Naming_The_Task_And_Its_Dates()
    {
        // Bug fix (Codex round 5, P1 #1 / P2 #2): tabindex/role/aria-label
        // moved off the outer [data-task-id] wrapper (Tooltip's own root)
        // onto GanttBar's inner content div — see GanttBar.InnerAttributes'
        // own remarks for why. It's the OUTER element's sole direct child
        // (TooltipTrigger's AsChild mode adds no wrapper of its own).
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 9));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.OnTaskClick, _ => { }));

        var interactive = cut.Find("[data-task-id='t1'] > div");
        Assert.Equal("0", interactive.GetAttribute("tabindex"));
        Assert.Equal("button", interactive.GetAttribute("role"));
        var label = interactive.GetAttribute("aria-label");
        Assert.Contains("Design", label);
        Assert.Contains("Jan 2, 2026", label);
        Assert.Contains("Jan 9, 2026", label);
    }

    [Fact]
    public void GanttBar_Milestone_Aria_Label_Normalizes_To_A_Single_Point_Date()
    {
        // Same Start/End normalization as the round-3 tooltip fix (P2 #5) —
        // a milestone's announced date shouldn't imply a span it doesn't have.
        var task = new L.GanttTask("m1", "Kickoff", D(2026, 3, 8), D(2026, 3, 20), IsMilestone: true);
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 22d));

        var label = cut.Find("[data-milestone='true'] > div").GetAttribute("aria-label");
        Assert.Contains("Mar 8, 2026", label);
        Assert.DoesNotContain("Mar 20, 2026", label);
    }

    [Fact]
    public async Task GanttBar_Enter_Keydown_Invokes_OnTaskClick()
    {
        L.GanttTask? clicked = null;
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 9));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.OnTaskClick, t => clicked = t));

        await cut.Find("[data-task-id='t1'] > div").TriggerEventAsync("onkeydown", new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Assert.Same(task, clicked);
    }

    [Fact]
    public async Task GanttBar_Space_Keydown_Invokes_OnTaskClick()
    {
        L.GanttTask? clicked = null;
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 9));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.OnTaskClick, t => clicked = t));

        await cut.Find("[data-task-id='t1'] > div").TriggerEventAsync("onkeydown", new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = " " });

        Assert.Same(task, clicked);
    }

    [Fact]
    public async Task GanttBar_Other_Keydown_Does_Not_Invoke_OnTaskClick()
    {
        var invoked = false;
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 9));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.OnTaskClick, _ => invoked = true));

        await cut.Find("[data-task-id='t1'] > div").TriggerEventAsync("onkeydown", new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Tab" });

        Assert.False(invoked);
    }

    // ── GanttBar: CustomClass forwarding (finding #7) ───────────────────────

    [Fact]
    public void GanttBar_Applies_Task_CustomClass_To_The_Wrapper()
    {
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 9), CustomClass: "my-highlight");
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d));

        Assert.Contains("my-highlight", cut.Find("[data-task-id='t1']").GetAttribute("class"));
    }

    [Fact]
    public void GanttBar_Renders_Without_A_CustomClass_When_The_Task_Has_None()
    {
        // Regression guard: Cx.Merge(..., null, ...) must not throw or leave
        // a literal "null" token in the class list.
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 9));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d));

        Assert.DoesNotContain("null", cut.Find("[data-task-id='t1']").GetAttribute("class"));
    }

    // ── Gantt3: view-mode switch recenter (finding #8) ──────────────────────

    [Fact]
    public void Gantt3_ViewMode_Toggle_Requests_A_Scroll_Recenter()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var callCountAtMount = _interop.GanttV3ScrollToXCallCount;

        var monthToggle = cut.FindAll("button").First(b => b.TextContent.Trim() == "Month");
        monthToggle.Click();

        Assert.True(_interop.GanttV3ScrollToXCallCount > callCountAtMount,
            "expected the view-mode switch to request another scroll-to-center");
    }
}
