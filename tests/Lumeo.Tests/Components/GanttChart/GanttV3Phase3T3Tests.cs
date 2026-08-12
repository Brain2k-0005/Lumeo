using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 3, T3 — summary rollups: envelope strips + overridable math
/// (see <see cref="GanttRollupModelTests"/> for the exhaustive pure-math
/// coverage — this file is the COMPONENT-level integration layer: does
/// <see cref="L.GanttTimeline"/>/<see cref="L.GanttChart"/> actually wire
/// <c>ShowSummaryBars</c>/<c>RollupMath</c>/<c>SummaryTemplate</c> into
/// rendered markup, and does a drag/progress commit recompute the strip).
/// </summary>
public class GanttV3Phase3T3Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3Phase3T3Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private const string StripSelector = ".lumeo-gantt-v3-summary-bar";
    private const string LabelSelector = ".lumeo-gantt-v3-summary-label";

    private static List<L.GanttTask> HierarchyFixture() => new()
    {
        new("p1", "Parent", D(2026, 1, 1), D(2026, 1, 10)),
        new("c1", "Child A", D(2026, 1, 1), D(2026, 1, 5), Progress: 0) { ParentId = "p1" }, // 4 days
        new("c2", "Child B", D(2026, 1, 5), D(2026, 1, 10), Progress: 0) { ParentId = "p1" }, // 5 days
    };

    private static List<L.GanttTask> GroupFixture() => new()
    {
        new("t1", "A", D(2026, 1, 1), D(2026, 1, 5), Progress: 100, GroupLabel: "Design"),
        new("t2", "B", D(2026, 1, 5), D(2026, 1, 10), Progress: 0, GroupLabel: "Design"),
    };

    // ── T3 decision #1: default is OFF ──────────────────────────────────────

    [Fact]
    public void ShowSummaryBars_Defaults_To_False_Even_With_A_Hierarchy_Present()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p.Add(c => c.Tasks, HierarchyFixture()));

        Assert.Empty(cut.FindAll(StripSelector));
    }

    // ── Rendering: strip + % label on each eligible row kind ────────────────

    [Fact]
    public void ShowSummaryBars_True_Renders_A_Strip_With_The_Correct_Percent_Label_On_A_Hierarchy_Parent_Row()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowSummaryBars, true));

        var strips = cut.FindAll(StripSelector);
        Assert.Single(strips);
        // (4*0 + 5*0) / 9 = 0% — both children start at 0 progress.
        Assert.Equal("0%", cut.Find(LabelSelector).TextContent);
    }

    [Fact]
    public void ShowSummaryBars_True_Renders_A_Strip_On_A_GroupHeader_Row()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, GroupFixture())
            .Add(c => c.GroupBy, (L.GanttTask t) => t.GroupLabel ?? "")
            .Add(c => c.ShowSummaryBars, true));

        var strips = cut.FindAll(StripSelector);
        Assert.Single(strips);
        // t1: 4 days @ 100%, t2: 5 days @ 0% -> (4*100+5*0)/9 = 400/9 ≈ 44.4% -> rounds to 44%.
        Assert.Equal("44%", cut.Find(LabelSelector).TextContent);
    }

    [Fact]
    public void ShowSummaryBars_True_Renders_No_Strip_On_A_Leaf_Row()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowSummaryBars, true));

        // Exactly one strip total (the parent's) — neither leaf child renders one.
        Assert.Single(cut.FindAll(StripSelector));
    }

    [Fact]
    public void ShowSummaryBars_False_Renders_No_Strip_Even_Though_A_Parent_Row_Exists()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowSummaryBars, false));

        Assert.Empty(cut.FindAll(StripSelector));
    }

    // ── SummaryTemplate override ─────────────────────────────────────────────

    [Fact]
    public void SummaryTemplate_Replaces_The_Default_Strip_Content()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowSummaryBars, true)
            .Add(c => c.SummaryTemplate, (RenderFragment<GanttRollup>)(r => b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "custom-summary-marker");
                b.AddContent(2, $"custom:{r.WeightedProgress}");
                b.CloseElement();
            })));

        Assert.Contains("custom-summary-marker", cut.Markup);
        Assert.Contains("custom:0", cut.Markup);
        // The default bg/fill/label pieces never render when the template is set.
        Assert.Empty(cut.FindAll(LabelSelector));
    }

    // ── RollupMath override wiring ───────────────────────────────────────────

    [Fact]
    public void RollupMath_Override_Is_Threaded_Through_GanttChart_Into_The_Rendered_Label()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowSummaryBars, true)
            .Add(c => c.RollupMath, (Func<IReadOnlyList<L.GanttTask>, GanttRollup>)(children =>
                new GanttRollup(D(2026, 1, 1), D(2026, 1, 10), 91))));

        Assert.Equal("91%", cut.Find(LabelSelector).TextContent);
    }

    // ── Recompute after a commit (design spec Phase 3, T3: "Rollup recomputes
    //    on task edits (drag commit updates parent strips — bUnit asserts
    //    post-commit rollup)") ─────────────────────────────────────────────

    [Fact]
    public async Task Progress_Commit_On_A_Child_Task_Recomputes_The_Parents_Summary_Percent_Label()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowSummaryBars, true));

        Assert.Equal("0%", cut.Find(LabelSelector).TextContent);

        var timeline = cut.FindComponent<L.GanttTimeline>();
        // c1 (4-day weight) jumps to 100%; c2 (5-day weight) stays at 0%.
        // (4*100 + 5*0) / 9 = 400/9 ≈ 44.44% -> rounds to 44%.
        await cut.InvokeAsync(() => timeline.Instance.CommitProgress("c1", 100));

        Assert.Equal("44%", cut.Find(LabelSelector).TextContent);
    }

    [Fact]
    public async Task Date_Commit_On_A_Child_Task_Widens_The_Parents_Envelope()
    {
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, HierarchyFixture())
            .Add(c => c.ShowSummaryBars, true));

        var timeline = cut.FindComponent<L.GanttTimeline>();
        // Move c2's end from Jan 10 out to Jan 20 — the parent's envelope
        // (rooted in its children's Start/End, not the parent's own dates)
        // must grow to match.
        await cut.InvokeAsync(() => timeline.Instance.CommitDrag("c2", "resize-end", "2026-01-05", "2026-01-20"));

        var style = cut.Find(StripSelector).GetAttribute("style") ?? "";
        var widthMatch = System.Text.RegularExpressions.Regex.Match(style, @"width:(\d+(?:\.\d+)?)px");
        Assert.True(widthMatch.Success);
        var strippedWidth = double.Parse(widthMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

        // Before the edit the envelope spanned Jan 1 -> Jan 10 inclusive (the
        // same end.AddDays(1) inclusive-end convention GanttScale.BarGeometry
        // uses for a real bar: 10 calendar days -> 10 columns wide); after the
        // edit it spans Jan 1 -> Jan 20 inclusive (20 columns) — strictly
        // wider, not merely re-rendered at the same geometry (which a
        // stale-cache bug would still pass a "some strip exists" assertion for).
        var colW = GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth;
        Assert.Equal(20 * colW, strippedWidth, 5);
    }

    // ── Recursion (T3 decision #4) at the component level ────────────────────

    [Fact]
    public void A_Grandparent_Row_Renders_Its_Own_Transitively_Rolled_Up_Strip()
    {
        var tasks = new List<L.GanttTask>
        {
            new("gp", "Grandparent", D(2026, 1, 1), D(2026, 2, 1)),
            new("p", "Parent", D(2026, 1, 1), D(2026, 1, 20)) { ParentId = "gp" },
            new("leaf", "Leaf", D(2026, 1, 1), D(2026, 1, 11), Progress: 100) { ParentId = "p" }, // 10 days @ 100%
        };
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ShowSummaryBars, true));

        // Two summary strips: "gp" (rolled up transitively through "p") and "p"
        // itself (rolled up from its one real leaf).
        Assert.Equal(2, cut.FindAll(StripSelector).Count);
        var labels = cut.FindAll(LabelSelector).Select(e => e.TextContent).OrderBy(x => x).ToList();
        // Both "gp" and "p" ultimately reduce to the SAME single 100%-progress
        // leaf, so both strips read 100% — proving "gp" didn't fall back to 0%
        // (its own raw, never-independently-set Progress) or ignore the leaf
        // two levels down.
        Assert.Equal(new[] { "100%", "100%" }, labels);
    }
}
