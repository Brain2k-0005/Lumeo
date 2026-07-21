namespace Lumeo.Tests.ServerHost.E2E;

/// <summary>
/// Deterministic task fixtures shared by the v2/v3 Gantt parity harness
/// (feat/gantt-v3, T4 — see docs/superpowers/gantt-v3-t4-report.md). Rendered
/// by BOTH <c>/e2e/gantt-v2</c> (Lumeo's <c>Gantt</c>) and <c>/e2e/gantt-v3</c>
/// (the working-name <c>Gantt3</c>) so the Lumeo.Tests.E2E Playwright suite can
/// assert render-equivalence against the SAME data.
///
/// Dates are hardcoded (March 2026) — NEVER <see cref="DateTime.Now"/>/
/// <see cref="DateTime.Today"/> here, so every pixel/label assertion in the
/// parity suite is reproducible across runs and CI/local clocks. The one
/// exception is <see cref="TodayMarkerTasks"/>, which deliberately anchors on
/// <see cref="DateTime.Today"/> because the today-marker itself only renders
/// when "today" falls inside the rendered date window.
/// </summary>
internal static class GanttParityFixtures
{
    /// <summary>
    /// 12 tasks (incl. 1 milestone) across 2 flat groups ("Frontend" sorts
    /// before "Ops" alphabetically — <c>Gantt</c>/<c>Gantt3</c> both sort
    /// tasks by <c>GroupBy</c> output before rendering, so the group names are
    /// chosen to keep this list's declared order == the rendered row order).
    /// Exactly 3 dependency edges are set, covering:
    ///   - fe2 -&gt; fe1: forward, adjacent row (fe1 is the row directly above fe2).
    ///   - fe5 -&gt; fe1: forward, DISTANT row (5 rows apart) — a literal "same
    ///     row" dependency is impossible in this row-flattening model (two
    ///     different tasks can never share a RowIndex), so this substitutes
    ///     a non-adjacent forward edge for that slot; see the T4 report.
    ///   - be2 -&gt; be1: backward/target-left (be2 STARTS before be1 ENDS, so
    ///     be2's left edge sits left of be1's midX — the classic "loop back"
    ///     case GanttArrowRouting's own remarks describe).
    /// Progress values include 0, 50, and 100. Two tasks carry custom colors
    /// (fe3, be1). The overall range (2026-02-23 .. 2026-04-03) crosses two
    /// month boundaries (Feb-&gt;Mar, Mar-&gt;Apr).
    /// </summary>
    internal static List<GanttTask> SharedTasks() => new()
    {
        // ── Frontend group (rows 1..6 once the group-header row is counted) ──
        new("fe1", "UI Design", new DateTime(2026, 2, 23), new DateTime(2026, 3, 1),
            Progress: 100, GroupLabel: "Frontend"),
        new("fe2", "Components", new DateTime(2026, 3, 1), new DateTime(2026, 3, 8),
            Progress: 50, Dependencies: new[] { "fe1" }, GroupLabel: "Frontend"),
        new("fe-ms", "Design Sign-off", new DateTime(2026, 3, 8), new DateTime(2026, 3, 8),
            IsMilestone: true, GroupLabel: "Frontend"),
        new("fe3", "Integration", new DateTime(2026, 3, 8), new DateTime(2026, 3, 15),
            Progress: 0, GroupLabel: "Frontend"),
        new("fe4", "Hardening", new DateTime(2026, 3, 15), new DateTime(2026, 3, 22),
            Progress: 10, GroupLabel: "Frontend"),
        new("fe5", "Launch Prep", new DateTime(2026, 3, 22), new DateTime(2026, 3, 29),
            Progress: 0, Dependencies: new[] { "fe1" }, GroupLabel: "Frontend"),

        // ── Ops group (rows 8..13) ───────────────────────────────────────────
        new("be1", "API Design", new DateTime(2026, 3, 1), new DateTime(2026, 3, 10),
            Progress: 100, GroupLabel: "Ops"),
        new("be2", "REST Impl", new DateTime(2026, 3, 5), new DateTime(2026, 3, 18),
            Progress: 30, Dependencies: new[] { "be1" }, GroupLabel: "Ops"),
        new("be3", "Deploy Pipeline", new DateTime(2026, 3, 18), new DateTime(2026, 3, 25),
            Progress: 0, GroupLabel: "Ops"),
        new("be4", "Monitoring Setup", new DateTime(2026, 3, 25), new DateTime(2026, 3, 30),
            Progress: 0, GroupLabel: "Ops"),
        new("be5", "Docs", new DateTime(2026, 3, 10), new DateTime(2026, 3, 20),
            Progress: 60, GroupLabel: "Ops"),
        new("be6", "Support Handoff", new DateTime(2026, 3, 28), new DateTime(2026, 4, 3),
            Progress: 0, GroupLabel: "Ops"),
    };

    /// <summary>Per-task bar colour override for <see cref="SharedTasks"/> — mirrors what a
    /// consumer's <c>BarColor</c> delegate looks like; identical contract for v2's
    /// <c>Gantt.BarColor</c> and v3's <c>Gantt3.BarColor</c>.</summary>
    internal static string? GetBarColor(GanttTask t) => t.Id switch
    {
        "fe3" => "#f59e0b",
        "be1" => "#22c55e",
        _ => null,
    };

    /// <summary>
    /// v3-only: 5 tasks forming a 3-level <see cref="GanttTask.ParentId"/> hierarchy —
    /// v2 has no <c>ParentId</c> concept at all, so this fixture is never rendered
    /// through the v2 route. Used by <c>/e2e/gantt-v3-tree</c>.
    /// </summary>
    internal static List<GanttTask> TreeTasks() => new()
    {
        new("root1", "Program Kickoff", new DateTime(2026, 3, 1), new DateTime(2026, 3, 30)),
        new("child1", "Design Phase", new DateTime(2026, 3, 1), new DateTime(2026, 3, 10))
        {
            ParentId = "root1",
        },
        new("grandchild1", "Wireframes", new DateTime(2026, 3, 1), new DateTime(2026, 3, 5))
        {
            ParentId = "child1",
        },
        new("child2", "Build Phase", new DateTime(2026, 3, 10), new DateTime(2026, 3, 20), Dependencies: new[] { "child1" })
        {
            ParentId = "root1",
        },
        new("root2", "Independent Task", new DateTime(2026, 3, 5), new DateTime(2026, 3, 12)),
    };

    /// <summary>
    /// A dedicated, small fixture anchored on <see cref="DateTime.Today"/> — the ONLY
    /// place in this file that reads the clock, and only because the today-marker
    /// itself only renders when "today" falls inside the (deterministic, ±60-day-padded
    /// Day-mode) rendered window. Two tasks, one depending on the other, straddling today.
    /// </summary>
    internal static List<GanttTask> TodayMarkerTasks()
    {
        var today = DateTime.Today;
        return new List<GanttTask>
        {
            new("today-1", "In Progress Now", today.AddDays(-10), today.AddDays(5), Progress: 40),
            new("today-2", "Up Next", today.AddDays(5), today.AddDays(15), Dependencies: new[] { "today-1" }),
        };
    }
}
