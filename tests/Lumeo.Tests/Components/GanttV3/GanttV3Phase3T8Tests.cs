using System.Globalization;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Gantt v3 Phase 3, T8 — the <see cref="L.GanttSettingsMenu"/> companion,
/// <see cref="L.GanttBar.DragGhostTemplate"/>, and <see cref="L.GanttTimeline.BarContextMenu"/>.
/// </summary>
public class GanttV3Phase3T8Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3Phase3T8Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static List<L.GanttTask> Fixture() => new()
    {
        new("t1", "Task One", D(2026, 3, 1), D(2026, 3, 4)),
        new("t2", "Task Two", D(2026, 3, 5), D(2026, 3, 8)),
    };

    // ── GanttSettingsMenu: rendering ─────────────────────────────────────

    [Fact]
    public void Renders_A_Trigger_Button_With_The_Localized_Settings_AriaLabel()
    {
        var cut = _ctx.Render<L.GanttSettingsMenu>();

        var trigger = cut.Find("button[aria-label='Settings']");
        Assert.NotNull(trigger);
    }

    [Fact]
    public void Opening_The_Popover_Shows_All_Four_Group_Headings()
    {
        var cut = _ctx.Render<L.GanttSettingsMenu>();
        cut.Find("button[aria-label='Settings']").Click();

        Assert.Contains("Display", cut.Markup);
        Assert.Contains("Behavior", cut.Markup);
        Assert.Contains("Region", cut.Markup);
        Assert.Contains("Style", cut.Markup);
        Assert.Contains("Reset to defaults", cut.Markup);
    }

    private static AngleSharp.Dom.IElement FindCheckboxByLabel(IRenderedComponent<L.GanttSettingsMenu> cut, string labelText)
    {
        var label = cut.FindAll("label").Single(l => l.TextContent.Trim() == labelText);
        var forId = label.GetAttribute("for");
        return cut.Find($"#{forId}");
    }

    // ── Decision 1 — controlled-ness idiom for the nine plain-bool settings ─

    [Fact]
    public async Task Uncontrolled_Checkbox_Toggle_Flips_And_Stays_Flipped_Locally()
    {
        var cut = _ctx.Render<L.GanttSettingsMenu>();
        cut.Find("button[aria-label='Settings']").Click();

        var checkbox = FindCheckboxByLabel(cut, "Summary bars");
        Assert.Equal("false", checkbox.GetAttribute("aria-checked"));

        await cut.InvokeAsync(() => checkbox.Click());

        checkbox = FindCheckboxByLabel(cut, "Summary bars");
        Assert.Equal("true", checkbox.GetAttribute("aria-checked"));
    }

    [Fact]
    public async Task Controlled_Checkbox_Click_Notifies_Changed_With_The_New_Value()
    {
        bool? notified = null;
        var cut = _ctx.Render<L.GanttSettingsMenu>(p => p
            .Add(c => c.ShowSummaryBars, false)
            .Add(c => c.ShowSummaryBarsChanged, (bool v) => notified = v));
        cut.Find("button[aria-label='Settings']").Click();

        await cut.InvokeAsync(() => FindCheckboxByLabel(cut, "Summary bars").Click());

        Assert.True(notified);
    }

    [Fact]
    public async Task Controlled_Checkbox_The_Parent_Ignoring_ShowSummaryBarsChanged_Reverts_On_The_Next_Render()
    {
        // Same veto contract as every controlled/uncontrolled pair in this
        // campaign (Tasks/TreePaneWidth/SelectedIds/ViewMode): a controlled
        // parent that does not update its own bound value in response to
        // the Changed callback keeps its current value on the next render.
        var cut = _ctx.Render<L.GanttSettingsMenu>(p => p
            .Add(c => c.ShowSummaryBars, false)
            .Add(c => c.ShowSummaryBarsChanged, (bool _) => { /* parent ignores it */ }));
        cut.Find("button[aria-label='Settings']").Click();

        await cut.InvokeAsync(() => FindCheckboxByLabel(cut, "Summary bars").Click());

        // Genuinely NEW parameter pass (not the parameterless cut.Render()
        // trap — see GanttV3CodexRound20Tests' own remarks), still false.
        // The Popover itself stays open across this — its own open state is a
        // SEPARATE, persistently-mounted component untouched by GanttSettingsMenu's
        // own re-render, so re-clicking the trigger here would TOGGLE IT CLOSED
        // instead of keeping it open.
        cut.Render(p => p
            .Add(c => c.ShowSummaryBars, false)
            .Add(c => c.ShowSummaryBarsChanged, (bool _) => { }));

        Assert.Equal("false", FindCheckboxByLabel(cut, "Summary bars").GetAttribute("aria-checked"));
    }

    // DISABLE-CHECK target (design spec Phase 3, T8, decision 1): if
    // EffectiveShowSummaryBars ever stopped consulting ShowSummaryBarsChanged.
    // HasDelegate and just read the raw ShowSummaryBars parameter
    // unconditionally, an UNCONTROLLED checkbox (this exact test) would never
    // visually respond to its own click — a real, user-facing regression this
    // test is specifically shaped to catch. See the T8 report for the
    // predicted-vs-actual disable-check run.
    [Fact]
    public async Task ShowOffscreenIndicators_Uncontrolled_Default_Matches_Gantt3s_Own_True_Default()
    {
        var cut = _ctx.Render<L.GanttSettingsMenu>();
        cut.Find("button[aria-label='Settings']").Click();

        Assert.Equal("true", FindCheckboxByLabel(cut, "Off-screen indicators").GetAttribute("aria-checked"));
    }

    // ── Decision 1 — nullability-based controlled-ness (FirstDayOfWeek/OffDays) ─

    // Both Region toggle groups render day chips with the SAME aria-labels
    // (full day names) — scoped lookups distinguish "the FirstDayOfWeek
    // group" (Single mode, exactly one item ever pressed) from "the OffDays
    // group" (Multiple mode) via ToggleGroup's own root markup
    // (`role="group"` + `data-orientation`, distinct from this component's
    // OWN plain `role="group"` section wrappers, which carry neither).
    private static AngleSharp.Dom.IElement FirstDayOfWeekGroup(IRenderedComponent<L.GanttSettingsMenu> cut) =>
        cut.FindAll("div[role='group'][data-orientation]")[0];

    private static AngleSharp.Dom.IElement OffDaysGroup(IRenderedComponent<L.GanttSettingsMenu> cut) =>
        cut.FindAll("div[role='group'][data-orientation]")[1];

    [Fact]
    public void FirstDayOfWeek_Uncontrolled_Displays_The_Culture_Derived_Day_Pressed()
    {
        var cut = _ctx.Render<L.GanttSettingsMenu>();
        cut.Find("button[aria-label='Settings']").Click();

        var expected = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek.ToString();
        var pressed = FirstDayOfWeekGroup(cut).QuerySelectorAll("[data-toggle-item='true']")
            .Single(b => b.GetAttribute("data-state") == "on");
        Assert.Equal(expected, pressed.GetAttribute("aria-label"));
    }

    [Fact]
    public async Task FirstDayOfWeek_Toggling_A_Day_Sets_It_Explicitly()
    {
        DayOfWeek? notified = null;
        var cut = _ctx.Render<L.GanttSettingsMenu>(p => p
            .Add(c => c.FirstDayOfWeekChanged, (DayOfWeek? v) => notified = v));
        cut.Find("button[aria-label='Settings']").Click();

        var wednesday = FirstDayOfWeekGroup(cut).QuerySelectorAll("[data-toggle-item='true']")
            .First(b => b.GetAttribute("aria-label") == "Wednesday");
        await cut.InvokeAsync(() => wednesday.Click());

        Assert.Equal(DayOfWeek.Wednesday, notified);
    }

    [Fact]
    public async Task FirstDayOfWeek_Controlled_At_A_NonNull_Value_Vetoes_A_Toggle_Attempt()
    {
        // Real, POSSIBLE veto contract for a nullability-controlled setting
        // (design spec Phase 3, T8, decision 1): null is ALWAYS treated as
        // uncontrolled (there is no way to distinguish "explicitly passed
        // null" from "never set" on a nullable value-type parameter — same
        // limitation Gantt3.TreePaneWidth's own identical idiom has), so a
        // genuine veto needs a controlled NON-null value — a consumer pinned
        // at Monday who ignores further picks.
        var cut = _ctx.Render<L.GanttSettingsMenu>(p => p
            .Add(c => c.FirstDayOfWeek, (DayOfWeek?)DayOfWeek.Monday)
            .Add(c => c.FirstDayOfWeekChanged, (DayOfWeek? _) => { /* parent ignores it */ }));
        cut.Find("button[aria-label='Settings']").Click();

        var wednesday = FirstDayOfWeekGroup(cut).QuerySelectorAll("[data-toggle-item='true']")
            .First(b => b.GetAttribute("aria-label") == "Wednesday");
        await cut.InvokeAsync(() => wednesday.Click());

        cut.Render(p => p
            .Add(c => c.FirstDayOfWeek, (DayOfWeek?)DayOfWeek.Monday)
            .Add(c => c.FirstDayOfWeekChanged, (DayOfWeek? _) => { }));

        var pressedNow = FirstDayOfWeekGroup(cut).QuerySelectorAll("[data-toggle-item='true']")
            .Single(b => b.GetAttribute("data-state") == "on");
        Assert.Equal("Monday", pressedNow.GetAttribute("aria-label"));
    }

    // A null FirstDayOfWeek can NEVER veto (see the test above's own
    // remarks) — left unbound/null, every toggle click moves the effective
    // day forward (uncontrolled, self-tracking), which is the CORRECT,
    // intentional behavior, not a bug.
    [Fact]
    public async Task FirstDayOfWeek_Null_With_Changed_Bound_Still_Behaves_Uncontrolled_Not_As_A_Veto()
    {
        var cut = _ctx.Render<L.GanttSettingsMenu>(p => p
            .Add(c => c.FirstDayOfWeekChanged, (DayOfWeek? _) => { /* observes only, never rebinds FirstDayOfWeek itself */ }));
        cut.Find("button[aria-label='Settings']").Click();

        var wednesday = FirstDayOfWeekGroup(cut).QuerySelectorAll("[data-toggle-item='true']")
            .First(b => b.GetAttribute("aria-label") == "Wednesday");
        await cut.InvokeAsync(() => wednesday.Click());

        var pressedNow = FirstDayOfWeekGroup(cut).QuerySelectorAll("[data-toggle-item='true']")
            .Single(b => b.GetAttribute("data-state") == "on");
        Assert.Equal("Wednesday", pressedNow.GetAttribute("aria-label"));
    }

    [Fact]
    public void OffDays_Uncontrolled_Displays_The_Culture_Derived_Weekend_Pressed()
    {
        var cut = _ctx.Render<L.GanttSettingsMenu>();
        cut.Find("button[aria-label='Settings']").Click();

        // en-US default culture in the test environment -> Sat/Sun.
        var pressedLabels = OffDaysGroup(cut).QuerySelectorAll("[data-toggle-item='true']")
            .Where(b => b.GetAttribute("data-state") == "on")
            .Select(b => b.GetAttribute("aria-label"))
            .ToList();
        Assert.Contains("Saturday", pressedLabels);
        Assert.Contains("Sunday", pressedLabels);
        Assert.Equal(2, pressedLabels.Count);
    }

    // DISABLE-CHECK target (design spec Phase 3, T8, decision 1 — OffDays):
    // clearing every chip must produce an EXPLICIT empty set, never null
    // (which would silently reintroduce the culture weekend). See the T8
    // report for the predicted-vs-actual disable-check run against
    // HandleOffDaysToggledAsync's own "days.Count == 0 ? null : days" trap.
    [Fact]
    public async Task OffDays_Clearing_Every_Chip_Produces_An_Explicit_Empty_Set_Not_A_Reverted_Culture_Default()
    {
        IReadOnlySet<DayOfWeek>? notified = null;
        var cut = _ctx.Render<L.GanttSettingsMenu>(p => p
            .Add(c => c.OffDaysChanged, (IReadOnlySet<DayOfWeek>? v) => notified = v));
        cut.Find("button[aria-label='Settings']").Click();

        var saturday = OffDaysGroup(cut).QuerySelectorAll("[data-toggle-item='true']").First(b => b.GetAttribute("aria-label") == "Saturday");
        await cut.InvokeAsync(() => saturday.Click());
        // Re-locate after the render this click triggered — ToggleGroup's
        // own item registry rebuilds per render (see its own remarks), so
        // holding a stale element reference across a StateHasChanged here is
        // the same "re-find, don't reuse" discipline the codebase already
        // applies elsewhere.
        var sunday = OffDaysGroup(cut).QuerySelectorAll("[data-toggle-item='true']").First(b => b.GetAttribute("aria-label") == "Sunday");
        await cut.InvokeAsync(() => sunday.Click());

        Assert.NotNull(notified);
        Assert.Empty(notified!);
    }

    // ── Decision 2 — Reset restores THIS instance's mount-time baseline ────

    [Fact]
    public async Task Reset_Restores_An_Uncontrolled_Setting_To_Its_Own_Declared_Default()
    {
        var cut = _ctx.Render<L.GanttSettingsMenu>();
        cut.Find("button[aria-label='Settings']").Click();
        await cut.InvokeAsync(() => FindCheckboxByLabel(cut, "Summary bars").Click());
        Assert.Equal("true", FindCheckboxByLabel(cut, "Summary bars").GetAttribute("aria-checked"));

        await cut.InvokeAsync(() => cut.Find("[data-testid='gantt-settings-reset']").Click());

        Assert.Equal("false", FindCheckboxByLabel(cut, "Summary bars").GetAttribute("aria-checked"));
    }

    // DISABLE-CHECK target (design spec Phase 3, T8, decision 2): Reset must
    // restore the value THIS instance mounted with — for a CONTROLLED field
    // whose consumer's own app default is non-default (true here), NOT the
    // library's bare `false`. See the T8 report for the predicted-vs-actual
    // disable-check run against a version of ResetAsync that used hardcoded
    // literal defaults instead of the captured Baseline.
    [Fact]
    public async Task Reset_Restores_A_Controlled_Settings_Own_MountTime_Value_Not_The_Librarys_Hardcoded_Default()
    {
        var current = true; // the consumer's OWN app default, non-default relative to the library's bare `false`
        bool? lastNotified = null;
        var cut = _ctx.Render<L.GanttSettingsMenu>(p => p
            .Add(c => c.ShowSummaryBars, current)
            .Add(c => c.ShowSummaryBarsChanged, (bool v) => { current = v; lastNotified = v; }));
        cut.Find("button[aria-label='Settings']").Click();
        Assert.Equal("true", FindCheckboxByLabel(cut, "Summary bars").GetAttribute("aria-checked"));

        // Flip it off first (simulating a user session change)...
        await cut.InvokeAsync(() => FindCheckboxByLabel(cut, "Summary bars").Click());
        cut.Render(p => p
            .Add(c => c.ShowSummaryBars, current)
            .Add(c => c.ShowSummaryBarsChanged, (bool v) => { current = v; lastNotified = v; }));
        Assert.Equal("false", FindCheckboxByLabel(cut, "Summary bars").GetAttribute("aria-checked"));

        // ...then Reset: expect it back to `true` (this instance's own mount
        // baseline), NOT the library's bare `false`.
        await cut.InvokeAsync(() => cut.Find("[data-testid='gantt-settings-reset']").Click());
        Assert.True(lastNotified);
        cut.Render(p => p
            .Add(c => c.ShowSummaryBars, current)
            .Add(c => c.ShowSummaryBarsChanged, (bool v) => { current = v; lastNotified = v; }));

        Assert.Equal("true", FindCheckboxByLabel(cut, "Summary bars").GetAttribute("aria-checked"));
    }

    // Bug fix (Codex review, P2 #10): the mount-time Baseline stored the
    // CONTROLLED OffDays parameter's own set BY REFERENCE, not a copy — a
    // consumer mutating its own HashSet<DayOfWeek> in place (no new Blazor
    // render, just the same object gaining/losing members) silently mutated
    // the "mount-time" baseline right along with it, so Reset restored
    // whatever the set held AT RESET TIME, not what it held when this menu
    // instance actually opened.
    [Fact]
    public async Task Reset_Restores_Controlled_OffDays_To_A_Snapshot_Not_The_Live_Mutable_Set()
    {
        var ownedOffDays = new HashSet<DayOfWeek> { DayOfWeek.Friday };
        IReadOnlySet<DayOfWeek>? notified = null;
        var cut = _ctx.Render<L.GanttSettingsMenu>(p => p
            .Add(c => c.OffDays, ownedOffDays)
            .Add(c => c.OffDaysChanged, (IReadOnlySet<DayOfWeek>? v) => notified = v));
        cut.Find("button[aria-label='Settings']").Click();
        Assert.Equal(
            "on",
            OffDaysGroup(cut).QuerySelectorAll("[data-toggle-item='true']").First(b => b.GetAttribute("aria-label") == "Friday").GetAttribute("data-state"));

        // Mutate the CONSUMER's own set in place — same object reference,
        // no new Render/parameter assignment at all (simulating a real
        // controlled caller's app-level state mutation the component never
        // observes as a parameter change).
        ownedOffDays.Add(DayOfWeek.Monday);

        await cut.InvokeAsync(() => cut.Find("[data-testid='gantt-settings-reset']").Click());

        // Expected: Reset restores the ORIGINAL mount-time snapshot, {Friday}
        // only. Pre-fix, this was worse than "re-notifies {Friday, Monday}":
        // _baseline.OffDays held the SAME reference as ownedOffDays, so the
        // mutation above landed in the "baseline" too — SetOffDaysAsync's own
        // SetEquals(EffectiveOffDays, next) then compared the live (mutated)
        // set against the identically-mutated "baseline" via the SAME
        // reference, found them equal, and returned before ever calling
        // OffDaysChanged — a silent total no-op (`notified` stayed null).
        Assert.NotNull(notified);
        Assert.Equal(new[] { DayOfWeek.Friday }, notified!.OrderBy(d => d));
    }

    // ── GanttNav.TrailingContent / Gantt3.TrailingContent passthrough ──────

    [Fact]
    public void GanttNav_Renders_Nothing_Extra_When_TrailingContent_Is_Null()
    {
        var cut = _ctx.Render<L.GanttNav>();
        Assert.DoesNotContain("gantt-settings", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GanttNav_Renders_TrailingContent_When_Set()
    {
        var cut = _ctx.Render<L.GanttNav>(p => p
            .Add(c => c.TrailingContent, (Microsoft.AspNetCore.Components.RenderFragment)(b => b.AddMarkupContent(0, "<span data-testid='trailing-probe'>x</span>"))));

        Assert.NotNull(cut.Find("[data-testid='trailing-probe']"));
    }

    [Fact]
    public void Gantt3_Forwards_TrailingContent_Through_To_GanttNav()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.TrailingContent, (Microsoft.AspNetCore.Components.RenderFragment)(b => b.AddMarkupContent(0, "<span data-testid='trailing-probe'>x</span>"))));

        Assert.NotNull(cut.Find("[data-testid='trailing-probe']"));
    }

    // ── DragGhostTemplate (design spec Phase 3, T8, decision 3) ────────────

    [Fact]
    public void GanttBar_Renders_A_Hidden_Ghost_Template_When_Set_And_Not_Readonly()
    {
        var single = new List<L.GanttTask> { new("t1", "Task One", D(2026, 3, 1), D(2026, 3, 4)) };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, single)
            .Add(c => c.DragGhostTemplate, (Microsoft.AspNetCore.Components.RenderFragment<L.GanttTask>)(t => b => b.AddMarkupContent(0, $"<span data-testid='custom-ghost-content'>{t.Id}</span>"))));

        var template = cut.Find("[data-gantt-ghost-template]");
        Assert.Contains("hidden", template.GetAttribute("class"));
        Assert.Contains("custom-ghost-content", template.InnerHtml);
    }

    [Fact]
    public void GanttBar_Omits_The_Hidden_Ghost_Template_When_DragGhostTemplate_Is_Null()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, Fixture()));
        Assert.Empty(cut.FindAll("[data-gantt-ghost-template]"));
    }

    [Fact]
    public void GanttBar_Omits_The_Hidden_Ghost_Template_When_Readonly_Even_If_DragGhostTemplate_Is_Set()
    {
        // No drag ever starts on a Readonly chart (SyncDragRegistrationAsync's
        // own "no listeners at all" contract) — nothing for this template to
        // ever be cloned into, so it costs nothing to omit it entirely.
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.Readonly, true)
            .Add(c => c.DragGhostTemplate, (Microsoft.AspNetCore.Components.RenderFragment<L.GanttTask>)(t => b => b.AddMarkupContent(0, "<span>x</span>"))));

        Assert.Empty(cut.FindAll("[data-gantt-ghost-template]"));
    }

    // DISABLE-CHECK target (design spec Phase 3, T8, decision 3 — coordinate
    // space): the hidden ghost template MUST carry the exact same
    // WrapperStyle (left/top/width/height/--lumeo-gantt-bar-* custom
    // properties) as the real bar it sits beside, or gantt-v3.js's makeGhost
    // (which clones it verbatim) would render the custom ghost in the wrong
    // position — the pinned Phase-2 "split coordinate space" bug class. See
    // the T8 report for the predicted-vs-actual disable-check run against a
    // version of GanttBar.razor's markup that dropped `style="@WrapperStyle"`
    // from the hidden template.
    [Fact]
    public void Hidden_Ghost_Template_Carries_The_Exact_Same_WrapperStyle_As_The_Real_Bar()
    {
        var single = new List<L.GanttTask> { new("t1", "Task One", D(2026, 3, 1), D(2026, 3, 4)) };
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, single)
            .Add(c => c.DragGhostTemplate, (Microsoft.AspNetCore.Components.RenderFragment<L.GanttTask>)(t => b => b.AddMarkupContent(0, "<span>x</span>"))));

        var barStyle = cut.Find("[data-task-id='t1']").GetAttribute("style") ?? "";
        var templateStyle = cut.Find("[data-gantt-ghost-template]").GetAttribute("style") ?? "";

        Assert.Contains("--lumeo-gantt-bar-x", barStyle);
        Assert.Equal(barStyle, templateStyle);
    }

    // ── BarContextMenu (design spec Phase 3, T8, decisions 4 & 5) ──────────

    [Fact]
    public void GanttTimeline_Registers_The_BarContextMenu_Channel_When_Set()
    {
        _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.BarContextMenu, (Microsoft.AspNetCore.Components.RenderFragment<L.GanttTask>)(t => b => b.AddMarkupContent(0, "<span>x</span>"))));

        Assert.Equal(1, _interop.GanttV3RegisterBarContextMenuCallCount);
    }

    [Fact]
    public void GanttTimeline_Does_Not_Register_The_BarContextMenu_Channel_When_Unset()
    {
        _ctx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, Fixture()));
        Assert.Equal(0, _interop.GanttV3RegisterBarContextMenuCallCount);
    }

    // DISABLE-CHECK target (design spec Phase 3, T8, decision 5): unlike the
    // drag engine, BarContextMenu registration is deliberately INDEPENDENT of
    // Readonly (a context menu is a view action, not an edit) — see the T8
    // report for the predicted-vs-actual disable-check run against a version
    // of SyncBarContextMenuRegistrationAsync that added a Readonly guard
    // mirroring SyncDragRegistrationAsync's own.
    [Fact]
    public void BarContextMenu_Registration_Is_Not_Gated_By_Readonly()
    {
        _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.Readonly, true)
            .Add(c => c.BarContextMenu, (Microsoft.AspNetCore.Components.RenderFragment<L.GanttTask>)(t => b => b.AddMarkupContent(0, "<span>x</span>"))));

        Assert.Equal(1, _interop.GanttV3RegisterBarContextMenuCallCount);
    }

    [Fact]
    public async Task NotifyBarContextMenu_Opens_The_Menu_And_Renders_Content_For_The_Resolved_Task()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.BarContextMenu, (Microsoft.AspNetCore.Components.RenderFragment<L.GanttTask>)(t => b => b.AddMarkupContent(0, $"<span data-testid='ctx-content'>{t.Name}</span>"))));

        await cut.InvokeAsync(() => _interop.SimulateGanttV3BarContextMenu("t2", 100, 200));
        cut.Render();

        var probe = cut.Find("[data-testid='ctx-content']");
        Assert.Equal("Task Two", probe.TextContent);
    }

    [Fact]
    public async Task NotifyBarContextMenu_With_An_Unknown_TaskId_Is_A_NoOp()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.BarContextMenu, (Microsoft.AspNetCore.Components.RenderFragment<L.GanttTask>)(t => b => b.AddMarkupContent(0, $"<span data-testid='ctx-content'>{t.Name}</span>"))));

        await cut.InvokeAsync(() => _interop.SimulateGanttV3BarContextMenu("does-not-exist", 0, 0));
        cut.Render();

        Assert.Empty(cut.FindAll("[data-testid='ctx-content']"));
    }

    [Fact]
    public async Task DisposeAsync_Unregisters_The_BarContextMenu_Channel()
    {
        var cut = _ctx.Render<L.GanttTimeline>(p => p
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.RangeStart, D(2026, 3, 1))
            .Add(c => c.RangeEnd, D(2026, 3, 10))
            .Add(c => c.BarContextMenu, (Microsoft.AspNetCore.Components.RenderFragment<L.GanttTask>)(t => b => b.AddMarkupContent(0, "<span>x</span>"))));

        Assert.Equal(1, _interop.GanttV3RegisterBarContextMenuCallCount);
        await cut.Instance.DisposeAsync();

        Assert.Equal(1, _interop.GanttV3UnregisterBarContextMenuCallCount);
    }
}
