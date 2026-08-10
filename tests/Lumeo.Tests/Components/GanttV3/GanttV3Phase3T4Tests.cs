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
/// Gantt v3 Phase 3, T4 — hoistable GanttState + imperative API: the
/// COMPONENT-level integration layer (see <see cref="GanttStateImperativeApiTests"/>
/// for the pure, Blazor-free attach/detach POLICY unit tests against a fake host).
/// This file proves a REAL <see cref="L.Gantt3"/> fulfills the
/// <c>IGanttStateHost</c> attach contract, routes imperative scroll requests
/// through its EXISTING scroll interop, routes an imperative <c>SetViewMode</c>
/// through its CLAIM-based view-mode ownership model (PR #382), and that two
/// <see cref="L.Gantt3"/> instances sharing ONE <see cref="GanttState"/> stay in
/// sync (the REUI "nav and view rendered separately against one shared state
/// instance" contract).
/// </summary>
public class GanttV3Phase3T4Tests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3Phase3T4Tests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static List<L.GanttTask> Fixture() => new()
    {
        new("t1", "Design", D(2026, 1, 2), D(2026, 1, 6)),
        new("t2", "Build", D(2026, 1, 10), D(2026, 1, 20)),
    };

    // ── State == null: byte-identical default (see the T4 report for the full
    //    before/after markup diff proof — this is a lightweight smoke regression
    //    covering the same claim) ──────────────────────────────────────────────

    [Fact]
    public void Omitting_State_Still_Renders_A_Fully_Functional_Chart()
    {
        var cut = _ctx.Render<L.Gantt3>(p => p.Add(c => c.Tasks, Fixture()));

        Assert.Contains("Design", cut.Markup);
        Assert.Contains("Build", cut.Markup);
    }

    // ── Imperative scroll API routes through the EXISTING interop ──────────────

    [Fact]
    public async Task ScrollToDateAsync_On_A_Hoisted_Gantt3_Issues_A_Real_GanttV3ScrollToXAsync_Call()
    {
        var state = new GanttState();
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture()));

        var callsBefore = _interop.GanttV3ScrollToXCallCount;

        await cut.InvokeAsync(() => state.ScrollToDateAsync(D(2026, 1, 15)));

        Assert.True(_interop.GanttV3ScrollToXCallCount > callsBefore,
            "expected ScrollToDateAsync to issue a NEW GanttV3ScrollToXAsync call through the attached Gantt3's existing scroll interop");
    }

    [Fact]
    public async Task ScrollToTaskAsync_On_A_Hoisted_Gantt3_Scrolls_Toward_The_Tasks_Own_Midpoint()
    {
        var state = new GanttState();
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture()));

        // t1: Jan 2 -> Jan 6, ColumnWidth=38 (Day mode default) — assert the
        // request actually moved (a real interop call landed), not merely that
        // SOME call count exists (that would pass even if the date resolution
        // were broken and it always targeted Today instead).
        var callsBefore = _interop.GanttV3ScrollToXCallCount;
        await cut.InvokeAsync(() => state.ScrollToTaskAsync("t1"));
        Assert.True(_interop.GanttV3ScrollToXCallCount > callsBefore);

        var xForT1 = _interop.GanttV3ScrollToXCalls[^1];

        callsBefore = _interop.GanttV3ScrollToXCallCount;
        await cut.InvokeAsync(() => state.ScrollToTaskAsync("t2"));
        Assert.True(_interop.GanttV3ScrollToXCallCount > callsBefore);
        var xForT2 = _interop.GanttV3ScrollToXCalls[^1];

        // t2 (Jan 10 -> Jan 20) is temporally LATER than t1 (Jan 2 -> Jan 6), so
        // its target x must be strictly further right — proves the scroll target
        // tracks the RESOLVED task, not a fixed/constant value.
        Assert.True(xForT2 > xForT1, $"expected t2's target ({xForT2}) to be right of t1's ({xForT1})");
    }

    [Fact]
    public async Task ScrollToTaskAsync_With_An_Unknown_Id_Issues_No_Interop_Call()
    {
        var state = new GanttState();
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture()));

        var callsBefore = _interop.GanttV3ScrollToXCallCount;
        await cut.InvokeAsync(() => state.ScrollToTaskAsync("does-not-exist"));

        Assert.Equal(callsBefore, _interop.GanttV3ScrollToXCallCount);
    }

    // ── Imperative SetViewMode routes through the CLAIM-based ownership model ──

    [Fact]
    public async Task SetViewMode_On_An_Uncontrolled_Hoisted_Gantt3_Updates_The_Rendered_Mode()
    {
        var state = new GanttState();
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Equal(L.GanttViewMode.Day, state.ViewMode);

        await cut.InvokeAsync(() => state.SetViewMode(L.GanttViewMode.Month));
        // SetViewMode is fire-and-forget on the STATE side, but its underlying
        // dispatch is InvokeAsync'd onto Gantt3's own sync context — awaiting
        // cut.InvokeAsync(...) around the CALL SITE (not a returned Task) still
        // pumps that same sync context to completion before returning, so the
        // commit is observable immediately after.

        Assert.Equal(L.GanttViewMode.Month, state.ViewMode);
        Assert.Contains("Month", cut.Markup);
    }

    [Fact]
    public async Task SetViewMode_On_A_Controlled_Hoisted_Gantt3_Echoes_Through_ViewModeChanged_Exactly_Like_A_Toolbar_Pick()
    {
        // Hard part 2: an imperative SetViewMode against a CONTROLLED chart must
        // ask the parent to ratify it — the SAME owesConsumerEcho path a toolbar
        // pick uses (GanttViewModeSource.Imperative, not a silent direct write).
        var state = new GanttState();
        L.GanttViewMode? notified = null;
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ViewModeChanged, (L.GanttViewMode m) => { notified = m; }));

        await cut.InvokeAsync(() => state.SetViewMode(L.GanttViewMode.Week));

        Assert.Equal(L.GanttViewMode.Week, notified);
        Assert.Equal(L.GanttViewMode.Week, state.ViewMode);
    }

    // ── No-attachment / post-disposal: documented no-op ─────────────────────

    [Fact]
    public async Task After_The_Attached_Gantt3_Is_Disposed_The_States_Imperative_Api_Is_A_Silent_No_Op()
    {
        var state = new GanttState();
        var cut = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture()));

        cut.Instance.Dispose(); // Gantt3's own IDisposable.Dispose() -> state.Detach(this)

        var callsBefore = _interop.GanttV3ScrollToXCallCount;
        var task = state.ScrollToDateAsync(D(2026, 1, 1));
        state.SetViewMode(L.GanttViewMode.Year);

        await task; // must complete (no-op), not hang or throw
        Assert.Equal(callsBefore, _interop.GanttV3ScrollToXCallCount);
    }

    // ── Two components sharing one state (REUI useGanttState analog) ───────────

    [Fact]
    public void A_Nav_Click_In_One_Gantt3_Instance_Updates_A_Sibling_Sharing_The_Same_State()
    {
        var state = new GanttState();
        var cutA = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture()));
        var cutB = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture()));

        var periodLabelBefore = ReadPeriodLabel(cutB);

        // A genuine UI interaction in instance A's OWN toolbar — not a raw
        // GanttState API call — proving the shared-state reactivity works for
        // ordinary internal navigation too, not just the new imperative surface.
        var nextButton = cutA.FindAll("button").First(b => (b.GetAttribute("aria-label") ?? "").Contains("ext", StringComparison.OrdinalIgnoreCase));
        nextButton.Click();

        var periodLabelAfterA = ReadPeriodLabel(cutA);
        var periodLabelAfterB = ReadPeriodLabel(cutB);

        Assert.NotEqual(periodLabelBefore, periodLabelAfterB);
        Assert.Equal(periodLabelAfterA, periodLabelAfterB);
    }

    [Fact]
    public async Task Imperative_SetViewMode_Against_Shared_State_Updates_Both_Sibling_Instances()
    {
        var state = new GanttState();
        var cutA = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));
        var cutB = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        await cutB.InvokeAsync(() => state.SetViewMode(L.GanttViewMode.Year));

        Assert.Equal(L.GanttViewMode.Year, state.ViewMode);
        Assert.Contains("Year", cutA.Markup);
        Assert.Contains("Year", cutB.Markup);
    }

    [Fact]
    public async Task The_More_Recently_Mounted_Instance_Is_The_Active_Imperative_Api_Target()
    {
        // "Second attach" policy (design spec Phase 3, T4, hard part 1): last
        // attacher wins. Proven here via two DIFFERENT ViewModeChanged callbacks —
        // only the one belonging to the LATER-mounted (currently attached)
        // instance must fire when the shared state's SetViewMode is invoked.
        var state = new GanttState();
        L.GanttViewMode? notifiedA = null, notifiedB = null;
        var cutA = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ViewModeChanged, (L.GanttViewMode m) => { notifiedA = m; }));
        var cutB = _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ViewModeChanged, (L.GanttViewMode m) => { notifiedB = m; }));

        await cutB.InvokeAsync(() => state.SetViewMode(L.GanttViewMode.Week));

        Assert.Null(notifiedA);
        Assert.Equal(L.GanttViewMode.Week, notifiedB);
    }

    private static string ReadPeriodLabel(IRenderedComponent<L.Gantt3> cut)
    {
        var spans = cut.FindAll("span.text-sm.font-medium.text-foreground");
        return spans.Count > 0 ? spans[0].TextContent : "";
    }
}
