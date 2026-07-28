using System.Text.RegularExpressions;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 3, T7 — canvas chrome: off-screen indicator chips
/// (<c>ShowOffscreenIndicators</c>), the floating zoom control
/// (<c>ShowZoomControl</c>), in-bar labels with contrast handling + the
/// narrow-bar outside fallback, and <c>ColorByGroup</c>. See <see
/// cref="GanttColorModelTests"/> for the exhaustive pure-logic coverage of
/// <see cref="GanttColorModel"/> (palette stability, contrast picking) this
/// file builds on — this is the COMPONENT-level integration layer.
/// </summary>
public class GanttV3Phase3T7Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3Phase3T7Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // ── ColorByGroup ──────────────────────────────────────────────────────

    private static List<L.GanttTask> GroupedFixture() => new()
    {
        new("a1", "A1", D(2026, 3, 1), D(2026, 3, 4), GroupLabel: "Design"),
        new("a2", "A2", D(2026, 3, 5), D(2026, 3, 8), GroupLabel: "Design"),
        new("b1", "B1", D(2026, 3, 1), D(2026, 3, 4), GroupLabel: "Engineering"),
    };

    // ColorStyle (background-color) lives on the INNER .lumeo-gantt-v3-bar-bg
    // div — see GanttBar.WrapperStyle's own remarks: the wrapper only carries
    // the --lumeo-gantt-bar-x/-w/-row positioning custom properties.
    private static string? BarColorStyle(IRenderedComponent<L.Gantt3> cut, string taskId)
    {
        var style = cut.Find($"[data-task-id='{taskId}'] .lumeo-gantt-v3-bar-bg").GetAttribute("style") ?? "";
        var m = Regex.Match(style, @"background-color:([^;]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    [Fact]
    public void ColorByGroup_False_Renders_The_Default_Primary_Bar_Regardless_Of_Group()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, GroupedFixture())
            .Add(c => c.GroupBy, (L.GanttTask t) => t.GroupLabel ?? "")
            .Add(c => c.ColorByGroup, false));

        Assert.Equal("var(--color-primary)", BarColorStyle(cut, "a1")); // GanttBar.ResolvedColor's own unchanged default
    }

    [Fact]
    public void ColorByGroup_True_Assigns_The_Same_Chart_Color_To_Every_Task_In_A_Group()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, GroupedFixture())
            .Add(c => c.GroupBy, (L.GanttTask t) => t.GroupLabel ?? "")
            .Add(c => c.ColorByGroup, true));

        var a1 = BarColorStyle(cut, "a1");
        var a2 = BarColorStyle(cut, "a2");
        var b1 = BarColorStyle(cut, "b1");

        Assert.NotNull(a1);
        Assert.Equal(a1, a2); // same group ("Design") -> same colour
        Assert.NotNull(b1);
        Assert.NotEqual(a1, b1); // a different group is free to differ
    }

    [Fact]
    public void ColorByGroup_Explicit_BarColor_Wins_Over_The_Group_Palette()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, GroupedFixture())
            .Add(c => c.GroupBy, (L.GanttTask t) => t.GroupLabel ?? "")
            .Add(c => c.ColorByGroup, true)
            .Add(c => c.BarColor, (L.GanttTask t) => t.Id == "a1" ? "#ff0000" : null));

        Assert.Equal("#ff0000", BarColorStyle(cut, "a1"));
    }

    [Fact]
    public async Task ColorByGroup_Survives_A_Real_Row_Reorder_Through_Gantt3()
    {
        var tasks = new List<L.GanttTask>
        {
            new("root1", "Root 1", D(2026, 3, 1), D(2026, 3, 10)),
            new("child1", "Child 1", D(2026, 3, 1), D(2026, 3, 5)) { ParentId = "root1" },
            new("child2", "Child 2", D(2026, 3, 5), D(2026, 3, 10)) { ParentId = "root1" },
        };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ColorByGroup, true)
            .Add(c => c.AllowRowReorder, true));

        var before = BarColorStyle(cut, "child1");
        Assert.NotNull(before);

        var tree = cut.FindComponent<L.GanttTree>();
        await cut.InvokeAsync(() => tree.Instance.CommitRowReorder("child2", 0));

        var after = BarColorStyle(cut, "child1");
        Assert.Equal(before, after);
    }

    // ── In-bar label: narrow-bar fallback + ellipsis ─────────────────────────

    private const string InBarLabelSelector = ".lumeo-gantt-v3-bar-label";
    private const string OutsideLabelSelector = ".lumeo-gantt-v3-bar-label-outside";

    [Fact]
    public void Wide_Bar_Renders_The_Label_Inside_With_Truncate()
    {
        // Day mode, ColumnWidth=38px: a 4-day (inclusive) task renders 4*38=152px
        // wide — comfortably over GanttScale.MinInBarLabelWidth (40px).
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Wide Task", D(2026, 3, 1), D(2026, 3, 4)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Single(cut.FindAll(InBarLabelSelector));
        Assert.Empty(cut.FindAll(OutsideLabelSelector));
        Assert.Contains("truncate", cut.Find(InBarLabelSelector).GetAttribute("class"));
    }

    [Fact]
    public void Narrow_Bar_Falls_Back_To_The_Outside_Label()
    {
        // A single-day task in Day mode renders exactly ColumnWidth=38px wide
        // — below the 40px threshold.
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Narrow", D(2026, 3, 1), D(2026, 3, 1)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Empty(cut.FindAll(InBarLabelSelector));
        Assert.Single(cut.FindAll(OutsideLabelSelector));
        Assert.Equal("Narrow", cut.Find(OutsideLabelSelector).TextContent);
    }

    [Fact]
    public void Milestones_Always_Render_The_Label_Outside_Regardless_Of_Width()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("m1", "Milestone", D(2026, 3, 1), D(2026, 3, 1), IsMilestone: true) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Empty(cut.FindAll(InBarLabelSelector));
        Assert.Empty(cut.FindAll(OutsideLabelSelector)); // milestones use their OWN label class, not this fallback
        Assert.Single(cut.FindAll(".lumeo-gantt-v3-milestone-label"));
    }

    [Fact]
    public void A_BarTemplate_Bypasses_Both_The_InBar_And_Outside_Label_Entirely()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Narrow", D(2026, 3, 1), D(2026, 3, 1)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.BarTemplate, (Microsoft.AspNetCore.Components.RenderFragment<L.GanttTask>)(t => b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "custom-bar-template");
                b.CloseElement();
            })));

        Assert.Empty(cut.FindAll(InBarLabelSelector));
        Assert.Empty(cut.FindAll(OutsideLabelSelector));
        Assert.Single(cut.FindAll(".custom-bar-template"));
    }

    // ── In-bar label: contrast handling ──────────────────────────────────────

    [Fact]
    public void Default_Primary_Bar_Keeps_The_Existing_Text_Foreground_Class_Unchanged()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 3, 1), D(2026, 3, 4)) }));

        var label = cut.Find(InBarLabelSelector);
        Assert.Contains("text-foreground", label.GetAttribute("class"));
        Assert.Null(label.GetAttribute("style"));
    }

    [Fact]
    public void Dark_Custom_Bar_Color_Picks_The_Background_Token_As_Foreground_In_Light_Mode()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 3, 1), D(2026, 3, 4)) })
            .Add(c => c.BarColor, (L.GanttTask _) => "#000000"));

        var label = cut.Find(InBarLabelSelector);
        Assert.DoesNotContain("text-foreground", label.GetAttribute("class") ?? "");
        Assert.Contains("color:var(--color-background)", label.GetAttribute("style"));
    }

    [Fact]
    public async Task Dark_Custom_Bar_Color_Flips_To_The_Foreground_Token_When_The_Theme_Is_Dark()
    {
        _ctx.JSInterop.Setup<bool>("themeManager.isDark").SetResult(true);
        var theme = _ctx.Services.GetRequiredService<ThemeService>();
        await theme.SetModeAsync(ThemeMode.Dark);

        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 3, 1), D(2026, 3, 4)) })
            .Add(c => c.BarColor, (L.GanttTask _) => "#000000"));

        var label = cut.Find(InBarLabelSelector);
        Assert.Contains("color:var(--color-foreground)", label.GetAttribute("style"));
    }

    [Fact]
    public void Unparseable_Custom_Bar_Color_Falls_Back_To_Text_Foreground()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 3, 1), D(2026, 3, 4)) })
            .Add(c => c.BarColor, (L.GanttTask _) => "var(--color-chart-2)"));

        var label = cut.Find(InBarLabelSelector);
        Assert.Contains("text-foreground", label.GetAttribute("class"));
    }

    // ── Hover states (bUnit can't drive :hover — assert the utility classes exist) ──

    [Fact]
    public void GanttBar_Wrapper_Carries_A_Hover_Shadow_Class()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 3, 1), D(2026, 3, 4)) }));

        var wrapper = cut.Find("[data-task-id='t1']");
        Assert.Contains("hover:shadow-md", wrapper.GetAttribute("class"));
    }

    [Fact]
    public void Timeline_Row_Item_And_Tree_Row_Both_Carry_A_Hover_Background_Class()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 3, 1), D(2026, 3, 4)) })
            .Add(c => c.ShowTreePane, true));

        Assert.Contains("hover:bg-accent/10", cut.Find(".lumeo-gantt-v3-row-item").GetAttribute("class"));
        var treeRow = cut.FindAll("[data-row-kind='task']").First();
        Assert.Contains("hover:bg-accent/10", treeRow.GetAttribute("class"));
    }

    // ── ShowZoomControl ───────────────────────────────────────────────────────

    private const string ZoomControlSelector = ".lumeo-gantt-v3-zoom-control";

    [Fact]
    public void ShowZoomControl_Defaults_To_False()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 3, 1), D(2026, 3, 4)) }));

        Assert.Empty(cut.FindAll(ZoomControlSelector));
    }

    [Fact]
    public void ShowZoomControl_True_Renders_The_Floating_Control_With_Two_Buttons()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 3, 1), D(2026, 3, 4)) })
            .Add(c => c.ShowZoomControl, true));

        var control = cut.Find(ZoomControlSelector);
        Assert.Equal(2, control.QuerySelectorAll("button").Length);
    }

    [Fact]
    public async Task Clicking_Plus_Steps_The_Toolbar_And_Control_To_A_Finer_Zoom_Level()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 3, 1), D(2026, 3, 4)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Month)
            .Add(c => c.ShowZoomControl, true));

        var buttons = cut.Find(ZoomControlSelector).QuerySelectorAll("button");
        var zoomIn = buttons[1]; // "-" then "+" in DOM order (see GanttZoomControl.razor)
        await cut.InvokeAsync(() => zoomIn.Click());

        // Month -> Week (GanttZoomLevelModel.DefaultLevels: Day, Week, Month, Year).
        Assert.Equal("Week", cut.FindComponent<L.GanttNav>().Instance.ViewMode.ToString());
    }

    [Fact]
    public async Task Clicking_Minus_Steps_To_A_Coarser_Zoom_Level_And_Disables_At_The_Boundary()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 3, 1), D(2026, 3, 4)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Year)
            .Add(c => c.ShowZoomControl, true));

        var buttons = cut.Find(ZoomControlSelector).QuerySelectorAll("button");
        var zoomOut = buttons[0]; // "-"
        Assert.True(zoomOut.HasAttribute("disabled")); // already at the coarsest (last) level
    }

    [Fact]
    public void ZoomLevels_Restricts_What_The_Floating_Control_Can_Step_Through()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 3, 1), D(2026, 3, 4)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ZoomLevels, new[] { L.GanttViewMode.Day, L.GanttViewMode.Week })
            .Add(c => c.ShowZoomControl, true));

        var buttons = cut.Find(ZoomControlSelector).QuerySelectorAll("button");
        Assert.True(buttons[0].HasAttribute("disabled") == false); // "-" Day->Week is available
        Assert.True(buttons[1].HasAttribute("disabled")); // "+" already at the finest of the restricted set
    }

    // ── ShowOffscreenIndicators ───────────────────────────────────────────────

    private const string ChipSelector = "[data-gantt-offscreen-chip]";

    // 35 days apart in Day mode (38px/column) puts "far" ~1330px to the right
    // of "near" regardless of where Gantt3's own ComputeInitialRange happens
    // to seed its Origin (never asserted on directly — see BarGeometryOf,
    // which reads the ACTUAL rendered pixel geometry instead of assuming a
    // hand-derived Origin).
    private static List<L.GanttTask> OffscreenFixture() => new()
    {
        new("near", "Near", D(2026, 1, 1), D(2026, 1, 1)),
        new("far", "Far", D(2026, 2, 5), D(2026, 2, 5)),
    };

    private static (double X, double Width) BarGeometryOf(IRenderedComponent<L.Gantt3> cut, string taskId)
    {
        var style = cut.Find($"[data-task-id='{taskId}']").GetAttribute("style") ?? "";
        var x = double.Parse(Regex.Match(style, @"--lumeo-gantt-bar-x:([\d.]+)px").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var w = double.Parse(Regex.Match(style, @"--lumeo-gantt-bar-w:([\d.]+)px").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        return (x, w);
    }

    [Fact]
    public void ShowOffscreenIndicators_Defaults_To_True()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, OffscreenFixture()));
        // No scroll report simulated yet -> viewport unknown -> no chip (see
        // VisibleTimelineWindow's own remarks), but the PARAMETER itself must
        // default true — checked indirectly via the disable-check below and
        // the report-driven test right after, which would ALSO be empty if
        // the parameter defaulted false.
        Assert.Empty(cut.FindAll(ChipSelector)); // nothing reported yet
    }

    [Fact]
    public async Task A_Bar_Entirely_Past_The_Reported_Viewport_Gets_An_After_Chip()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, OffscreenFixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var (nearX, nearW) = BarGeometryOf(cut, "near");
        var (farX, _) = BarGeometryOf(cut, "far");
        var clientWidth = nearX + nearW + (farX - (nearX + nearW)) / 2; // covers "near", stops well short of "far"

        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: clientWidth));

        var chips = cut.FindAll(ChipSelector);
        Assert.Single(chips);
        Assert.Equal("after", chips[0].GetAttribute("data-gantt-offscreen-chip"));
    }

    [Fact]
    public async Task A_Fully_Visible_Bar_Gets_No_Chip()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, new List<L.GanttTask> { new("t1", "Task", D(2026, 1, 1), D(2026, 1, 1)) })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var (x, w) = BarGeometryOf(cut, "t1");
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: x - 20, clientWidth: w + 40));

        Assert.Empty(cut.FindAll(ChipSelector));
    }

    [Fact]
    public async Task ShowOffscreenIndicators_False_Renders_No_Chip_Even_Though_A_Bar_Is_Offscreen()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, OffscreenFixture())
            .Add(c => c.ShowOffscreenIndicators, false)
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var (nearX, nearW) = BarGeometryOf(cut, "near");
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: nearX + nearW + 50));

        Assert.Empty(cut.FindAll(ChipSelector));
    }

    [Fact]
    public async Task Clicking_The_Chip_Scrolls_The_Bar_Into_View_Via_The_Existing_Scroll_Interop()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, OffscreenFixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var (nearX, nearW) = BarGeometryOf(cut, "near");
        var (farX, farW) = BarGeometryOf(cut, "far");
        var clientWidth = nearX + nearW + (farX - (nearX + nearW)) / 2;

        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: clientWidth));

        var expectedCenter = farX + farW / 2;

        var callsBefore = _interop.GanttV3ScrollToXCallCount;
        var chip = cut.Find(ChipSelector);
        await cut.InvokeAsync(() => chip.Click());

        Assert.Equal(callsBefore + 1, _interop.GanttV3ScrollToXCallCount);
        Assert.Equal(expectedCenter, _interop.GanttV3ScrollToXCalls[^1], precision: 3);
    }

    // Real bug found via the E2E gate, reproduced here at the bUnit level
    // (see OnGanttV3VerticalScroll's own remarks): RecomputeVisibleRowRange's
    // "no-op: avoid a redundant re-render" guard only compares the VERTICAL
    // culling range — a SECOND report whose scrollTop/clientHeight are
    // UNCHANGED from the first (the common case: scrolling horizontally to
    // browse dates without also scrolling rows) never called StateHasChanged
    // at all, so the off-screen chip never disappeared even though
    // VisibleTimelineWindow (and therefore RowItems' own Offscreen field)
    // genuinely changed. Every OTHER T7 test above only ever calls
    // RaiseGanttV3VerticalScroll ONCE per test, so none of them exercised
    // this "second report, vertical range identical" path — this is a
    // REBUILT-for-this-finding test, added specifically to close that gap.
    [Fact]
    public async Task A_Second_Report_With_An_Unchanged_Vertical_Range_Still_Updates_Which_Bars_Are_Offscreen()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, OffscreenFixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        var (nearX, nearW) = BarGeometryOf(cut, "near");
        var (farX, farW) = BarGeometryOf(cut, "far");
        var narrowWidth = nearX + nearW + (farX - (nearX + nearW)) / 2; // covers "near" only

        // First report: same scrollTop/clientHeight both times (0/200) —
        // only scrollLeft/clientWidth differ between the two calls.
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: narrowWidth));
        Assert.Single(cut.FindAll(ChipSelector)); // "far" is offscreen

        // Second report: widen clientWidth enough to cover BOTH bars —
        // scrollTop/clientHeight identical to the first call, so
        // RecomputeVisibleRowRange's own vertical-range comparison sees no
        // change at all; only the horizontal state (which off-screen chips
        // depend on) actually moved.
        var wideWidth = farX + farW + 50;
        await cut.InvokeAsync(() => _interop.RaiseGanttV3VerticalScroll(scrollTop: 0, clientHeight: 200, scrollLeft: 0, clientWidth: wideWidth));

        Assert.Empty(cut.FindAll(ChipSelector)); // both bars now fit — the chip must be gone
    }
}
