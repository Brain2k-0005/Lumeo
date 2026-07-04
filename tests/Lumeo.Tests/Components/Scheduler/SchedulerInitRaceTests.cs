using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// Regression for battle-test wave 1, finding #20 (state-on-data-change): an Events
/// refresh from the parent must NOT be silently dropped while the calendar is still
/// initializing.
///
/// Mechanism of the bug: <c>scheduler.init</c> is an async JS handshake. While it is
/// in flight <c>_initialized</c> is still <c>false</c>, so the
/// <c>OnParametersSetAsync</c> guard <c>if (_initialized &amp;&amp; Events is not null)</c>
/// skips updating <c>_events</c>/<c>_lastEventsHash</c> entirely. The init options were
/// captured from the ORIGINAL snapshot, so a parent that swaps Events mid-init has its
/// new event list silently lost — init renders the stale set and nothing repushes it.
///
/// The fix re-reads <c>Events</c> immediately after <c>_initialized = true</c> and, when
/// the hash differs from the snapshot init actually rendered, pushes the current set via
/// <c>scheduler.setEvents</c>. This is timing-independent: it reconciles regardless of
/// when during the async handshake the parameter change landed.
///
/// Mirrors <see cref="SchedulerEventsHashTests"/> / <see cref="GanttUncontrolledDragTests"/>:
/// the Scheduler's own isolated module is pre-registered in Loose mode. Here, crucially,
/// <c>scheduler.init</c> is left PENDING (no SetResult) so we can swap Events while
/// <c>_initialized</c> is still false, then complete init and assert the reconciliation.
/// </summary>
public class SchedulerInitRaceTests : IAsyncLifetime
{
    private const string ModulePath = "./_content/Lumeo.Scheduler/js/scheduler.js";
    private const string InstanceId = "sched-instance-1";

    private readonly BunitContext _ctx = new();
    private BunitJSModuleInterop _module = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();

        _module = _ctx.JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;

        _module.Setup<string>("scheduler.getTitle", _ => true).SetResult("June 2026");

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static int SetEventsCount(BunitJSModuleInterop module) =>
        module.Invocations.Count(i => i.Identifier == "scheduler.setEvents");

    private static L.SchedulerEvent Event(string id, string title) =>
        new(id, title, DateTime.Today.AddHours(10), DateTime.Today.AddHours(11));

    [Fact]
    public async Task Events_swapped_while_init_is_in_flight_are_pushed_after_init_completes()
    {
        // Leave scheduler.init PENDING: holding the handler without SetResult keeps the
        // OnAfterRenderAsync await suspended, so _initialized stays false — exactly the
        // window in which OnParametersSetAsync drops an Events refresh.
        var initHandler = _module.Setup<string>("scheduler.init", _ => true);

        var original = new[] { Event("e1", "Team Meeting") };
        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, original));

        // Init has not resolved yet, so no setEvents has fired and _initialized is false.
        Assert.Equal(0, SetEventsCount(_module));

        // The parent swaps in a fresh, larger list WHILE init is still mid-handshake.
        // OnParametersSetAsync runs with _initialized == false and (before the fix) drops
        // this refresh on the floor.
        var refreshed = new[] { Event("e1", "Team Meeting"), Event("e2", "1:1 Review") };
        cut.Render(p => p.Add(c => c.Events, refreshed));

        // Still no setEvents — the refresh has not reached JS yet.
        Assert.Equal(0, SetEventsCount(_module));

        // Now the JS init handshake completes. Post-init reconciliation must notice the
        // mid-init swap and push the current (2-event) set. The push runs in the
        // OnAfterRenderAsync continuation that resumes AFTER init's await — that
        // continuation is scheduled on the dispatcher and is not awaited by the
        // SetResult InvokeAsync, so poll for it rather than asserting synchronously.
        await cut.InvokeAsync(() => initHandler.SetResult(InstanceId));

        // The reconciliation push AND the trailing scheduler.getTitle read both run in
        // the OnAfterRenderAsync continuation that resumes AFTER init's await — SetResult's
        // InvokeAsync does not await it. getTitle is that continuation's FINAL interop call
        // (it fires strictly after the setEvents push), so waiting for it guarantees the push
        // has already been recorded AND that nothing else is still mutating the (non-thread-
        // safe) invocation dictionary before we enumerate it below. Reading Invocations under
        // WaitForAssertion also retries past any transient concurrent enumeration.
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(_module.Invocations, i => i.Identifier == "scheduler.getTitle");
            Assert.True(
                SetEventsCount(_module) > 0,
                "An Events refresh that arrives while the calendar is still initializing must " +
                "be pushed via scheduler.setEvents once init completes, not silently dropped.");
        });

        // The pushed payload must carry the REFRESHED list (2 events), not the stale one.
        var setEvents = _module.Invocations.Last(i => i.Identifier == "scheduler.setEvents");
        var serialized = setEvents.Arguments[1]!;
        var array = Assert.IsAssignableFrom<System.Collections.IEnumerable>(serialized);
        Assert.Equal(2, array.Cast<object>().Count());
    }

    [Fact]
    public async Task Unchanged_events_across_init_do_not_trigger_a_redundant_push()
    {
        // No mid-init swap: the same Events snapshot init rendered is still current when
        // init completes, so the reconciliation must NOT fire a needless setEvents.
        var initHandler = _module.Setup<string>("scheduler.init", _ => true);

        var events = new[] { Event("e1", "Team Meeting") };
        var cut = _ctx.Render<L.Scheduler>(p => p.Add(c => c.Events, events));

        // Complete init without any intervening param change.
        await cut.InvokeAsync(() => initHandler.SetResult(InstanceId));

        // Post-init reconciliation + the scheduler.getTitle read run in the
        // OnAfterRenderAsync continuation that resumes AFTER init's await; SetResult's
        // InvokeAsync does not await it. Asserting SetEventsCount synchronously here
        // enumerates bUnit's non-thread-safe invocation dictionary WHILE that continuation
        // is still recording getTitle — the source of the historic "Collection was modified"
        // flake. Wait for the continuation to finish (getTitle is its final interop call, and
        // it fires whether or not a redundant push happened) before asserting no setEvents
        // was pushed; read under WaitForAssertion so any transient concurrent enumeration is
        // retried rather than surfacing as an exception.
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(_module.Invocations, i => i.Identifier == "scheduler.getTitle");
            Assert.Equal(0, SetEventsCount(_module));
        });
    }
}
