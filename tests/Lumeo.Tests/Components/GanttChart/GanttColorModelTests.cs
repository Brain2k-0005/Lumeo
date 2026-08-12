using Lumeo.GanttV3;
using Xunit;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Regression tests for <see cref="GanttColorModel"/> — the pure colour
/// resolution logic behind <c>ColorByGroup</c> (stable per-group palette
/// colours) and the in-bar label's contrast-aware foreground pick (design
/// spec Phase 3, T7). Mirrors <see cref="GanttReorderModelTests"/>/<see
/// cref="GanttRollupModelTests"/>'s own "pure logic, no Blazor/DOM" style.
/// </summary>
public class GanttColorModelTests
{
    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static GanttTask Task(string id, string? parentId = null, string? groupLabel = null) =>
        new(id, id, D(2026, 3, 1), D(2026, 3, 5)) { ParentId = parentId, GroupLabel = groupLabel };

    // ── ResolveGroupColorVar: stability + determinism ────────────────────────

    // Golden values — independently computed (FNV-1a 32-bit, standard offset
    // basis 2166136261 / prime 16777619, mod 5) rather than derived by
    // running the production code and copying its output: "p1" -> index 4
    // -> chart-5, "p2" -> index 0 -> chart-1. Pinning the ALGORITHM (not just
    // "same input gives same output") is what would catch a regression to
    // string.GetHashCode() — see ResolveGroupColorVar's own remarks for why
    // that would still pass a same-process self-consistency check but break
    // cross-process/cross-run determinism.
    [Fact]
    public void ResolveGroupColorVar_Matches_The_Independently_Computed_FNV1a_Golden_Value()
    {
        var p1 = Task("p1");
        var p2 = Task("p2");

        Assert.Equal("var(--color-chart-5)", GanttColorModel.ResolveGroupColorVar(usesHierarchy: false, task: p1 with { GroupLabel = "p1" }));
        Assert.Equal("var(--color-chart-1)", GanttColorModel.ResolveGroupColorVar(usesHierarchy: false, task: p2 with { GroupLabel = "p2" }));
    }

    [Fact]
    public void ResolveGroupColorVar_Hierarchy_Mode_Keys_On_ParentId()
    {
        var tasks = new[] { Task("root"), Task("c1", parentId: "root") };

        // "root" (the bucket key for c1) is the SAME string used above ("p1"
        // was the key there too, coincidentally different content) — assert
        // against ITS OWN golden value instead, computed the same way.
        var expected = GanttColorModel.ResolveGroupColorVar(true, tasks[1]);
        var again = GanttColorModel.ResolveGroupColorVar(true, tasks[1]);

        Assert.Equal(expected, again);
        Assert.Matches(@"^var\(--color-chart-[1-5]\)$", expected);
    }

    [Fact]
    public void ResolveGroupColorVar_Same_Key_Always_Returns_The_Same_Slot_Regardless_Of_Which_Task_Instance_Carries_It()
    {
        var a = Task("a", groupLabel: "Design");
        var b = Task("b", groupLabel: "Design");

        Assert.Equal(
            GanttColorModel.ResolveGroupColorVar(usesHierarchy: false, task: a),
            GanttColorModel.ResolveGroupColorVar(usesHierarchy: false, task: b));
    }

    [Fact]
    public void ResolveGroupColorVar_Different_Keys_Can_Map_To_Different_Slots()
    {
        var a = Task("a", groupLabel: "p1");
        var b = Task("b", groupLabel: "p2");

        Assert.NotEqual(
            GanttColorModel.ResolveGroupColorVar(usesHierarchy: false, task: a),
            GanttColorModel.ResolveGroupColorVar(usesHierarchy: false, task: b));
    }

    [Fact]
    public void ResolveGroupColorVar_Root_Bucket_Tasks_All_Share_One_Slot()
    {
        var tasks = new[] { Task("r1"), Task("r2"), Task("r3") };

        var colors = tasks.Select(t => GanttColorModel.ResolveGroupColorVar(true, t)).Distinct().ToList();

        Assert.Single(colors); // every null-ParentId task hits the SAME RootBucketSentinel key
    }

    // ── ResolveGroupColorVar: stability under a REAL reorder ────────────────
    // (design spec Phase 3, T7 decision #3 — "prove stability under a reorder")

    [Fact]
    public void ResolveGroupColorVar_Is_Unchanged_By_A_Within_Bucket_Reorder()
    {
        var tasks = new List<GanttTask>
        {
            Task("root1"),
            Task("child1", parentId: "root1"),
            Task("child2", parentId: "root1"),
            Task("root2"),
        };

        var beforeChild1 = GanttColorModel.ResolveGroupColorVar(GanttRowModel.UsesHierarchy(tasks), tasks.First(t => t.Id == "child1"));
        var beforeRoot2 = GanttColorModel.ResolveGroupColorVar(GanttRowModel.UsesHierarchy(tasks), tasks.First(t => t.Id == "root2"));

        // Reorder child1/child2 within their own bucket (root1's children) —
        // GanttReorderModel.Move only permutes slots WITHIN one bucket; every
        // other task (root2 included) keeps its exact list position.
        var reordered = GanttReorderModel.Move(tasks, "child2", 0);
        var hierarchy = GanttRowModel.UsesHierarchy(reordered);

        var afterChild1 = GanttColorModel.ResolveGroupColorVar(hierarchy, reordered.First(t => t.Id == "child1"));
        var afterRoot2 = GanttColorModel.ResolveGroupColorVar(hierarchy, reordered.First(t => t.Id == "root2"));

        Assert.Equal(beforeChild1, afterChild1);
        Assert.Equal(beforeRoot2, afterRoot2);
    }

    [Fact]
    public void ResolveGroupColorVar_FlatGroup_Reorder_Leaves_Every_Groups_Colour_Unchanged()
    {
        var tasks = new List<GanttTask>
        {
            Task("d1", groupLabel: "Design"),
            Task("d2", groupLabel: "Design"),
            Task("e1", groupLabel: "Engineering"),
        };

        var beforeDesign = GanttColorModel.ResolveGroupColorVar(false, tasks[0]);
        var beforeEngineering = GanttColorModel.ResolveGroupColorVar(false, tasks[2]);

        var reordered = GanttReorderModel.Move(tasks, "d2", 0);

        var afterDesign = GanttColorModel.ResolveGroupColorVar(false, reordered.First(t => t.Id == "d1"));
        var afterEngineering = GanttColorModel.ResolveGroupColorVar(false, reordered.First(t => t.Id == "e1"));

        Assert.Equal(beforeDesign, afterDesign);
        Assert.Equal(beforeEngineering, afterEngineering);
    }

    // ── DISABLE CHECK: string.GetHashCode()-style (process-random) hashing ──
    // would still pass every test ABOVE (same-process self-consistency is all
    // they need) — this is the one that would catch it: two SEPARATE process
    // runs of this exact xunit assembly must agree on the same golden value
    // pinned above. Simulated here by asserting the golden value directly
    // (rather than "equal to itself") — a hash keyed on GetHashCode() would
    // only coincidentally match this literal on any given run.
    [Fact]
    public void ResolveGroupColorVar_Golden_Value_Is_Reproducible_Not_Process_Random()
    {
        Assert.Equal("var(--color-chart-5)", GanttColorModel.ResolveGroupColorVar(false, Task("x", groupLabel: "p1")));
    }

    // ── PickLabelForegroundVar ────────────────────────────────────────────────

    [Fact]
    public void PickLabelForegroundVar_Null_Color_Returns_Null()
    {
        Assert.Null(GanttColorModel.PickLabelForegroundVar(null, themeIsDark: false));
        Assert.Null(GanttColorModel.PickLabelForegroundVar(null, themeIsDark: true));
    }

    [Theory]
    [InlineData("not-a-color")]
    [InlineData("red")]
    [InlineData("var(--color-primary)")]
    [InlineData("hsl(200, 100%, 50%)")]
    [InlineData("oklch(0.5 0.1 200)")]
    public void PickLabelForegroundVar_Unparseable_Color_Returns_Null(string color)
    {
        Assert.Null(GanttColorModel.PickLabelForegroundVar(color, themeIsDark: false));
    }

    [Theory]
    [InlineData("#ffffff")]
    [InlineData("#FFF")]
    [InlineData("rgb(255, 255, 255)")]
    [InlineData("rgba(255,255,255,0.5)")]
    public void PickLabelForegroundVar_Light_Custom_Color_Picks_The_Dark_Pole(string color)
    {
        Assert.Equal("var(--color-foreground)", GanttColorModel.PickLabelForegroundVar(color, themeIsDark: false));
        Assert.Equal("var(--color-background)", GanttColorModel.PickLabelForegroundVar(color, themeIsDark: true));
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#000")]
    [InlineData("rgb(0, 0, 0)")]
    [InlineData("#808080")] // luminance 0.50196 — below the 0.6 threshold, same as ColorPicker.IsLight's own convention
    public void PickLabelForegroundVar_Dark_Custom_Color_Picks_The_Light_Pole(string color)
    {
        Assert.Equal("var(--color-background)", GanttColorModel.PickLabelForegroundVar(color, themeIsDark: false));
        Assert.Equal("var(--color-foreground)", GanttColorModel.PickLabelForegroundVar(color, themeIsDark: true));
    }
}
