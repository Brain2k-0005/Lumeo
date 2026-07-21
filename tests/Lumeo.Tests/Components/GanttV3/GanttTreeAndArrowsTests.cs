using Bunit;
using Lumeo.GanttV3;
using Lumeo.Tests.Helpers;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// bUnit regression tests for the GanttV3 tree pane + dependency-arrow overlay
/// (design spec Phase 2, T3): <see cref="L.GanttTree"/>, <see cref="L.GanttArrowLayer"/>,
/// and the additions to <see cref="L.GanttTimeline"/>/<see cref="L.Gantt3"/> that
/// wire them together (Rows, ShowTreePane). No drag/JS-interop — everything
/// asserted is static markup produced from <see cref="GanttRowModel"/> +
/// <see cref="GanttArrowRouting"/> + plain parameters.
/// </summary>
public class GanttTreeAndArrowsTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public GanttTreeAndArrowsTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── GanttTree ────────────────────────────────────────────────────────────

    [Fact]
    public void GanttTree_Renders_Indent_Per_Depth_And_A_Summary_Row_Class_For_Group_Headers()
    {
        var rows = GanttRowModel.BuildVisibleRows(new[]
        {
            new L.GanttTask("t1", "Design", D(2026, 1, 1), D(2026, 1, 5)) { GroupLabel = "Phase 1" },
        }, new HashSet<string>());

        var cut = _ctx.Render<L.GanttTree>(p => p.Add(c => c.Rows, rows));

        var headerRow = cut.Find("[data-row-kind='group']");
        Assert.Contains("lumeo-gantt-v3-tree-summary-row", headerRow.GetAttribute("class"));
        Assert.Contains("Phase 1", cut.Markup);

        var taskRow = cut.Find("[data-row-kind='task']");
        var indent = taskRow.QuerySelector(".lumeo-gantt-v3-tree-indent");
        Assert.Contains("width:16px", indent!.GetAttribute("style")); // depth 1 * 16px
        Assert.Contains("Design", cut.Markup);
    }

    [Fact]
    public void GanttTree_Chevron_Toggle_Raises_OnToggleCollapse_With_The_Rows_ToggleKey()
    {
        var rows = GanttRowModel.BuildVisibleRows(new[]
        {
            new L.GanttTask("parent", "Parent", D(2026, 1, 1), D(2026, 1, 5)),
            new L.GanttTask("child", "Child", D(2026, 1, 1), D(2026, 1, 5)) { ParentId = "parent" },
        }, new HashSet<string>());

        string? received = null;
        var cut = _ctx.Render<L.GanttTree>(p => p
            .Add(c => c.Rows, rows)
            .Add(c => c.OnToggleCollapse, key => received = key));

        cut.Find(".lumeo-gantt-v3-tree-toggle").Click();

        Assert.Equal("parent", received);
    }

    [Fact]
    public void GanttTree_Leaf_Row_Has_No_Toggle_Button()
    {
        var rows = GanttRowModel.BuildVisibleRows(new[]
        {
            new L.GanttTask("t1", "Design", D(2026, 1, 1), D(2026, 1, 5)),
        }, new HashSet<string>());

        var cut = _ctx.Render<L.GanttTree>(p => p.Add(c => c.Rows, rows));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-tree-toggle"));
        Assert.Single(cut.FindAll(".lumeo-gantt-v3-tree-toggle-spacer"));
    }

    [Fact]
    public void GanttTree_Class_And_AdditionalAttributes_Splat_Onto_The_Root()
    {
        var cut = _ctx.Render<L.GanttTree>(p => p
            .Add(c => c.Rows, Array.Empty<GanttVisibleRow>())
            .Add(c => c.Class, "my-tree")
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "tree-1" }));

        Assert.Contains("my-tree", cut.Markup);
        Assert.Contains("data-testid=\"tree-1\"", cut.Markup);
    }

    // ── GanttTimeline: collapsing hides bars ────────────────────────────────

    [Fact]
    public void GanttTimeline_Collapsing_A_Parent_Hides_Its_Child_Bars()
    {
        var tasks = new[]
        {
            new L.GanttTask("parent", "Parent", D(2026, 1, 1), D(2026, 1, 5)),
            new L.GanttTask("child", "Child", D(2026, 1, 1), D(2026, 1, 5)) { ParentId = "parent" },
        };
        var expandedRows = GanttRowModel.BuildVisibleRows(tasks, new HashSet<string>());
        var collapsedRows = GanttRowModel.BuildVisibleRows(tasks, new HashSet<string> { "parent" });

        var expandedCut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Rows, expandedRows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));
        Assert.Equal(2, expandedCut.FindAll("[data-task-id]").Count);

        var collapsedCut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Rows, collapsedRows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 1, 1))
            .Add(c => c.RangeEnd, D(2026, 1, 10)));
        var bars = collapsedCut.FindAll("[data-task-id]");
        Assert.Single(bars);
        Assert.Equal("parent", bars[0].GetAttribute("data-task-id"));
    }

    // ── GanttArrowLayer ──────────────────────────────────────────────────────

    [Fact]
    public void GanttArrowLayer_Renders_One_Path_Per_Dependency_With_The_Expected_D_Attribute()
    {
        var rangeStart = D(2026, 1, 1);
        var upstream = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4));
        var downstream = new L.GanttTask("t2", "Build", D(2026, 1, 6), D(2026, 1, 8), Dependencies: new[] { "t1" });
        var rows = GanttRowModel.BuildVisibleRows(new[] { upstream, downstream }, new HashSet<string>());
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, rangeStart, D(2026, 1, 12))[0];

        var cut = _ctx.Render<L.GanttArrowLayer>(p => p
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Origin, origin)
            .Add(c => c.ColumnWidth, GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth)
            .Add(c => c.BarHeight, GanttScale.DefaultBarHeight)
            .Add(c => c.Width, 2000d)
            .Add(c => c.Height, 200d));

        var paths = cut.FindAll(".lumeo-gantt-v3-arrow");
        var path = Assert.Single(paths);
        Assert.Equal("t1", path.GetAttribute("data-arrow-from"));
        Assert.Equal("t2", path.GetAttribute("data-arrow-to"));

        var (upstreamX, upstreamWidth) = GanttScale.BarGeometry(upstream, L.GanttViewMode.Day, origin,
            GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth, GanttScale.DefaultBarHeight);
        var (downstreamX, _) = GanttScale.BarGeometry(downstream, L.GanttViewMode.Day, origin,
            GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth, GanttScale.DefaultBarHeight);
        var expectedPathD = GanttArrowRouting.ComputePathD(
            new GanttArrowRouting.BarGeometry(upstreamX, upstreamWidth, 0),
            new GanttArrowRouting.BarGeometry(downstreamX, 0, 1),
            GanttScale.DefaultBarHeight);
        Assert.Equal(expectedPathD, path.GetAttribute("d"));

        Assert.Single(cut.FindAll(".lumeo-gantt-v3-arrowhead"));
    }

    [Fact]
    public void GanttArrowLayer_Omits_An_Arrow_Whose_Task_Is_Hidden_By_Collapse()
    {
        var upstream = new L.GanttTask("t1", "Design", D(2026, 1, 2), D(2026, 1, 4)) { ParentId = "parent" };
        var parent = new L.GanttTask("parent", "Parent", D(2026, 1, 1), D(2026, 1, 10));
        var downstream = new L.GanttTask("t2", "Build", D(2026, 1, 6), D(2026, 1, 8), Dependencies: new[] { "t1" });
        var tasks = new[] { parent, upstream, downstream };

        // Collapse "parent" -> "t1" (its child) disappears from Rows entirely.
        var rows = GanttRowModel.BuildVisibleRows(tasks, new HashSet<string> { "parent" });
        var origin = GanttScale.BuildDateUnits(L.GanttViewMode.Day, D(2026, 1, 1), D(2026, 1, 12))[0];

        var cut = _ctx.Render<L.GanttArrowLayer>(p => p
            .Add(c => c.Rows, rows)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.Origin, origin)
            .Add(c => c.ColumnWidth, GanttScale.GetConfig(L.GanttViewMode.Day).ColumnWidth)
            .Add(c => c.BarHeight, GanttScale.DefaultBarHeight)
            .Add(c => c.Width, 2000d)
            .Add(c => c.Height, 200d));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-arrow"));
    }

    [Fact]
    public void GanttArrowLayer_Class_And_AdditionalAttributes_Splat_Onto_The_Root()
    {
        var cut = _ctx.Render<L.GanttArrowLayer>(p => p
            .Add(c => c.Rows, Array.Empty<GanttVisibleRow>())
            .Add(c => c.Width, 100d)
            .Add(c => c.Height, 50d)
            .Add(c => c.Class, "my-arrows")
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "arrows-1" }));

        Assert.Contains("my-arrows", cut.Markup);
        Assert.Contains("data-testid=\"arrows-1\"", cut.Markup);
    }

    // ── Gantt3: ShowTreePane wiring ──────────────────────────────────────────

    [Fact]
    public void Gantt3_Does_Not_Render_The_Tree_Pane_By_Default_For_A_Flat_Task_List()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, new[]
        {
            new L.GanttTask("t1", "Design", D(2026, 1, 1), D(2026, 1, 5)),
        }));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-tree-label"));
    }

    [Fact]
    public void Gantt3_Renders_The_Tree_Pane_By_Default_When_A_Task_Has_A_ParentId()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, new[]
        {
            new L.GanttTask("parent", "Parent", D(2026, 1, 1), D(2026, 1, 5)),
            new L.GanttTask("child", "Child", D(2026, 1, 1), D(2026, 1, 5)) { ParentId = "parent" },
        }));

        Assert.Equal(2, cut.FindAll(".lumeo-gantt-v3-tree-label").Count);
    }

    [Fact]
    public void Gantt3_ShowTreePane_False_Overrides_The_Hierarchy_Default()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new[]
            {
                new L.GanttTask("parent", "Parent", D(2026, 1, 1), D(2026, 1, 5)),
                new L.GanttTask("child", "Child", D(2026, 1, 1), D(2026, 1, 5)) { ParentId = "parent" },
            })
            .Add(c => c.ShowTreePane, false));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-tree-label"));
    }

    [Fact]
    public void Gantt3_ShowTreePane_True_Overrides_The_Flat_Default()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new[] { new L.GanttTask("t1", "Design", D(2026, 1, 1), D(2026, 1, 5)) })
            .Add(c => c.ShowTreePane, true));

        Assert.Single(cut.FindAll(".lumeo-gantt-v3-tree-label"));
    }
}
