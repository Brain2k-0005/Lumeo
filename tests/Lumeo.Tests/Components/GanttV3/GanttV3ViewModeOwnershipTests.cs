using System.Linq;
using System.Reflection;
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
/// Gantt3's view-mode OWNERSHIP model (Codex PR-382 review round 3, P1
/// "Keep the toolbar mode pending through the callback").
///
/// Seven consecutive findings across three review rounds all landed on one
/// surface — "which intent currently owns the view mode, and for how long" —
/// because that answer was spread over four independently-maintained fields
/// (_pendingToolbarMode / _committedViewModeParam / _lastSeenViewModeParam plus
/// the reconcile generation) whose LIFETIMES were hand-written at each call
/// site. Each fix corrected one lifetime and exposed the next.
///
/// All four are now one value — <see cref="GanttViewModeIntent"/> — with one
/// claim point, one replay rule and one settle point. These tests pin the
/// model's invariants rather than a list of symptoms:
///
///  * A pending intent survives an arbitrarily long consumer callback, so a
///    navigation landing in that window replays it instead of resurrecting the
///    parent's not-yet-updated ViewMode parameter (the round-3 finding itself,
///    on BOTH paths that clear it — the nav re-apply AND the toolbar's own
///    direct commit, which had the identical shape and was never flagged).
///  * A newer intent of ANY source cleanly supersedes an older one, and the
///    older one's outstanding callback can never clobber the newer.
///  * A parameter value a competing trigger deliberately overrode is never
///    resurrected — "has the parameter been accounted for" is a property of the
///    owning intent, not a field anyone has to remember to advance.
///  * Once everything quiesces, exactly one intent is settled and its mode IS
///    the committed GanttState.ViewMode — no intent is ever left parked.
/// </summary>
public class GanttV3ViewModeOwnershipTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3ViewModeOwnershipTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // Forces a repaint via the PROTECTED ComponentBase.StateHasChanged, NOT
    // bUnit's parameterless cut.Render() — see GanttV3CodexRound20Tests' own
    // remarks on the identical helper: that overload re-issues SetParametersAsync,
    // which re-enters the parameter lifecycle and can silently self-heal exactly
    // the ownership bugs these tests are about, making them falsely pass.
    private static async Task ForceRepaintAsync(IRenderedComponent<L.Gantt3> cut)
    {
        var stateHasChanged = typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(() => stateHasChanged.Invoke(cut.Instance, null));
    }

    private static MethodInfo Method(string name) =>
        typeof(L.Gantt3).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static GanttViewModeIntent Intent(IRenderedComponent<L.Gantt3> cut) =>
        (GanttViewModeIntent)typeof(L.Gantt3)
            .GetField("_viewModeIntent", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(cut.Instance)!;

    private static GanttState State(IRenderedComponent<L.Gantt3> cut) =>
        (GanttState)typeof(L.Gantt3)
            .GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(cut.Instance)!;

    private static string Label(IRenderedComponent<L.Gantt3> cut) =>
        cut.Find("span.text-sm.font-medium").TextContent;

    // Month's PeriodLabel is a single "MMMM yyyy"; Day/Week's is a two-date
    // "… – …" range and Year's is "{y1}–{y2}" — so "no en dash" is a
    // format-shape check for Month that never needs the exact captured center.
    private static void AssertMonthMode(IRenderedComponent<L.Gantt3> cut, string because)
    {
        var label = Label(cut);
        Assert.False(label.Contains('–', StringComparison.Ordinal),
            $"expected Month's \"MMMM yyyy\" label, got \"{label}\" — {because}");
    }

    private static List<L.GanttTask> OneTask() =>
        new() { new L.GanttTask("t1", "Task", D(2026, 1, 10), D(2026, 1, 20)) };

    // Renders a CONTROLLED Gantt3 whose consumer handler records its calls and
    // then blocks on <paramref name="gate"/> — i.e. a parent whose own
    // ViewModeChanged handler is itself async and has NOT yet assigned ViewMode.
    // That window is the whole subject of this file: throughout it, the ViewMode
    // parameter still reads as the PRE-pick mode.
    private IRenderedComponent<L.Gantt3> RenderControlled(
        List<L.GanttTask> tasks, TaskCompletionSource gate, List<L.GanttViewMode> calls) =>
        _ctx.Render<L.Gantt3>(p => p
            .Add(c => c.Tasks, tasks)
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ViewModeChanged, EventCallback.Factory.Create<L.GanttViewMode>(this, async mode =>
            {
                calls.Add(mode);
                await gate.Task;
            }))
            .Add(c => c.ShowTreePane, false));

    // ── The round-3 finding: a navigation during a controlled callback ──────
    //
    // Path A — the flagged one (Gantt3.razor:518): the toolbar's own reconcile
    // is suspended, so a FIRST navigation supersedes and REPLAYS it, commits,
    // and then yields on the consumer echo. A SECOND navigation landing in that
    // yield used to find the pending pick already cleared and fall back to the
    // parent's stale ViewMode parameter as authoritative — reverting the zoom
    // and reshaping the range under the wrong unit.

    [Theory]
    [InlineData("ShiftToNextAsync")]
    [InlineData("ShiftToPreviousAsync")]
    [InlineData("GoToTodayAsync")]
    public async Task A_Navigation_During_A_Replayed_Toolbar_Picks_Callback_Does_Not_Revert_It(string navMethod)
    {
        var tasks = OneTask();
        var callbackGate = new TaskCompletionSource();
        var calls = new List<L.GanttViewMode>();
        var cut = RenderControlled(tasks, callbackGate, calls);
        var nav = Method(navMethod);

        // Toolbar picks Month; suspend its own live-center capture so the pick
        // is still PENDING when the first navigation arrives.
        var toolbarGate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = toolbarGate;
        Task toolbarReconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            toolbarReconcile = (Task)Method("HandleViewModeChangedAsync").Invoke(cut.Instance, new object[] { L.GanttViewMode.Month })!;
        });
        Assert.False(toolbarReconcile.IsCompleted, "the toolbar's own reconcile should still be awaiting its capture");

        // Navigation #1 supersedes it, replays Month, commits — then yields on
        // the consumer's own async handler.
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;
        Task firstNav = Task.CompletedTask;
        await cut.InvokeAsync(() => { firstNav = (Task)nav.Invoke(cut.Instance, null)!; });
        Assert.Equal(new[] { L.GanttViewMode.Month }, calls); // the replay committed and echoed
        Assert.False(firstNav.IsCompleted, "the navigation should still be awaiting the consumer's mode callback");

        // Navigation #2 lands WHILE that callback is outstanding — the parent's
        // ViewMode parameter still reads Day at this point.
        await cut.InvokeAsync(async () => await (Task)nav.Invoke(cut.Instance, null)!);

        toolbarGate.SetResult(0);
        await toolbarReconcile; // the long-superseded original pass abandons cleanly
        await ForceRepaintAsync(cut);

        AssertMonthMode(cut, "a navigation during the consumer callback resurrected the parent's stale ViewMode parameter");
        Assert.Single(calls); // and it did not re-notify the consumer either

        callbackGate.SetResult();
        await firstNav;
    }

    // Path B — the adjacent case on the SAME surface that the finding did not
    // name (the toolbar's own direct commit cleared the pending pick before its
    // echo in exactly the same way). No supersession is involved at all here:
    // the toolbar commits immediately and only then yields on the consumer.

    [Theory]
    [InlineData("ShiftToNextAsync")]
    [InlineData("GoToTodayAsync")]
    public async Task A_Navigation_During_A_Direct_Toolbar_Picks_Callback_Does_Not_Revert_It(string navMethod)
    {
        var tasks = OneTask();
        var callbackGate = new TaskCompletionSource();
        var calls = new List<L.GanttViewMode>();
        var cut = RenderControlled(tasks, callbackGate, calls);

        _interop.GanttV3ScrollCenterXToReturn = 0;

        // The toolbar's own reconcile commits straight away; only the consumer
        // echo yields.
        Task toolbarPick = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            toolbarPick = (Task)Method("HandleViewModeChangedAsync").Invoke(cut.Instance, new object[] { L.GanttViewMode.Month })!;
        });
        Assert.Equal(new[] { L.GanttViewMode.Month }, calls);
        Assert.False(toolbarPick.IsCompleted, "the toolbar pick should still be awaiting the consumer's mode callback");

        await cut.InvokeAsync(async () => await (Task)Method(navMethod).Invoke(cut.Instance, null)!);
        await ForceRepaintAsync(cut);

        AssertMonthMode(cut, "a navigation during the toolbar's own consumer callback resurrected the stale ViewMode parameter");
        Assert.Single(calls);

        callbackGate.SetResult();
        await toolbarPick;
    }

    // ── A pending intent survives an ARBITRARILY long callback ──────────────

    [Fact]
    public async Task A_Pending_Toolbar_Intent_Survives_An_Arbitrarily_Long_Consumer_Callback()
    {
        var tasks = OneTask();
        var callbackGate = new TaskCompletionSource();
        var calls = new List<L.GanttViewMode>();
        var cut = RenderControlled(tasks, callbackGate, calls);

        _interop.GanttV3ScrollCenterXToReturn = 0;
        Task toolbarPick = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            toolbarPick = (Task)Method("HandleViewModeChangedAsync").Invoke(cut.Instance, new object[] { L.GanttViewMode.Month })!;
        });
        Assert.False(toolbarPick.IsCompleted);

        // Everything that can happen while a slow parent handler is outstanding:
        // repeated navigations, an unrelated parent re-render that re-pushes the
        // NOT-yet-updated ViewMode, and a no-op theme notification.
        for (var i = 0; i < 3; i++)
        {
            await cut.InvokeAsync(async () => await (Task)Method("ShiftToNextAsync").Invoke(cut.Instance, null)!);
            await cut.InvokeAsync(async () => await (Task)Method("ShiftToPreviousAsync").Invoke(cut.Instance, null)!);
        }

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(L.Gantt3.Tasks)] = tasks,
            [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Day, // the parent's handler has NOT assigned yet
            [nameof(L.Gantt3.ViewModeChanged)] = EventCallback.Factory.Create<L.GanttViewMode>(this, async mode =>
            {
                calls.Add(mode);
                await callbackGate.Task;
            }),
            [nameof(L.Gantt3.ShowTreePane)] = false,
            [nameof(L.Gantt3.Class)] = "an-unrelated-class-change",
        })));

        var themeService = _ctx.Services.GetRequiredService<IThemeService>();
        await cut.InvokeAsync(async () => await themeService.SetModeAsync(ThemeMode.Dark)); // direction untouched
        await cut.InvokeAsync(() => { });

        await ForceRepaintAsync(cut);
        AssertMonthMode(cut, "the pending toolbar intent did not survive the consumer callback");
        Assert.Single(calls);

        // Once the parent finally comes back, the intent settles — and stays
        // settled on the mode it actually applied.
        callbackGate.SetResult();
        await toolbarPick;
        Assert.Equal(GanttViewModePhase.Settled, Intent(cut).Phase);
        Assert.Equal(L.GanttViewMode.Month, Intent(cut).Mode);
        Assert.Equal(L.GanttViewMode.Month, State(cut).ViewMode);
    }

    // ── Supersession by a newer intent of each source ───────────────────────

    [Fact]
    public async Task A_Newer_Toolbar_Pick_Supersedes_An_Older_One_Whose_Callback_Is_Still_Outstanding()
    {
        var tasks = OneTask();
        var firstGate = new TaskCompletionSource();
        var calls = new List<L.GanttViewMode>();
        var cut = RenderControlled(tasks, firstGate, calls);

        _interop.GanttV3ScrollCenterXToReturn = 0;
        Task firstPick = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            firstPick = (Task)Method("HandleViewModeChangedAsync").Invoke(cut.Instance, new object[] { L.GanttViewMode.Month })!;
        });
        Assert.False(firstPick.IsCompleted);
        var monthIntentId = Intent(cut).Id;

        // A NEWER toolbar pick while the first one's echo is still outstanding.
        // Its own echo re-enters the same (still-blocked) handler, so it too
        // stays outstanding — but it must have taken ownership outright.
        Task secondPick = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            secondPick = (Task)Method("HandleViewModeChangedAsync").Invoke(cut.Instance, new object[] { L.GanttViewMode.Year })!;
        });
        Assert.Equal(new[] { L.GanttViewMode.Month, L.GanttViewMode.Year }, calls);
        Assert.NotEqual(monthIntentId, Intent(cut).Id);
        Assert.Equal(L.GanttViewMode.Year, Intent(cut).Mode);
        Assert.Equal(L.GanttViewMode.Year, State(cut).ViewMode);

        // Release both handlers. The OLDER pick's continuation must not settle,
        // clear or clobber the newer intent it no longer owns.
        firstGate.SetResult();
        await firstPick;
        await secondPick;

        Assert.Equal(L.GanttViewMode.Year, Intent(cut).Mode);
        Assert.Equal(L.GanttViewMode.Year, State(cut).ViewMode);
        Assert.Equal(GanttViewModePhase.Settled, Intent(cut).Phase);

        await ForceRepaintAsync(cut);
        var label = Label(cut);
        Assert.False(label.Any(char.IsLetter),
            $"expected Year's letter-free \"{{y1}}–{{y2}}\" label, got \"{label}\" — the older pick's callback clobbered the newer one");
    }

    [Fact]
    public async Task A_Newer_Parameter_Push_Supersedes_A_Toolbar_Pick_Whose_Callback_Is_Still_Outstanding()
    {
        var tasks = OneTask();
        var callbackGate = new TaskCompletionSource();
        var calls = new List<L.GanttViewMode>();
        var cut = RenderControlled(tasks, callbackGate, calls);

        _interop.GanttV3ScrollCenterXToReturn = 0;
        Task toolbarPick = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            toolbarPick = (Task)Method("HandleViewModeChangedAsync").Invoke(cut.Instance, new object[] { L.GanttViewMode.Month })!;
        });
        Assert.False(toolbarPick.IsCompleted);

        // The parent decides something ELSE entirely while the echo is still
        // outstanding — a genuine parameter change, which legitimately takes
        // ownership from the pending pick (round 16's finding #2 contract).
        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(L.Gantt3.Tasks)] = tasks,
            [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Year,
            [nameof(L.Gantt3.ViewModeChanged)] = EventCallback.Factory.Create<L.GanttViewMode>(this, m => calls.Add(m)),
            [nameof(L.Gantt3.ShowTreePane)] = false,
        })));

        Assert.Equal(L.GanttViewMode.Year, State(cut).ViewMode);
        Assert.Equal(GanttViewModeSource.Parameter, Intent(cut).Source);

        callbackGate.SetResult();
        await toolbarPick;

        // Navigating afterwards must keep the parent's Year, not resurrect the
        // superseded pick.
        await cut.InvokeAsync(async () => await (Task)Method("ShiftToNextAsync").Invoke(cut.Instance, null)!);
        Assert.Equal(L.GanttViewMode.Year, State(cut).ViewMode);
        Assert.Single(calls); // only the toolbar's own echo; a parameter push never echoes back
    }

    // ── No resurrection of a parameter a competing trigger overrode ─────────

    [Fact]
    public async Task A_Parameter_Superseded_By_A_Toolbar_Pick_Is_Never_Resurrected_By_Later_Navigations()
    {
        var tasks = OneTask();
        var callbackGate = new TaskCompletionSource();
        var calls = new List<L.GanttViewMode>();
        var cut = RenderControlled(tasks, callbackGate, calls);

        // A parent Day -> Month push, suspended mid-capture.
        var paramGate = new TaskCompletionSource<double?>();
        _interop.GanttV3ScrollCenterXGate = paramGate;
        Task paramReconcile = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            paramReconcile = cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(L.Gantt3.Tasks)] = tasks,
                [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Month,
                [nameof(L.Gantt3.ViewModeChanged)] = EventCallback.Factory.Create<L.GanttViewMode>(this, async mode =>
                {
                    calls.Add(mode);
                    await callbackGate.Task;
                }),
                [nameof(L.Gantt3.ShowTreePane)] = false,
            }));
        });
        Assert.False(paramReconcile.IsCompleted);

        // The user picks Year from the toolbar instead — that pick supersedes
        // and commits over the suspended parameter push.
        _interop.GanttV3ScrollCenterXGate = null;
        _interop.GanttV3ScrollCenterXToReturn = 0;
        Task toolbarPick = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            toolbarPick = (Task)Method("HandleViewModeChangedAsync").Invoke(cut.Instance, new object[] { L.GanttViewMode.Year })!;
        });
        Assert.Equal(L.GanttViewMode.Year, State(cut).ViewMode);

        paramGate.SetResult(0);
        await paramReconcile; // the superseded push abandons cleanly

        // Navigate repeatedly, both during and after the consumer callback. The
        // parent's abandoned "Month" must never come back.
        await cut.InvokeAsync(async () => await (Task)Method("ShiftToNextAsync").Invoke(cut.Instance, null)!);
        Assert.Equal(L.GanttViewMode.Year, State(cut).ViewMode);

        callbackGate.SetResult();
        await toolbarPick;

        await cut.InvokeAsync(async () => await (Task)Method("ShiftToPreviousAsync").Invoke(cut.Instance, null)!);
        await cut.InvokeAsync(async () => await (Task)Method("GoToTodayAsync").Invoke(cut.Instance, null)!);

        Assert.Equal(L.GanttViewMode.Year, State(cut).ViewMode);
        await ForceRepaintAsync(cut);
        var label = Label(cut);
        Assert.False(label.Any(char.IsLetter),
            $"expected Year's letter-free \"{{y1}}–{{y2}}\" label, got \"{label}\" — the superseded parameter's Month value was resurrected");
    }

    // ── The model's own closing invariant ───────────────────────────────────

    [Fact]
    public async Task Once_Everything_Quiesces_Exactly_One_Settled_Intent_Matches_The_Committed_Mode()
    {
        var tasks = OneTask();
        var callbackGate = new TaskCompletionSource();
        var calls = new List<L.GanttViewMode>();
        var cut = RenderControlled(tasks, callbackGate, calls);

        // A settled intent's Mode is the committed mode — at mount, and after
        // every kind of trigger has had its turn.
        Assert.Equal(GanttViewModePhase.Settled, Intent(cut).Phase);
        Assert.Equal(State(cut).ViewMode, Intent(cut).Mode);
        Assert.Equal(GanttViewModeSource.Mount, Intent(cut).Source);

        _interop.GanttV3ScrollCenterXToReturn = 0;
        Task toolbarPick = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            toolbarPick = (Task)Method("HandleViewModeChangedAsync").Invoke(cut.Instance, new object[] { L.GanttViewMode.Month })!;
        });

        // While the consumer is still deciding, the intent is deliberately NOT
        // settled — that is exactly what a replay keys off.
        Assert.Equal(GanttViewModePhase.Notifying, Intent(cut).Phase);
        Assert.True(Intent(cut).IsPending);
        Assert.Equal(GanttViewModeSource.Toolbar, Intent(cut).Source);

        callbackGate.SetResult();
        await toolbarPick;

        Assert.Equal(GanttViewModePhase.Settled, Intent(cut).Phase);
        Assert.Equal(State(cut).ViewMode, Intent(cut).Mode);

        // A navigation never authors an intent — it replays the settled one, so
        // neither the identity nor the mode moves.
        var idBeforeNav = Intent(cut).Id;
        await cut.InvokeAsync(async () => await (Task)Method("ShiftToNextAsync").Invoke(cut.Instance, null)!);
        Assert.Equal(idBeforeNav, Intent(cut).Id);
        Assert.Equal(GanttViewModePhase.Settled, Intent(cut).Phase);
        Assert.Equal(State(cut).ViewMode, Intent(cut).Mode);

        // A parameter pass that says nothing new authors nothing either.
        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(L.Gantt3.Tasks)] = tasks,
            [nameof(L.Gantt3.ViewMode)] = L.GanttViewMode.Month, // what the settled intent told the parent to hold
            [nameof(L.Gantt3.ViewModeChanged)] = EventCallback.Factory.Create<L.GanttViewMode>(this, m => calls.Add(m)),
            [nameof(L.Gantt3.ShowTreePane)] = false,
        })));
        Assert.Equal(idBeforeNav, Intent(cut).Id);
        Assert.Equal(L.GanttViewMode.Month, State(cut).ViewMode);
    }
}
