using System.Globalization;
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

    /// <summary>
    /// Codex review finding #2 (P2), the locale analogue of the Events race above:
    /// <c>scheduler.init</c> is an async JS handshake. While it is in flight
    /// <c>_initialized</c> is still <c>false</c>, so OnParametersSetAsync's
    /// culture-change branch records the new <c>_currentLocale</c> (it isn't gated on
    /// <c>_initialized</c>) but SKIPS the <c>scheduler.setLocale</c> push (that IS
    /// gated on <c>_initialized</c>). Init's OWN options were captured from the
    /// culture in effect BEFORE the change, so the calendar is created with the STALE
    /// locale — and because every later render sees <c>_currentLocale</c> already
    /// "up to date", nothing ever re-pushes it again. The calendar would be stuck on
    /// the old locale for the rest of its life.
    ///
    /// Fixed the same way as the Events race: snapshot the locale init is about to
    /// render, and after <c>_initialized = true</c>, reconcile against the (possibly
    /// now different) <c>_currentLocale</c> and push it if it changed.
    ///
    /// Per the review's rigor standard this drives the ACTUAL race — the culture
    /// changes WHILE init is still pending, not after it resolves (a post-init change
    /// would already be covered by <see cref="SchedulerLocaleTests"/> and would pass
    /// even on the broken code, proving nothing about this bug).
    /// </summary>
    [Fact]
    public async Task Culture_changed_while_init_is_in_flight_is_not_lost()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            // Leave scheduler.init PENDING — same technique as the Events race tests
            // above — so _initialized stays false while we flip the culture.
            var initHandler = _module.Setup<string>("scheduler.init", _ => true);

            var cut = _ctx.Render<L.Scheduler>();

            Assert.Equal(0, _module.Invocations.Count(i => i.Identifier == "scheduler.setLocale"));

            // The culture flips WHILE init is still mid-handshake — the actual race,
            // not a change applied after init has already resolved.
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            cut.Render(p => p.Add(c => c.Height, "640px"));

            // Still nothing pushed to JS — OnParametersSetAsync recorded the change in
            // _currentLocale but _initialized is still false, so the interop push was
            // skipped (this is the bug's own mechanism, not yet its consequence).
            Assert.Equal(0, _module.Invocations.Count(i => i.Identifier == "scheduler.setLocale"));

            // Now complete the JS handshake.
            await cut.InvokeAsync(() => initHandler.SetResult(InstanceId));

            // Post-init reconciliation and the trailing scheduler.getTitle read both run
            // in the OnAfterRenderAsync continuation that resumes AFTER init's await;
            // SetResult's InvokeAsync does not await it. getTitle is that continuation's
            // FINAL interop call, so waiting for it (as the Events race tests above do)
            // guarantees the reconciliation has already run before we assert.
            cut.WaitForAssertion(() =>
            {
                Assert.Contains(_module.Invocations, i => i.Identifier == "scheduler.getTitle");

                // Init itself must have captured the STALE locale ("en") — confirms this
                // test actually drove the race (a change applied only after init
                // resolves would never exercise this path).
                var init = Assert.Single(_module.Invocations, i => i.Identifier == "scheduler.init");
                var options = (System.Collections.Generic.IDictionary<string, object?>)init.Arguments[2]!;
                Assert.Equal("en", options["locale"]);

                // Predicted WRONG behavior on the broken code: 0 scheduler.setLocale
                // invocations, ever — the mid-init culture change is recorded in the
                // field but nothing reconciles it once init completes, so the live
                // calendar stays on "en" forever despite the culture now being de-DE.
                var setLocale = Assert.Single(_module.Invocations, i => i.Identifier == "scheduler.setLocale");
                Assert.Equal(InstanceId, setLocale.Arguments[0]);
                Assert.Equal("de", setLocale.Arguments[1]);
            });
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}
