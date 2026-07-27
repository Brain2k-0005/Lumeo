using System.Globalization;
using System.Linq;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 5 (PR #379, feat/gantt-v3) — 2 P1 + 7 P2 findings, mostly
/// follow-ups on round 4's own changes. See docs/superpowers/gantt-v3-cx5-report.md
/// for the full per-finding writeup. Findings #4 (RTL tree-offset side) and
/// #9 (RTL pre-range clamp) get their own precise bUnit assertions here in
/// addition to the E2E specs (GanttV3RtlTests) — the exact scroll-target
/// ARITHMETIC is a pure C# computation, testable deterministically without a
/// real browser, while the E2E specs prove the resulting DOM/CSSOM behavior.
/// </summary>
public class GanttV3CodexRound5Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound5Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── GanttBar: pointer click + tap-to-pin composition (P1 #1, P2 #2/#3) ──

    [Fact]
    public async Task GanttBar_Click_On_The_Inner_Content_Invokes_OnTaskClick_Once()
    {
        var clickCount = 0;
        L.GanttTask? clicked = null;
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 9));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.OnTaskClick, t => { clicked = t; clickCount++; }));

        await cut.Find("[data-task-id='t1'] > div").TriggerEventAsync("onclick", new MouseEventArgs());

        Assert.Same(task, clicked);
        Assert.Equal(1, clickCount);
    }

    [Fact]
    public async Task GanttBar_Touch_Tap_Still_Pins_The_Tooltip_Open()
    {
        // Bug fix (Codex round 5, P1 #1 / P2 #2): proves Tooltip's OWN
        // onclick="HandleTap" binding (touch tap-to-pin) — bound directly on
        // ITS OWN root element, entirely separate from GanttBar's new onclick
        // handler on the inner content div — is untouched by this fix.
        // Triggered directly on the OUTER [data-task-id] element (where
        // Tooltip's handler actually lives), independent of any assumption
        // about cross-element event bubbling in bUnit's headless DOM.
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 9));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.OnTaskClick, _ => { }));

        var wrapper = cut.Find("[data-task-id='t1']");
        await wrapper.TriggerEventAsync("onpointerdown", new PointerEventArgs { PointerType = "touch" });
        await wrapper.TriggerEventAsync("onclick", new MouseEventArgs());

        var content = cut.Find("[role='tooltip']");
        Assert.Equal("open", content.GetAttribute("data-state"));
    }

    [Fact]
    public void GanttBar_Without_OnTaskClick_Stays_Focusable_But_Claims_No_Button_Role()
    {
        // Bug fix (Codex round 5, P2 #3): role="button" (and the keyboard/
        // click handlers) only apply when OnTaskClick actually has a
        // delegate — a bar with nothing listening for activation has no
        // business claiming an interactive role. tabindex + aria-label stay
        // unconditional (focus is still meaningful — see InnerAttributes'
        // own remarks on Tooltip's focusin-driven show-on-focus behavior).
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 9));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d));

        var interactive = cut.Find("[data-task-id='t1'] > div");
        Assert.Equal("0", interactive.GetAttribute("tabindex"));
        Assert.NotNull(interactive.GetAttribute("aria-label"));
        Assert.Null(interactive.GetAttribute("role"));
    }

    // ── GanttArrowLayer: window-crossing culling (P2 #7) ────────────────────

    [Fact]
    public void GanttArrowLayer_Keeps_An_Arrow_Whose_Endpoints_Straddle_The_Visible_Window_On_Opposite_Sides()
    {
        // Bug fix (Codex round 5, P2 #7): the round-4 culling check tested
        // source/target EACH individually against the window — a source
        // above it AND a target below it both satisfy "individually outside"
        // even though the edge's own span crosses straight through the
        // window, so it was wrongly culled. Placed at row 5 / row 70 (via 69
        // unrelated filler rows in between — GanttArrowLayer derives RowIndex
        // from Rows' own LIST POSITION, not a stored field) with a
        // window of [20, 50]: neither endpoint individually falls inside it,
        // but the edge's own [5, 70] interval fully brackets it.
        var tasks = new List<L.GanttTask>();
        for (var i = 0; i < 71; i++)
        {
            if (i == 5) tasks.Add(new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4)));
            else if (i == 70) tasks.Add(new L.GanttTask("t2", "Build", D(2026, 1, 6), D(2026, 1, 8), Dependencies: new[] { "t1" }));
            else tasks.Add(new L.GanttTask($"filler-{i}", $"Filler {i}", D(2026, 1, 1), D(2026, 1, 2)));
        }
        var rows = GanttRowModel.BuildVisibleRows(tasks, new HashSet<string>());
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, D(2026, 1, 1), D(2026, 3, 1))[0];

        var cut = _ctx.Render<L.GanttArrowLayer>(p => p
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Origin, origin)
            .Add(c => c.ColumnWidth, GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth)
            .Add(c => c.BarHeight, GanttScale.DefaultBarHeight)
            .Add(c => c.Width, 4000d)
            .Add(c => c.Height, 3000d)
            .Add(c => c.VisibleRowRange, ((int Start, int End)?)(20, 50)));

        Assert.Single(cut.FindAll(".lumeo-gantt-v3-arrow"));
    }

    // ── Gantt3: RTL scroll-offset side (P1 #4) ──────────────────────────────

    [Fact]
    public void Gantt3_Does_Not_Add_The_Tree_Width_Offset_To_The_Scroll_Target_Under_Rtl()
    {
        // Bug fix (Codex round 5, P1 #4): under RTL, the outer flex row
        // wrapping GanttTree + GanttTimeline reverses its children's VISUAL
        // order (see Gantt3.ScrollHostLeadingOffset's own remarks), so
        // Timeline's own origin sits at the scrollable content's physical 0
        // instead of past the tree's width — the round-4 fix added
        // GanttScale.TreePaneWidth unconditionally whenever a tree pane is
        // shown, which only holds under LTR. The two mount-time scroll
        // targets (LTR vs RTL, otherwise identical inputs) must differ by
        // EXACTLY TreePaneWidth.
        var tasks = new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 13), D(2026, 1, 17), GroupLabel: "G") };

        var ltrInterop = new TrackingInteropService();
        var ltrCtx = new BunitContext();
        ltrCtx.AddLumeoServices();
        ltrCtx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(ltrInterop);
        ltrCtx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, tasks).Add(c => c.ViewMode, L.GanttViewMode.Day).Add(c => c.ShowTreePane, true));
        var ltrTarget = ltrInterop.GanttV3ScrollToXCalls.Last();

        var rtlInterop = new TrackingInteropService();
        var rtlCtx = new BunitContext();
        rtlCtx.AddLumeoServices();
        rtlCtx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(rtlInterop);
        rtlCtx.Render<L.DirectionProvider>(p => p
            .Add(d => d.Direction, Lumeo.Services.LayoutDirection.Rtl)
            .AddChildContent<L.Gantt3>(g => g.Add(c => c.Tasks, tasks).Add(c => c.ViewMode, L.GanttViewMode.Day).Add(c => c.ShowTreePane, true)));
        var rtlTarget = rtlInterop.GanttV3ScrollToXCalls.Last();

        Assert.True(Math.Abs((ltrTarget - rtlTarget) - GanttScale.TreePaneWidth) < 0.01,
            $"expected the LTR and RTL scroll targets to differ by exactly TreePaneWidth ({GanttScale.TreePaneWidth}), got ltr={ltrTarget}, rtl={rtlTarget}, delta={ltrTarget - rtlTarget}");

        ltrCtx.Dispose();
        rtlCtx.Dispose();
    }

    // ── Gantt3: view-mode switch reads the live scroll center (P2 #5) ──────

    [Fact]
    public void Gantt3_ViewMode_Switch_Reads_The_Live_Scroll_Center_When_Available()
    {
        // Bug fix (Codex round 5, P2 #5): the round-4 fix recentered on the
        // outgoing VisibleRange's own MIDPOINT — a proxy that goes stale the
        // moment a user pans the DOM manually with no corresponding
        // VisibleRange change at all. Gantt3 now reads the pane's ACTUAL
        // live scroll center via a new interop round-trip first. This test
        // only asserts the MECHANISM fires (the interop is actually
        // consulted before recentering) — the resulting VisibleRange is
        // private, and the concrete DATE math is already covered by
        // GanttScaleTests' PixelToDate round-trip coverage; the end-to-end
        // BEHAVIORAL outcome (a manually-panned-to bar staying visible after
        // the switch) is covered by GanttV3ScrollCenteringTests' own E2E spec.
        var tasks = new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 5)) };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        _interop.GanttV3ScrollCenterXToReturn = 500d;
        var scrollCallCountBefore = _interop.GanttV3GetScrollCenterXCallCount;
        var scrollToXCallCountBefore = _interop.GanttV3ScrollToXCallCount;

        var monthToggle = cut.FindAll("button").First(b => b.TextContent.Trim() == "Month");
        monthToggle.Click();

        Assert.True(_interop.GanttV3GetScrollCenterXCallCount > scrollCallCountBefore,
            "expected the view-mode switch to read the live scroll center");
        Assert.True(_interop.GanttV3ScrollToXCallCount > scrollToXCallCountBefore,
            "expected the view-mode switch to still request a scroll-to-center");
    }
}
