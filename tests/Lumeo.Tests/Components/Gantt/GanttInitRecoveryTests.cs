using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Gantt;

/// <summary>
/// Regression for battle-test wave 1, finding #19 (lifecycle): a failed
/// <c>gantt.init</c> must NOT leave the component permanently dead. A later
/// parameter change (new <c>Tasks</c> / <c>ViewMode</c>) has to clear the latched
/// error and re-attempt init.
///
/// Mechanism of the bug: <c>OnAfterRenderAsync</c> was gated on
/// <c>if (!firstRender || _initialized) return;</c>, so init was only ever attempted
/// on the FIRST render. When <c>GanttInitAsync</c> threw a <c>JSException</c>,
/// <c>_initError</c> was set, <c>_initialized</c> stayed <c>false</c> and
/// <c>_instanceId</c> stayed <c>null</c>. Because the sole init path lived behind the
/// <c>firstRender</c> latch and <c>firstRender</c> is never true again,
/// <c>OnAfterRenderAsync</c> short-circuited forever — and
/// <c>OnParametersSetAsync</c>'s own <c>if (!_initialized || _instanceId is null) return;</c>
/// dropped every subsequent Tasks/ViewMode change. The component was bricked.
///
/// The fix drops the <c>firstRender</c> latch: init is attempted whenever there is no
/// live instance and no parked error (<c>_initialized || _instanceId is not null ||
/// _initError is not null</c> bails). A failed init still sets <c>_initError</c> to
/// stop a tight auto-retry loop, but <c>OnParametersSetAsync</c> clears that error on
/// the next genuine Tasks/ViewMode change, so the next render re-runs init.
///
/// Mirrors <see cref="Lumeo.Tests.Components.Chart.ChartLifecycleTests"/> (the
/// equivalent Chart #207 recovery test) and the Gantt module setup used by
/// <see cref="GanttTasksHashTests"/> / <see cref="GanttUncontrolledDragTests"/>: the
/// Gantt's own isolated JS module is pre-registered in Loose mode, but here
/// <c>gantt.init</c> is wired to THROW while a mutable <c>failInit</c> flag is set and
/// to return a valid instance id once it is cleared.
/// </summary>
public class GanttInitRecoveryTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Gantt/js/gantt-v2.js";
    private const string InstanceId = "gantt-instance-1";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();

        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private int InitCount() =>
        _module.Invocations.Count(i => i.Identifier == "gantt.init");

    private static L.GanttTask Task1 =>
        new("t1", "Design", new DateTime(2026, 1, 1), new DateTime(2026, 1, 5), 20);

    private static L.GanttTask Task2 =>
        new("t2", "Build", new DateTime(2026, 1, 6), new DateTime(2026, 1, 10), 0);

    [Fact]
    public void Gantt_recovers_and_re_inits_after_a_failed_init_when_tasks_change()
    {
        // Make the FIRST gantt.init throw (e.g. the SVG renderer module failed to
        // load); a later attempt, once failInit is cleared, returns a valid id.
        var failInit = true;
        _module
            .Setup<string>("gantt.init", _ => failInit)
            .SetException(new JSException("Frappe Gantt failed to load"));
        _module
            .Setup<string>("gantt.init", _ => !failInit)
            .SetResult(InstanceId);

        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1 }));

        // The error branch is showing and the host surface is gone.
        Assert.Contains("Gantt initialization failed", cut.Markup);
        Assert.DoesNotContain("lumeo-gantt-host", cut.Markup);
        var initsAfterFailure = InitCount();
        Assert.True(initsAfterFailure >= 1);

        // The consumer supplies a genuinely new Tasks payload. That parameter change
        // must clear the latched error, re-render the host, and re-attempt init.
        failInit = false;
        cut.Render(p => p
            .Add(c => c.Tasks, new[] { Task1, Task2 }));

        // Recovered: the error is cleared (the host surface is back) and init ran
        // again. Before the fix _initError + the firstRender latch kept the
        // component dead — no further gantt.init was ever issued.
        Assert.DoesNotContain("Gantt initialization failed", cut.Markup);
        Assert.Contains("lumeo-gantt-host", cut.Markup);
        Assert.True(
            InitCount() > initsAfterFailure,
            "A changed Tasks payload after an init failure must trigger a fresh gantt.init.");
    }

    [Fact]
    public void Gantt_recovers_on_a_ViewMode_change_after_a_failed_init()
    {
        var failInit = true;
        _module
            .Setup<string>("gantt.init", _ => failInit)
            .SetException(new JSException("boom"));
        _module
            .Setup<string>("gantt.init", _ => !failInit)
            .SetResult(InstanceId);

        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Contains("Gantt initialization failed", cut.Markup);
        var initsAfterFailure = InitCount();

        // A ViewMode change is also an init-relevant parameter change and must
        // clear the error and re-attempt init.
        failInit = false;
        cut.Render(p => p
            .Add(c => c.Tasks, new[] { Task1 })
            .Add(c => c.ViewMode, L.GanttViewMode.Week));

        Assert.DoesNotContain("Gantt initialization failed", cut.Markup);
        Assert.True(
            InitCount() > initsAfterFailure,
            "A ViewMode change after an init failure must trigger a fresh gantt.init.");
    }

    [Fact]
    public void A_still_failing_re_render_stays_safely_parked_on_the_error()
    {
        // gantt.init keeps throwing across renders (the underlying problem is not
        // fixed yet). Recovery must never turn into a hard fault or a tight retry
        // loop that bubbles the JSException out of Render.
        _module
            .Setup<string>("gantt.init", _ => true)
            .SetException(new JSException("boom"));

        var cut = _ctx.Render<L.Gantt>(p => p
            .Add(c => c.Tasks, new[] { Task1 }));

        Assert.Contains("Gantt initialization failed", cut.Markup);

        // A re-render while the world is still broken: the component re-attempts
        // init, it fails again, and the error stays shown — but Render itself must
        // not throw (the JSException is caught and re-parks into _initError).
        var ex = Record.Exception(() => cut.Render(p => p
            .Add(c => c.Tasks, new[] { Task1, Task2 })));

        Assert.Null(ex);
        Assert.Contains("Gantt initialization failed", cut.Markup);
        Assert.DoesNotContain("lumeo-gantt-host", cut.Markup);
    }
}
