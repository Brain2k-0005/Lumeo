using System.Linq;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Codex round 6 (PR #379, feat/gantt-v3) - 2 P1 + 3 P2 findings, all
/// refinements of round 5's own changes. See docs/superpowers/gantt-v3-cx6-report.md
/// for the full per-finding writeup. Finding #3 (RTL convention/formula
/// mismatch) is JS-only (see gantt-v3.js's own detectRtlScrollConvention
/// remarks); finding #4 (Space scrolling the viewport) and finding #5 (v2
/// click-payload merge) get their own coverage elsewhere -
/// GanttV3KeyboardActivationTests (E2E) and GanttInteropTests.cs
/// (JsOnTaskClick_Merges_Only_ParentId_Onto_The_Renderer_Normalized_Payload)
/// respectively.
/// </summary>
public class GanttV3CodexRound6Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3CodexRound6Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // -- Gantt3: center preservation uses the continuous inverse (P1 #1) --

    [Fact]
    public void ViewMode_Switch_From_Month_Preserves_A_MidMonth_Center_Date_Instead_Of_Snapping()
    {
        // Bug fix (Codex round 6, P1 #1): Gantt3.ResolveCurrentCenterDateAsync
        // used to go through GanttScale.PixelToDate, the DRAG-snapping
        // inverse, which rounds to the nearest WHOLE month in Month view. A
        // single task anchored on 2026-01-15 makes Month mode's VisibleRange
        // (and therefore its Origin) exactly [2025-01-01, 2027-01-01) - see
        // ComputeInitialRange's own 12-month padding for Month mode - so a
        // scroll-center pixel of colWidth*14.5 (120*14.5=1740) is EXACTLY
        // half-way through month 15 from that origin (2026-03-01), landing
        // continuously on 2026-03-16 (day 1 + 15 days). The OLD snapping
        // behavior would instead round 14.5 UP to whole month 15 (JS
        // Math.round's tie-break - see GanttScale.RoundToInt's own remarks),
        // landing on 2026-04-01 - a full month off, and reproducibly
        // DIFFERENT from the continuous answer, not just "close".
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 15), D(2026, 1, 15)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Month));

        _interop.GanttV3ScrollCenterXToReturn = 120d * 14.5; // mid-month-15 from the Month-mode origin

        var dayToggle = cut.FindAll("button").First(b => b.TextContent.Trim() == "Day");
        dayToggle.Click();

        // Day mode recenters its own VisibleRange around the preserved
        // center date, padded +/-60 days (Day mode's own PadBefore/PadAfter) -
        // a continuous 2026-03-16 center produces "Jan 15, 2026" as the
        // window's own start, while the OLD snapped 2026-04-01 center would
        // instead produce "Feb 1, 2026" (a different, checkable date).
        Assert.Contains("Jan 15, 2026", cut.Markup);
        Assert.DoesNotContain("Feb 1, 2026", cut.Markup);
    }

    // -- GanttTimeline: arrow-culling window invalidates on rows-count change (P1 #2) --

    [Fact]
    public void Arrow_Culling_Window_Recomputes_When_An_Empty_Task_List_Becomes_Populated_With_No_Scroll()
    {
        // Bug fix (Codex round 6, P1 #2): the culling window used to ONLY
        // recompute in response to a native 'scroll' event - an empty-to-
        // populated task-list transition never fires one at all, so the
        // window stayed at whatever it was for the EMPTY list (rowCount=0,
        // clamped to [0,0]), culling every arrow in the newly-populated list
        // even though nothing about the scroll position itself changed.
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Array.Empty<L.GanttTask>())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 3, 1)));

        // Registration's own initial report (scrollTop=0, some clientHeight)
        // already ran at mount - bUnit's fake JS runtime resolves it
        // synchronously (TrackingInteropService), so OnGanttV3VerticalScroll
        // has already cached SOME last-reported scrollTop/clientHeight by
        // the time this render completes, exactly like a real browser would
        // report before any task data arrives.

        var tasks = new List<L.GanttTask>();
        for (var i = 0; i < 5; i++)
        {
            var dependsOn = i > 0 ? new[] { $"t{i - 1}" } : null;
            tasks.Add(new L.GanttTask($"t{i}", $"Task {i}", D(2026, 1, 1).AddDays(i), D(2026, 1, 1).AddDays(i + 1), Dependencies: dependsOn));
        }
        var rows = GanttRowModel.BuildVisibleRows(tasks, new HashSet<string>());

        cut.Render(p => p
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 3, 1)));

        // No new scroll report was simulated between the two renders - if
        // the culling window were still stuck at the empty list's [0,0]
        // range, every arrow connecting the 5 new rows would be culled.
        var arrows = cut.FindAll(".lumeo-gantt-v3-arrow");
        Assert.NotEmpty(arrows);
    }

    // -- GanttBar: Space-preventDefault reconciles every render, not just firstRender (cx6b, Important #2) --

    [Fact]
    public void GanttBar_Registers_PreventDefaultKeys_When_OnTaskClick_Is_Attached_After_The_First_Render()
    {
        // Bug fix (Codex round 6 review / cx6b, Important #2): the original
        // fix only registered on `firstRender && OnTaskClick.HasDelegate` -
        // role/onkeydown/onclick in InnerAttributes re-evaluate HasDelegate
        // on EVERY render, so a consumer attaching OnTaskClick AFTER mount
        // got the live keyboard/click wiring without the matching
        // preventDefault suppression ever registering.
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 9));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)); // no OnTaskClick at mount

        Assert.Empty(_interop.RegisterPreventDefaultKeysElementIds);

        cut.Render(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.OnTaskClick, (L.GanttTask _) => { })); // attached AFTER the first render

        Assert.Single(_interop.RegisterPreventDefaultKeysElementIds);
    }

    [Fact]
    public void GanttBar_Unregisters_PreventDefaultKeys_When_OnTaskClick_Is_Detached()
    {
        var task = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 9));
        var cut = _ctx.Render<L.GanttBar>(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.OnTaskClick, (L.GanttTask _) => { }));

        Assert.Single(_interop.RegisterPreventDefaultKeysElementIds);
        Assert.Empty(_interop.UnregisterPreventDefaultKeysElementIds);

        cut.Render(p => p
            .Add(c => c.Task, task)
            .Add(c => c.X, 0d)
            .Add(c => c.Width, 114d)
            .Add(c => c.OnTaskClick, default(Microsoft.AspNetCore.Components.EventCallback<L.GanttTask>))); // detached

        Assert.Single(_interop.UnregisterPreventDefaultKeysElementIds);
    }
}
