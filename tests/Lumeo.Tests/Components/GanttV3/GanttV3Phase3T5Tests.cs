using System.Globalization;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 3, T5 — tree pane upgrades: multi-column, splitter,
/// RowTemplate. Covers all five plan deliverables (<c>GanttTreeColumn</c>,
/// <c>TreeColumns</c>, <c>TreeHeaderMenu</c>, <c>TreePaneWidth</c>,
/// <c>RowTemplate</c>) and the five explicitly-required decisions:
/// row alignment across both virtualized panes (drift-catching tests),
/// the splitter's reuse of the gantt-v3.js drag-engine conventions,
/// controlled-vs-uncontrolled <c>TreePaneWidth</c>, RowTemplate vs the pinned
/// name column's indent/expander chrome, and the min/max clamps.
/// </summary>
public class GanttV3Phase3T5Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3Phase3T5Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static List<L.GanttTask> Fixture() => new()
    {
        new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6), Progress: 40),
        new("t2", "Build", D(2026, 1, 10), D(2026, 1, 20), Progress: 10),
    };

    private static List<L.GanttTask> GroupFixture() => new()
    {
        new("t1", "A", D(2026, 1, 1), D(2026, 1, 5), Progress: 100, GroupLabel: "Design"),
        new("t2", "B", D(2026, 1, 5), D(2026, 1, 10), Progress: 0, GroupLabel: "Design"),
    };

    private static List<GanttTreeColumn> Columns() => new()
    {
        new GanttTreeColumn("Progress", 70, (RenderFragment<L.GanttTask>)(task => b =>
        {
            b.OpenElement(0, "span");
            b.AddAttribute(1, "class", "custom-progress-cell");
            b.AddContent(2, $"{task.Progress}%");
            b.CloseElement();
        })),
    };

    private static RenderFragment<L.GanttTask> TallCellTemplate => task => b =>
    {
        b.OpenElement(0, "div");
        b.AddAttribute(1, "style", "height:400px;");
        b.AddAttribute(2, "class", "tall-cell-content");
        b.AddContent(3, "tall");
        b.CloseElement();
    };

    private static List<GanttVisibleRow> RowsFor(params L.GanttTask[] tasks) =>
        tasks.Select(t => new GanttVisibleRow(GanttRowKind.Task, t, t.Name, 0, false, null, false)).ToList();

    // Fixture() is a flat 2-task list (no ParentId/GroupBy), so
    // GanttRowModel.DefaultShowTreePane would resolve to FALSE and GanttTree
    // would never render at all — every test in this file targets the tree
    // pane specifically, so ShowTreePane is forced on explicitly here rather
    // than relying on each fixture happening to carry a hierarchy.
    private IRenderedComponent<L.Gantt3> RenderTree(Action<Bunit.ComponentParameterCollectionBuilder<L.Gantt3>> configure) =>
        _ctx.Render<L.Gantt3>(p =>
        {
            p.Add(c => c.ShowTreePane, true);
            configure(p);
        });

    // ── TreeColumns render typed content ────────────────────────────────────

    [Fact]
    public void TreeColumns_Render_Typed_CellTemplate_Content_For_Task_Rows()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.TreeColumns, Columns()));

        var cells = cut.FindAll(".custom-progress-cell");
        Assert.Equal(2, cells.Count); // one per task row
        Assert.Contains("40%", cut.Markup);
        Assert.Contains("10%", cut.Markup);

        // Header row shows the column title.
        Assert.Equal("Progress", cut.Find(".lumeo-gantt-v3-tree-header-cell").TextContent);
    }

    [Fact]
    public void TreeColumns_Render_An_Empty_Aligned_Cell_For_GroupHeader_Rows()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, GroupFixture())
            .Add(c => c.GroupBy, (L.GanttTask t) => t.GroupLabel ?? "")
            .Add(c => c.TreeColumns, Columns()));

        // 1 GroupHeader row + 2 task rows = 3 `.lumeo-gantt-v3-tree-cell` slots,
        // but only the 2 task rows get CellTemplate content — the GroupHeader's
        // own cell is present (alignment) yet empty (no Task to feed the template).
        var cells = cut.FindAll(".lumeo-gantt-v3-tree-cell");
        Assert.Equal(3, cells.Count);
        Assert.Equal(2, cut.FindAll(".custom-progress-cell").Count);
    }

    [Fact]
    public void TreeHeaderMenu_Slot_Renders_When_Set()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.TreeHeaderMenu, (RenderFragment)(b =>
            {
                b.OpenElement(0, "button");
                b.AddAttribute(1, "class", "custom-columns-menu-trigger");
                b.AddContent(2, "...");
                b.CloseElement();
            })));

        Assert.Single(cut.FindAll(".custom-columns-menu-trigger"));
    }

    [Fact]
    public void TreeHeaderMenu_Renders_Nothing_Extra_When_Null()
    {
        var cut = RenderTree(p => p.Add(c => c.Tasks, Fixture()));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-tree-header-menu"));
    }

    // ── RowTemplate replaces ONLY the label — decision #4 ───────────────────

    [Fact]
    public void RowTemplate_Override_Wins_For_The_Label_But_Indent_And_Toggle_Chrome_Survive()
    {
        var tasks = new List<L.GanttTask>
        {
            new("p1", "Parent", D(2026, 1, 1), D(2026, 1, 10)),
            new("c1", "Child", D(2026, 1, 1), D(2026, 1, 5)) { ParentId = "p1" },
        };
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.RowTemplate, (RenderFragment<L.GanttTask>)(task => b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "custom-row-marker");
                b.AddContent(2, $"custom:{task.Name}");
                b.CloseElement();
            })));

        Assert.Contains("custom:Parent", cut.Markup);
        Assert.Contains("custom:Child", cut.Markup);
        // The default label span never renders when RowTemplate is set.
        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-tree-label"));
        // Chrome ALWAYS rendered by GanttTree itself, regardless of RowTemplate:
        Assert.Single(cut.FindAll(".lumeo-gantt-v3-tree-toggle")); // parent's expander
        Assert.Equal(2, cut.FindAll(".lumeo-gantt-v3-tree-indent").Count); // one per row
    }

    [Fact]
    public void RowTemplate_Falls_Back_To_The_Default_Label_For_A_GroupHeader_Row_With_No_Task()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, GroupFixture())
            .Add(c => c.GroupBy, (L.GanttTask t) => t.GroupLabel ?? "")
            .Add(c => c.RowTemplate, (RenderFragment<L.GanttTask>)(task => b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "custom-row-marker");
                b.AddContent(2, task.Name);
                b.CloseElement();
            })));

        // 2 real task rows get the custom marker; the GroupHeader row (no Task)
        // still renders its own label via the default path.
        Assert.Equal(2, cut.FindAll(".custom-row-marker").Count);
        Assert.Contains("Design", cut.FindAll(".lumeo-gantt-v3-tree-label")[0].TextContent);
    }

    // ── ShowTaskMeta (2026-08-10 shadcn alignment, G6) ──────────────────────

    [Fact]
    public void ShowTaskMeta_Renders_A_DateRange_And_Progress_Line_Under_The_Name()
    {
        // Pinned culture (en-US) — TaskMetaText formats via CurrentCulture (same
        // determinism concern GanttV3Phase3T2Tests' off-day tests already pin
        // for), so the "Mar 1"/"Mar 4" literals below are deterministic
        // regardless of the test runner's ambient culture.
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            var cut = RenderTree(p => p
                .Add(c => c.Tasks, new List<L.GanttTask>
                {
                    new("t1", "Design Phase", D(2026, 3, 1), D(2026, 3, 4), Progress: 40),
                })
                .Add(c => c.ShowTaskMeta, true));

            var meta = cut.Find(".lumeo-gantt-v3-tree-meta");
            Assert.Contains("Mar 1", meta.TextContent);
            Assert.Contains("Mar 4", meta.TextContent);
            Assert.Contains("40%", meta.TextContent);
            Assert.Contains("Design Phase", cut.Find(".lumeo-gantt-v3-tree-label").TextContent);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ShowTaskMeta_Default_False_Renders_No_Meta_Line()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, new List<L.GanttTask>
            {
                new("t1", "Design Phase", D(2026, 3, 1), D(2026, 3, 4), Progress: 40),
            }));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-tree-meta"));
    }

    [Fact]
    public void ShowTaskMeta_Never_Renders_For_A_GroupHeader_Row_With_No_Task()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, GroupFixture())
            .Add(c => c.GroupBy, (L.GanttTask t) => t.GroupLabel ?? "")
            .Add(c => c.ShowTaskMeta, true));

        // 2 real task rows get a meta line; the GroupHeader row (no Task) has
        // no Start/End/Progress to show and falls back to the plain label.
        Assert.Equal(2, cut.FindAll(".lumeo-gantt-v3-tree-meta").Count);
    }

    [Fact]
    public void ShowTaskMeta_Is_Ignored_When_RowTemplate_Is_Set()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, new List<L.GanttTask>
            {
                new("t1", "Design Phase", D(2026, 3, 1), D(2026, 3, 4), Progress: 40),
            })
            .Add(c => c.ShowTaskMeta, true)
            .Add(c => c.RowTemplate, (RenderFragment<L.GanttTask>)(task => b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "custom-row-marker");
                b.AddContent(2, task.Name);
                b.CloseElement();
            })));

        Assert.Empty(cut.FindAll(".lumeo-gantt-v3-tree-meta"));
        Assert.Single(cut.FindAll(".custom-row-marker"));
    }

    // ── Row-alignment drift guards (decision #1) ────────────────────────────
    //
    // bUnit performs no real layout, so these assert the STRUCTURAL guarantee
    // (a hard-pinned inline `height` + `overflow-hidden`) that alignment
    // depends on, rather than a measured pixel position — see the report for
    // the predicted-vs-actual disable-check proof that this genuinely catches
    // the "a tall column title/CellTemplate grows the row past its own slot"
    // class of drift, not just "it renders".

    [Fact]
    public void Header_Row_Stays_Pinned_To_HeaderHeight_Even_With_A_Tall_Column_Title_And_Menu()
    {
        var longTitle = new string('X', 500); // pathological — no length contract on Title
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.TreeColumns, new List<GanttTreeColumn>
            {
                new(longTitle, 70, (RenderFragment<L.GanttTask>)(t => b => b.AddContent(0, t.Name))),
            })
            .Add(c => c.TreeHeaderMenu, (RenderFragment)(b =>
            {
                b.OpenElement(0, "div");
                b.AddAttribute(1, "style", "height:400px;");
                b.AddContent(2, "tall menu content");
                b.CloseElement();
            })));

        var header = cut.Find(".lumeo-gantt-v3-tree-header-name-cell").ParentElement!;
        Assert.Contains("height:56px", header.GetAttribute("style"));
        Assert.Contains("overflow-hidden", header.ClassList);
    }

    [Fact]
    public void Tree_Row_Stays_Pinned_To_RowHeight_Even_With_Tall_CellTemplate_And_RowTemplate_Content()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.TreeColumns, new List<GanttTreeColumn> { new("Tall", 100, TallCellTemplate) })
            .Add(c => c.RowTemplate, TallCellTemplate));

        var rows = cut.FindAll("[data-row-kind='task']");
        Assert.Equal(2, rows.Count);
        foreach (var row in rows)
        {
            Assert.Contains("height:36px", row.GetAttribute("style"));
        }
        // The tall content genuinely rendered (proves this isn't vacuously
        // passing because the template never ran).
        Assert.NotEmpty(cut.FindAll(".tall-cell-content"));
    }

    // ── Multi-column + name-column total width ──────────────────────────────

    [Fact]
    public void Root_Pane_Width_Is_The_Name_Column_Plus_All_TreeColumns()
    {
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.TreeColumns, new List<GanttTreeColumn>
            {
                new("A", 70, (RenderFragment<L.GanttTask>)(t => b => { })),
                new("B", 50, (RenderFragment<L.GanttTask>)(t => b => { })),
            }));

        var pane = cut.Find(".lumeo-gantt-v3-tree-splitter").ParentElement!;
        // Default TreePaneWidth (224) + 70 + 50 = 344.
        Assert.Contains("width:344px", pane.GetAttribute("style"));
    }

    // ── Min/max clamps (decision #5) ─────────────────────────────────────────

    [Fact]
    public void A_Controlled_TreePaneWidth_Parameter_Value_Outside_The_Bounds_Is_Also_Clamped_On_Render()
    {
        // Independent of the gesture-time clamps (GanttTree.CommitSplitterWidth's
        // own JSInvokable-boundary clamp): a controlled caller can hand Gantt3 an
        // out-of-range TreePaneWidth PARAMETER directly, with no drag involved at
        // all — Gantt3.EffectiveTreePaneWidth clamps that too, so the rendered
        // pane never exceeds the bounds regardless of how the value arrived.
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.TreePaneWidth, 9999.0));

        var pane = cut.Find(".lumeo-gantt-v3-tree-splitter").ParentElement!;
        Assert.Contains($"--lumeo-gantt-tree-name-width:{GanttScale.MaxTreePaneWidth}px", pane.GetAttribute("style"));
    }

    [Fact]
    public void MinMaxTreePaneWidth_Bracket_The_Default_And_Are_Sane()
    {
        Assert.True(GanttScale.MinTreePaneWidth < GanttScale.DefaultTreePaneWidth);
        Assert.True(GanttScale.DefaultTreePaneWidth < GanttScale.MaxTreePaneWidth);
        Assert.True(GanttScale.MinTreePaneWidth > 0);
    }

    [Fact]
    public async Task Splitter_Commit_Below_Min_Clamps_To_MinTreePaneWidth_Uncontrolled()
    {
        var cut = RenderTree(p => p.Add(c => c.Tasks, Fixture()));

        await cut.InvokeAsync(() => _interop.SimulateGanttV3SplitterCommit(1.0));

        var pane = cut.Find(".lumeo-gantt-v3-tree-splitter").ParentElement!;
        Assert.Contains($"--lumeo-gantt-tree-name-width:{GanttScale.MinTreePaneWidth}px", pane.GetAttribute("style"));
    }

    [Fact]
    public async Task Splitter_Commit_Above_Max_Clamps_To_MaxTreePaneWidth_Uncontrolled()
    {
        var cut = RenderTree(p => p.Add(c => c.Tasks, Fixture()));

        await cut.InvokeAsync(() => _interop.SimulateGanttV3SplitterCommit(999_999.0));

        var pane = cut.Find(".lumeo-gantt-v3-tree-splitter").ParentElement!;
        Assert.Contains($"--lumeo-gantt-tree-name-width:{GanttScale.MaxTreePaneWidth}px", pane.GetAttribute("style"));
    }

    // GanttTree.CommitSplitterWidth's OWN clamp, isolated from Gantt3 entirely
    // (rendering GanttTree standalone, asserting on its OWN OnResize callback
    // argument) — the two tests above only prove the COMBINED (GanttTree +
    // Gantt3) pipeline clamps; a first attempt at disable-checking THIS layer
    // specifically found that Gantt3.HandleTreePaneResizeAsync's own redundant
    // clamp silently rescues a disabled GanttTree clamp, so those two tests
    // alone are not evidence this specific layer does anything (a "disable-
    // check passes" blind spot — rebuilt as the two tests below, which target
    // ONLY GanttTree with no Gantt3 in the render tree to rescue a broken clamp).
    [Fact]
    public async Task GanttTree_CommitSplitterWidth_Clamps_Below_Min_In_Isolation_From_Gantt3()
    {
        double? resized = null;
        var cut = _ctx.Render<L.GanttTree>(p => p
            .Add(c => c.Rows, RowsFor(Fixture().ToArray()))
            .Add(c => c.OnResize, (double w) => resized = w));

        await cut.InvokeAsync(() => cut.Instance.CommitSplitterWidth(1.0));

        Assert.Equal((double)GanttScale.MinTreePaneWidth, resized);
    }

    [Fact]
    public async Task GanttTree_CommitSplitterWidth_Clamps_Above_Max_In_Isolation_From_Gantt3()
    {
        double? resized = null;
        var cut = _ctx.Render<L.GanttTree>(p => p
            .Add(c => c.Rows, RowsFor(Fixture().ToArray()))
            .Add(c => c.OnResize, (double w) => resized = w));

        await cut.InvokeAsync(() => cut.Instance.CommitSplitterWidth(999_999.0));

        Assert.Equal((double)GanttScale.MaxTreePaneWidth, resized);
    }

    // ── Controlled vs uncontrolled TreePaneWidth (decision #3) ──────────────

    [Fact]
    public async Task A_TreePaneWidthChanged_Listener_With_No_Bound_TreePaneWidth_Still_Behaves_Uncontrolled()
    {
        // Bug found during T5's own disable-check pass (see Gantt3.HandleTreePaneResizeAsync's
        // own remarks): controlled-ness must be decided by TreePaneWidth's own
        // nullability, NOT by whether TreePaneWidthChanged has a delegate — a
        // caller that wires the callback purely to OBSERVE resizes, without
        // ever supplying TreePaneWidth, must still see the pane actually
        // resize (and keep resizing on later renders), not freeze forever.
        double? notified = null;
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.TreePaneWidthChanged, (double w) => notified = w));

        await cut.InvokeAsync(() => _interop.SimulateGanttV3SplitterCommit(350.0));

        Assert.Equal(350.0, notified);
        var pane = cut.Find(".lumeo-gantt-v3-tree-splitter").ParentElement!;
        Assert.Contains("--lumeo-gantt-tree-name-width:350px", pane.GetAttribute("style"));
    }

    [Fact]
    public async Task Uncontrolled_Splitter_Width_Persists_Across_An_Unrelated_Rerender()
    {
        var cut = RenderTree(p => p.Add(c => c.Tasks, Fixture()));

        await cut.InvokeAsync(() => _interop.SimulateGanttV3SplitterCommit(300.0));
        var pane = cut.Find(".lumeo-gantt-v3-tree-splitter").ParentElement!;
        Assert.Contains("--lumeo-gantt-tree-name-width:300px", pane.GetAttribute("style"));

        // An UNRELATED re-render (a new Tasks list, same shape) must not revert
        // the resize — "persists during session" (design spec Phase 3, T5).
        cut.Render(p => p.Add(c => c.Tasks, Fixture()));

        pane = cut.Find(".lumeo-gantt-v3-tree-splitter").ParentElement!;
        Assert.Contains("--lumeo-gantt-tree-name-width:300px", pane.GetAttribute("style"));
    }

    [Fact]
    public async Task Controlled_Splitter_Commit_Raises_TreePaneWidthChanged_With_The_Clamped_Value()
    {
        double? notified = null;
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.TreePaneWidth, 224.0)
            .Add(c => c.TreePaneWidthChanged, (double w) => notified = w));

        await cut.InvokeAsync(() => _interop.SimulateGanttV3SplitterCommit(999_999.0));

        Assert.Equal(GanttScale.MaxTreePaneWidth, notified);
    }

    [Fact]
    public async Task Controlled_Splitter_Commit_The_Parent_Ignores_Reverts_On_The_Next_Render()
    {
        // "The drag must not fight the parameter" (design spec Phase 3, T5,
        // decision #3): a controlled parent that does NOT update its own bound
        // value in response to TreePaneWidthChanged is a veto — the SAME
        // contract Tasks/TasksChanged and ViewMode/ViewModeChanged already have.
        var cut = RenderTree(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.TreePaneWidth, 224.0)
            .Add(c => c.TreePaneWidthChanged, (double _) => { /* parent ignores it */ }));

        await cut.InvokeAsync(() => _interop.SimulateGanttV3SplitterCommit(400.0));
        // Force a fresh render pass with the parameter STILL at 224 (the parent
        // never adopted the pick) — mirrors how a real Blazor re-render would
        // reassert the unchanged parameter value. NOTE: this is a genuinely NEW
        // parameter pass (Render re-issues SetParametersAsync), not the
        // known-trap parameterless Render() that can self-heal state bugs into
        // false passes (see GanttV3CodexRound20Tests.cs's own remarks) — it
        // supplies TreePaneWidth explicitly, on every call.
        cut.Render(p => p.Add(c => c.TreePaneWidth, 224.0));

        var pane = cut.Find(".lumeo-gantt-v3-tree-splitter").ParentElement!;
        Assert.Contains("--lumeo-gantt-tree-name-width:224px", pane.GetAttribute("style"));
    }

    // ── Splitter drag-engine registration (decision #2) ─────────────────────

    [Fact]
    public void Splitter_Registers_With_The_JS_Drag_Engine_On_Mount_With_The_Right_Clamps()
    {
        var cut = _ctx.Render<L.GanttTree>(p => p.Add(c => c.Rows, RowsFor(Fixture().ToArray())));

        Assert.Equal(1, _interop.GanttV3RegisterSplitterDragCallCount);
        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastGanttV3SplitterDragOptions);
        Assert.Equal((double)GanttScale.DefaultTreePaneWidth, options["width"]);
        Assert.Equal((double)GanttScale.MinTreePaneWidth, options["minWidth"]);
        Assert.Equal((double)GanttScale.MaxTreePaneWidth, options["maxWidth"]);
    }

    [Fact]
    public async Task Splitter_Unregisters_On_Dispose()
    {
        var cut = _ctx.Render<L.GanttTree>(p => p.Add(c => c.Rows, RowsFor(Fixture().ToArray())));
        Assert.Equal(0, _interop.GanttV3UnregisterSplitterDragCallCount);

        await cut.Instance.DisposeAsync();

        Assert.Equal(1, _interop.GanttV3UnregisterSplitterDragCallCount);
    }

    // ── Keyboard resize (WAI-ARIA separator parity) ─────────────────────────

    [Fact]
    public async Task ArrowRight_On_The_Divider_Grows_The_Name_Column_By_One_Step()
    {
        var cut = RenderTree(p => p.Add(c => c.Tasks, Fixture()));
        var divider = cut.Find(".lumeo-gantt-v3-tree-splitter");

        await divider.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });

        var pane = cut.Find(".lumeo-gantt-v3-tree-splitter").ParentElement!;
        Assert.Contains($"--lumeo-gantt-tree-name-width:{GanttScale.DefaultTreePaneWidth + 16}px", pane.GetAttribute("style"));
    }

    [Fact]
    public void Divider_Exposes_WaiAria_Separator_Attributes()
    {
        var cut = RenderTree(p => p.Add(c => c.Tasks, Fixture()));
        var divider = cut.Find(".lumeo-gantt-v3-tree-splitter");

        Assert.Equal("separator", divider.GetAttribute("role"));
        Assert.Equal("vertical", divider.GetAttribute("aria-orientation"));
        Assert.Equal(GanttScale.DefaultTreePaneWidth.ToString(), divider.GetAttribute("aria-valuenow"));
        Assert.Equal(GanttScale.MinTreePaneWidth.ToString(), divider.GetAttribute("aria-valuemin"));
        Assert.Equal(GanttScale.MaxTreePaneWidth.ToString(), divider.GetAttribute("aria-valuemax"));
        Assert.Equal("0", divider.GetAttribute("tabindex"));
    }

    // ── Geometry sync: scroll offset accounts for a resized pane ────────────

    [Fact]
    public async Task ScrollToDateAsync_After_A_Resize_Targets_A_Pixel_Consistent_With_The_New_Width()
    {
        var state = new GanttState();
        var cut = RenderTree(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture()));

        await cut.InvokeAsync(() => state.ScrollToDateAsync(D(2026, 1, 15)));
        var xBefore = _interop.GanttV3ScrollToXCalls[^1];

        await cut.InvokeAsync(() => _interop.SimulateGanttV3SplitterCommit(400.0));

        await cut.InvokeAsync(() => state.ScrollToDateAsync(D(2026, 1, 15)));
        var xAfter = _interop.GanttV3ScrollToXCalls[^1];

        // Same target DATE, but the leading offset (tree pane width) grew by
        // 400-224=176px, so the absolute scroll-to-X pixel must grow by exactly
        // that amount too (GanttViewportGeometry.LeadingOffset feeding
        // Gantt3.ScrollHostLeadingOffset live — see its own remarks).
        Assert.Equal(176.0, xAfter - xBefore, precision: 3);
    }

    // ── Pure geometry logic (GanttViewportReconciler) ───────────────────────

    [Fact]
    public void LeadingOffset_Uses_The_Given_Width_Not_A_Hardcoded_Constant()
    {
        Assert.Equal(500.0, GanttViewportGeometry.LeadingOffset(true, LayoutDirection.Ltr, 500.0));
        Assert.Equal(0.0, GanttViewportGeometry.LeadingOffset(false, LayoutDirection.Ltr, 500.0));
        Assert.Equal(0.0, GanttViewportGeometry.LeadingOffset(true, LayoutDirection.Rtl, 500.0));
    }

    [Fact]
    public void Decide_Treats_A_TreePaneWidth_Only_Change_As_A_Geometry_Change()
    {
        var prev = new GanttViewportSnapshot(1, false, L.GanttViewMode.Day, 38, true, LayoutDirection.Ltr, 224.0);
        var next = prev with { TreePaneWidth = 300.0 };

        var decision = GanttViewportReconciler.Decide(prev, next, taskRangeDisjoint: false);

        Assert.False(decision.IsNoOp);
        Assert.Equal(GanttRangeSource.Keep, decision.Range);
        Assert.Equal(GanttScrollTarget.CapturedCenter, decision.Target);
    }
}
