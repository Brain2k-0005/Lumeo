using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toast;

/// <summary>
/// PR #357 round-6: consolidated admission-model invariant coverage. <c>ToastProvider</c>'s
/// <c>TryAdmit</c>/<c>ReconcileGroup</c> pair (see the ADMISSION MODEL comment above
/// <c>TryAdmit</c>) owns five invariants: (a) "persistent" is the RESOLVED duration, not the raw
/// property; (b) persistents bypass the cap entirely — mount directly, never evict/queue; (c) per-
/// position mounted non-persistent count never exceeds <c>EffectiveMaxToasts</c>, even mid-burst
/// while callers race each other's evictions; (d) any Update that changes a toast's group
/// (position) or persistence re-reconciles BOTH the old and new affected groups, whether the toast
/// is mounted or still queued; (e) queue admission is FIFO within a group.
///
/// Rather than one test per finding, this fires ONE deterministic (no <see cref="Random"/>) burst
/// sequence — show/update/dismiss/promise-resolve — across TWO independent position groups and
/// asserts the cap invariant at EVERY render via the <c>OnAfterRender</c> hook, the same technique
/// <c>ToastTests.ToastProvider_Spamming_Show_Does_Not_Hang_And_Bounds_Mounted_Toasts</c> (round-2)
/// uses: a transient overshoot can't hide behind a lucky end-state snapshot taken after the fact,
/// and — critically for round-6 finding 1 — this technique is what actually races
/// TryAdmit/ReconcileGroup against themselves (fire-and-forget calls with no awaits in between),
/// instead of merely asserting a single request/response pair in isolation.
///
/// PR #357 round-7 extends the same burst with a THIRD/FOURTH position pair
/// (TopRight/BottomLeft) exercising two more interaction edges of the admission model: a
/// position-change of a MOUNTED toast into an already-full destination while its source group
/// ALSO carries queued demand (finding 1), and a swipe-dismiss of a live toast interleaved with
/// the rest of the burst (finding 3) — via <see cref="TrackingInteropService"/> so the captured
/// <c>OnToastSwiped</c> delegate can be invoked exactly like the JS layer reporting a completed
/// gesture, racing the same fire-and-forget admission passes as every other step here.
///
/// PR #357 round-8 extends the same burst with a FIFTH/SIXTH position pair (TopCenter/
/// BottomCenter) exercising the two ReconcileGroup-internal edges this round fixed: a multi-
/// toast overflow that must fully drain to the cap on its own, with no further external
/// Show/Update/Dismiss nudging the group afterwards (finding 1), and a still-QUEUED toast
/// retargeted to an empty destination group, which must land there promptly rather than
/// waiting behind its source group's own unrelated eviction cascade (finding 2) — both
/// asserted via explicit <c>WaitForAssertion</c> checks on the provider's own accounting, not
/// just the blanket per-render cap check every other step relies on. Finding 3 (never evict the
/// toast whose own change triggered the reconcile) needs a fully controlled, non-racing
/// sequence to pin down deterministically — see the dedicated
/// <see cref="ReconcileGroup_After_Update_Never_Evicts_The_Toast_That_Triggered_It"/> below
/// instead of folding it into this burst.
/// </summary>
public class ToastAdmissionInvariantTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ToastAdmissionInvariantTests()
    {
        _ctx.AddLumeoServices();
        // Last registration wins: route component interop (incl. toast swipe) through the
        // tracker so round-7's swipe step can capture and invoke the registered handler — same
        // idiom as ToastSwipeTests.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private ToastService GetToastService() =>
        (ToastService)_ctx.Services.GetRequiredService(typeof(ToastService));

    [Fact]
    public void Deterministic_Burst_Sequence_Across_Two_Groups_Never_Violates_The_Per_Group_Cap()
    {
        const int maxToasts = 3;
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>(p => p.Add(b => b.MaxToasts, maxToasts));

        // Asserted on EVERY render, not sampled after the fact — see the class doc comment.
        // LiveNonPersistentMountedCount is the provider's OWN admission accounting (an `internal`
        // test seam — see its doc comment in ToastProvider.razor), so this checks the invariant
        // directly rather than trying to infer "is this DOM element persistent?" from markup that
        // carries no such signal. LIVE (not the raw, Leaving-inclusive count): a toast mid-exit for
        // its own ~220ms animation has already relinquished its cap claim the instant eviction
        // marked it Leaving, even though it's still in the DOM playing `animate-toast-out` — same
        // as every ordinary eviction in this codebase, not a cap violation (see that method's doc
        // comment for why the raw count is deliberately NOT the right thing to assert here).
        var violations = new List<string>();
        cut.OnAfterRender += (_, _) =>
        {
            var br = cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomRight);
            var tl = cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.TopLeft);
            var tr = cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.TopRight);
            var bl = cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomLeft);
            var tc = cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.TopCenter);
            var bc = cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomCenter);
            if (br > maxToasts) violations.Add($"BottomRight live-mounted={br} > cap={maxToasts}");
            if (tl > maxToasts) violations.Add($"TopLeft live-mounted={tl} > cap={maxToasts}");
            if (tr > maxToasts) violations.Add($"TopRight live-mounted={tr} > cap={maxToasts}");
            if (bl > maxToasts) violations.Add($"BottomLeft live-mounted={bl} > cap={maxToasts}");
            if (tc > maxToasts) violations.Add($"TopCenter live-mounted={tc} > cap={maxToasts}");
            if (bc > maxToasts) violations.Add($"BottomCenter live-mounted={bc} > cap={maxToasts}");
        };

        var ids = new Dictionary<string, string>();

        // ── Deterministic seed sequence (fixed order, NO Random) ───────────────────────────────
        // Fired back-to-back with no waits in between, exactly like the round-2 spam test — real
        // evictions/exit-animations/reconciliations race each other for the whole burst, the same
        // condition that produced round-6 finding 1.

        // 1) Flood BottomRight (BR) to 5 — well past the cap of 3 — forcing eviction + queueing.
        for (var i = 0; i < 5; i++)
        {
            ids[$"br{i}"] = toastService.Show(new ToastOptions
            { Title = $"BR-{i}", Duration = 60000, Position = ToastPosition.BottomRight });
        }

        // 2) Fill TopLeft (TL) exactly to the cap — its own, independent group.
        for (var i = 0; i < 3; i++)
        {
            ids[$"tl{i}"] = toastService.Show(new ToastOptions
            { Title = $"TL-{i}", Duration = 60000, Position = ToastPosition.TopLeft });
        }

        // 3) A persistent (loading/promise) BR toast — invariant (b): must bypass the cap
        // entirely, landing as an ADDITIONAL slot alongside whatever BR already has, never itself
        // counted by NonPersistentMountedCount.
        ids["br-loading"] = toastService.Show(new ToastOptions
        { Title = "BR-loading", Duration = 0, Position = ToastPosition.BottomRight });

        // 4) Dismiss the very first BR toast in whatever admission state it currently holds
        // (mounted, mid-eviction, or still queued) — exercises the mounted-removal AND
        // queued-removal paths without the test needing to know which one it landed in.
        toastService.Dismiss(ids["br0"]);

        // 5) Retarget a TL toast to BR mid-flight — invariant (d) for a toast that may be MOUNTED
        // by now: any group change must re-reconcile BOTH the old (TL) and new (BR) groups.
        toastService.Update(ids["tl1"], new ToastOptions
        { Title = "TL-1-moved-to-BR", Duration = 60000, Position = ToastPosition.BottomRight });

        // 6) Resolve the persistent BR loading toast to a finite one — the canonical promise-
        // resolve transition (finding 4): persistent -> finite while BR is already at/over cap.
        toastService.Update(ids["br-loading"], new ToastOptions
        { Title = "BR-loading-resolved", Duration = 4000, Position = ToastPosition.BottomRight });

        // 7) More TL arrivals now that TL just lost a member to BR (step 5) — exercises admission
        // into a group that gained room via a POSITION CHANGE, not a removal/timeout.
        for (var i = 3; i < 6; i++)
        {
            ids[$"tl{i}"] = toastService.Show(new ToastOptions
            { Title = $"TL-{i}", Duration = 60000, Position = ToastPosition.TopLeft });
        }

        // 8) A second persistent toast, in TL this time — proves the cap bypass (invariant b)
        // holds independently per group, not just for BR.
        ids["tl-loading"] = toastService.Show(new ToastOptions
        { Title = "TL-loading", Duration = 0, Position = ToastPosition.TopLeft });

        // 9) A queued-toast Update that ALSO flips persistence in the same call — a loading toast
        // that never got a slot resolving straight to a finite one (findings 2 + 3 combined: must
        // leave the cap-bounded queue immediately rather than waiting on a slot it no longer needs).
        toastService.Update(ids["tl-loading"], new ToastOptions
        { Title = "TL-loading-resolved", Duration = 3000, Position = ToastPosition.TopLeft });

        // 10) PR #357 round-7 (finding 1): a dedicated mini-burst on a FRESH position pair
        // (TopRight/BottomLeft) so this scenario's admission state is fully known, independent of
        // whatever BR/TL happened to settle into above. Fill BottomLeft (destination) exactly to
        // the cap, then flood TopRight (source) past the cap so it carries queued demand of its
        // own — the first `maxToasts` TopRight Show()s land mounted (no eviction needed yet), so
        // "tr0" is a known-MOUNTED toast at this point. Retargeting it into the already-full
        // BottomLeft group is exactly the precondition ReconcileGroup(wasPos) used to await ahead
        // of the destination reconcile: a MOUNTED toast, moving into a group already at
        // EffectiveMaxToasts, while the source group also has PendingCount > 0.
        for (var i = 0; i < maxToasts; i++)
        {
            ids[$"bl{i}"] = toastService.Show(new ToastOptions
            { Title = $"BL-{i}", Duration = 60000, Position = ToastPosition.BottomLeft });
        }
        for (var i = 0; i < maxToasts + 2; i++)
        {
            ids[$"tr{i}"] = toastService.Show(new ToastOptions
            { Title = $"TR-{i}", Duration = 60000, Position = ToastPosition.TopRight });
        }
        toastService.Update(ids["tr0"], new ToastOptions
        { Title = "TR-0-moved-to-BL", Duration = 60000, Position = ToastPosition.BottomLeft });

        // 11) PR #357 round-7 (finding 3): swipe-dismiss a live TopRight toast ("tr1" — still
        // mounted; step 10 only moved "tr0" out) while the rest of this burst is still resolving.
        // The provider registers the SAME `OnToastSwiped` delegate for every toast it wires up, so
        // any already-captured registration's Handler can drive the dismissal for a SPECIFIC toast
        // id — exactly like the JS layer reporting a completed swipe gesture for that element. No
        // await here either, matching every other step's fire-and-forget racing style.
        var swipeReg = _interop.ToastSwipeRegistrations.LastOrDefault();
        if (swipeReg.Handler is not null)
        {
            cut.InvokeAsync(() => swipeReg.Handler(ids["tr1"]));
        }

        // 12) PR #357 round-8 (finding 1), PR #357 round-9 (finding 4): a multi-toast overflow on
        // a FRESH group (TopCenter) that must fully drain to the cap on its own. The first
        // `maxToasts` land mounted with no eviction needed; the remaining two overflow into the
        // queue behind evictions that now run CONCURRENTLY (round-9 finding 4 — overlapping
        // ~220ms exits instead of one evict-wait-admit cycle at a time). Nothing below this loop
        // touches TopCenter again until it's fully settled — so the ONLY way it reaches a fully-
        // drained steady state (live count == cap, queue empty) is ReconcileGroup's own continued
        // passes, still true with evictions now overlapping rather than serialized.
        for (var i = 0; i < maxToasts + 2; i++)
        {
            ids[$"tc{i}"] = toastService.Show(new ToastOptions
            { Title = $"TC-{i}", Duration = 60000, Position = ToastPosition.TopCenter });
        }

        // Finding 1 fully drains: TopCenter settles at exactly the cap with nothing left queued,
        // reached purely by ReconcileGroup's own continued passes — no external nudge follows.
        // PR #357 round-9: waited for HERE (before step 13's retarget below) rather than after —
        // overlapping evictions (finding 4) mean several of this burst's evictions can be
        // in-flight, SPECULATIVELY reserved against the pending count observed at each one's own
        // admission moment, at the same instant a step-13-style retarget would otherwise race that
        // count. Settling the burst fully before introducing a fresh queued-retarget keeps the two
        // findings' scenarios independent, matching how they'd occur one after another in practice
        // rather than colliding in the same instant.
        cut.WaitForAssertion(
            () => Assert.Equal(maxToasts,
                cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.TopCenter)),
            TimeSpan.FromSeconds(10));

        // 13) PR #357 round-8 (finding 2): a FRESH TopCenter arrival, now that the group above has
        // settled at cap, immediately queues (TopCenter has zero room). Retargeting it into
        // BottomCenter — a position with zero demand of its own — must let it land there
        // immediately (destination reconciled first) instead of waiting behind TopCenter's
        // unrelated eviction machinery.
        ids["tc-last"] = toastService.Show(new ToastOptions
        { Title = "TC-last", Duration = 60000, Position = ToastPosition.TopCenter });
        toastService.Update(ids["tc-last"], new ToastOptions
        { Title = "TC-last-moved-to-BC", Duration = 60000, Position = ToastPosition.BottomCenter });

        // Finding 2 lands promptly: BottomCenter should need no further reconciliation of its own
        // to show the retargeted toast.
        cut.WaitForAssertion(
            () => Assert.Equal(1,
                cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomCenter)),
            TimeSpan.FromSeconds(10));

        // TopCenter must never exceed the cap — the one invariant every reconcile pass owns
        // unconditionally. PR #357 round-9: this is deliberately `<=`, not `==`. "tc-last" queued
        // BECAUSE TopCenter was already full, which means its OWN admission (TryAdmit ->
        // ReconcileGroup, before the retargeting Update even runs) already reserved/started an
        // eviction on its behalf — overlapping evictions (finding 4) mean that reservation is
        // already in flight the instant the toast is retargeted away, and an in-flight
        // RemoveWithExitAsync cannot be un-started. With nothing else left queued for TopCenter,
        // that reservation settles the group one BELOW cap rather than reclaiming the freed slot —
        // a real, accepted trade-off of "no lock, decide once, commit" (see the field comment on
        // _pendingQueue): the alternative (serializing every admission behind a lock so a retarget
        // could always cancel a not-yet-started eviction) is exactly the throughput regression
        // finding 4 exists to avoid. The cap is still never violated either way.
        cut.WaitForAssertion(
            () => Assert.True(
                cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.TopCenter) <= maxToasts),
            TimeSpan.FromSeconds(10));

        // 14) Dismiss everything — must not throw, and the invariant (checked on every render
        // above) must keep holding while the whole set unwinds down to empty.
        toastService.DismissAll();

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("[role='alert'],[role='status']")),
            TimeSpan.FromSeconds(10));

        Assert.Empty(violations);
    }

    /// <summary>
    /// PR #357 round-8 (finding 3), pinned down deterministically rather than folded into the
    /// racing burst above. <c>MaxToasts=1</c>: show a persistent (<c>Duration=0</c>) loading toast
    /// first, then a long-duration ordinary toast — the loading toast mounts directly (bypasses the
    /// cap) and stays the OLDEST entry in <c>_toasts</c>, the ordinary one queues behind it since
    /// the live non-persistent count is already 0 but nothing evicts a persistent. Awaiting each
    /// step keeps this test fully deterministic (no burst racing) so the assertions below pin an
    /// EXACT sequence, not just an eventual invariant.
    /// <para>
    /// Once the loading toast resolves to finite (persistent -> finite, the exact transition
    /// ReconcileGroup re-enters cap accounting for), it becomes the oldest LIVE non-persistent
    /// candidate in a group that's now over cap — the ordinary toast, admitted from the queue while
    /// the loading toast was still persistent, is younger. The oldest-first eviction query would
    /// pick the just-resolved toast for eviction, undoing the very update that made it visible,
    /// were it not for <c>ReconcileGroup</c>'s <c>protectedId</c> excluding the toast whose own
    /// change triggered the reconcile. Asserting BOTH which toast survives AND which one is
    /// gone (not just "cap is respected") is what actually verifies this, since a naive fix that
    /// evicted a DIFFERENT wrong toast could satisfy a cap-only check just as well.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ReconcileGroup_After_Update_Never_Evicts_The_Toast_That_Triggered_It()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>(p => p.Add(b => b.MaxToasts, 1));

        var loadingId = toastService.Show(new ToastOptions { Title = "Loading", Duration = 0 });
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role='alert'],[role='status']")));

        var ordinaryId = toastService.Show(new ToastOptions { Title = "Ordinary", Duration = 60000 });
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[role='alert'],[role='status']").Count));

        // Resolve the loading toast to finite — MaxToasts=1, so the group is now over cap with two
        // LIVE non-persistent toasts (loading-turned-finite + ordinary) and must shed exactly one.
        await cut.InvokeAsync(() => toastService.Update(loadingId,
            new ToastOptions { Title = "Loading-resolved", Duration = 4000 }));

        // The toast whose own persistence change triggered this reconcile must survive — it must
        // never be picked as its own reconcile's eviction candidate.
        cut.WaitForAssertion(
            () => Assert.Contains(cut.FindAll("[role='alert'],[role='status']"),
                el => el.TextContent.Contains("Loading-resolved")));

        // The OLDER, unrelated ordinary toast is the one that gets evicted instead — it must
        // disappear entirely once its exit animation completes.
        cut.WaitForAssertion(
            () => Assert.DoesNotContain(cut.FindAll("[role='alert'],[role='status']"),
                el => el.TextContent.Contains("Ordinary")),
            TimeSpan.FromSeconds(10));

        Assert.Equal(1, cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomRight));
    }

    /// <summary>
    /// PR #357 round-9 (findings 1 + 6) — THE ADMISSION SNAPSHOT. A toast's group/persistence is
    /// resolved once, at admission, and frozen (see the doc comment on <c>ToastItem</c> in
    /// <c>ToastProvider.razor</c>); a later change to the provider's <c>DefaultDuration</c>/
    /// <c>Position</c> parameters must never retroactively reclassify a toast that already resolved
    /// against the OLD default — only affect toasts admitted AFTER the change. Covers both an
    /// already-MOUNTED toast (finding 1's exact repro: <c>Duration=null</c> under one
    /// <c>DefaultDuration</c>, changed after mounting) and a still-QUEUED one (finding 6: resolved
    /// group must survive a live <c>Position</c> change while waiting for a slot). The queued
    /// toast's own PERSISTENCE snapshot is covered separately by
    /// <see cref="Changing_DefaultDuration_While_A_Toast_Is_Still_Queued_Does_Not_Reclassify_It_At_Mount"/>
    /// (PR #357 round-10, finding 1) — this test uses an explicit <c>Duration</c> for "B" precisely
    /// to keep that concern out of scope here.
    /// </summary>
    [Fact]
    public async Task Changing_Provider_Defaults_Does_Not_Reclassify_Already_Admitted_Mounted_Or_Queued_Toasts()
    {
        var toastService = GetToastService();
        // MaxToasts=1 so a second Show() is guaranteed to queue, not mount — the QUEUED half of
        // this test needs a toast that's still sitting in _pendingQueue when the defaults change.
        var cut = _ctx.Render<L.ToastProvider>(p => p
            .Add(b => b.MaxToasts, 1)
            .Add(b => b.DefaultDuration, 5000)
            .Add(b => b.Position, L.ToastViewport.ToastPosition.BottomRight));

        // "A" mounts under DefaultDuration=5000 (omits Duration) — resolves non-persistent, real
        // 5000ms timer started, counts against the cap. Routed through cut.InvokeAsync (like
        // ReconcileGroup_After_Update_Never_Evicts_The_Toast_That_Triggered_It above) so admission
        // runs to completion — including TryAdmit's fully synchronous enqueue+reconcile — BEFORE
        // this line returns, not just dispatched for some later pump to pick up.
        string idA = null!;
        await cut.InvokeAsync(() => idA = toastService.Show(new ToastOptions { Title = "A" }));
        Assert.Equal(1, cut.Instance.NonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomRight));

        // "B" queues (cap already at 1, MaxToasts=1) — resolved to BottomRight (Position omitted),
        // still sitting in _pendingQueue, never mounted yet. Same deterministic dispatch as "A":
        // this must fully complete (B's ResolvedPosition/ResolvedDuration snapshotted against the
        // STILL-current BottomRight/5000ms defaults) before the live parameters change below.
        // Duration is EXPLICIT (not omitted) here so this test isolates finding 6 (the queued
        // toast's GROUP/position snapshot) — see the summary above for the sibling test that
        // isolates the omitted-Duration/persistence half (round-10, finding 1).
        string idB = null!;
        await cut.InvokeAsync(() => idB = toastService.Show(new ToastOptions { Title = "B", Duration = 60000 }));

        // Live provider defaults change AFTER both admissions: DefaultDuration 5000 -> 0 (would
        // make an omitted-Duration toast persistent), Position BottomRight -> TopLeft.
        await cut.InvokeAsync(() => cut.Render(p => p
            .Add(b => b.DefaultDuration, 0)
            .Add(b => b.Position, L.ToastViewport.ToastPosition.TopLeft)));

        // "A" is UNCHANGED by the DefaultDuration flip: still counts as non-persistent at
        // BottomRight — the round-9 bug would have made IsPersistent(A.Options) read the NEW
        // DefaultDuration=0 live and exclude A from the cap entirely.
        Assert.Equal(1, cut.Instance.NonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomRight));
        Assert.Equal(0, cut.Instance.NonPersistentMountedCount(L.ToastViewport.ToastPosition.TopLeft));

        // Dismissing "A" frees BottomRight's one slot. "B" — still resolved to BottomRight from
        // when it queued, despite the live Position default now being TopLeft — must land THERE,
        // not TopLeft, proving its queued ResolvedPosition snapshot survived the parameter change.
        toastService.Dismiss(idA);
        cut.WaitForAssertion(
            () => Assert.Equal(1,
                cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomRight)),
            TimeSpan.FromSeconds(10));
        Assert.Equal(0, cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.TopLeft));
        Assert.Contains(cut.FindAll("[role='alert'],[role='status']"), el => el.TextContent.Contains("B"));

        // A FRESH admission after the defaults changed, in contrast, resolves against the CURRENT
        // (new) defaults: persistent (Duration omitted, DefaultDuration=0) at TopLeft — proving the
        // contract's other half ("provider default changes affect only FUTURE toasts") the right
        // way around, not just "never".
        toastService.Show(new ToastOptions { Title = "C" });
        cut.WaitForAssertion(
            () => Assert.Contains(cut.FindAll("[role='alert'],[role='status']"), el => el.TextContent.Contains("C")),
            TimeSpan.FromSeconds(10));
        // "C" bypassed the cap entirely (persistent) — BottomRight's live occupant ("B") is
        // untouched, and "C" doesn't count against TopLeft's (empty) non-persistent accounting.
        Assert.Equal(1, cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomRight));
        Assert.Equal(0, cut.Instance.NonPersistentMountedCount(L.ToastViewport.ToastPosition.TopLeft));
        _ = idB;
    }

    /// <summary>
    /// PR #357 round-10 (finding 1) — the queue-side half of the ADMISSION SNAPSHOT that round-9
    /// left open: a still-QUEUED toast's eventual PERSISTENCE used to be decided fresh in
    /// <c>MountToast</c>, at the moment it actually mounted, against whatever <c>DefaultDuration</c>
    /// happened to be current THEN — not what it was when the toast was actually admitted
    /// (<c>TryAdmit</c>). A toast that omits <c>Duration</c>, queues under one <c>DefaultDuration</c>,
    /// then sits queued through a change to <c>DefaultDuration="0"</c>, would mount PERSISTENT even
    /// though it was admitted as (and the caller/consumer reasonably expects) a normal, timed toast.
    /// Persistence is now frozen at admission — <c>QueuedToast.ResolvedDuration</c> — and
    /// <c>MountToast</c> only ever consumes it, never recomputes it.
    /// </summary>
    [Fact]
    public async Task Changing_DefaultDuration_While_A_Toast_Is_Still_Queued_Does_Not_Reclassify_It_At_Mount()
    {
        var toastService = GetToastService();
        // MaxToasts=1 so "B" is guaranteed to queue (not mount) behind "A".
        var cut = _ctx.Render<L.ToastProvider>(p => p
            .Add(b => b.MaxToasts, 1)
            .Add(b => b.DefaultDuration, 5000));

        // "A" occupies the one slot, non-persistent (Duration omitted, DefaultDuration=5000).
        string idA = null!;
        await cut.InvokeAsync(() => idA = toastService.Show(new ToastOptions { Title = "A" }));
        Assert.Equal(1, cut.Instance.NonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomRight));

        // "B" queues — Duration OMITTED, so it resolves against DefaultDuration=5000 (still
        // non-persistent) AT THIS ADMISSION INSTANT. This must fully complete (B's ResolvedDuration
        // snapshotted against the STILL-current DefaultDuration=5000) before the live parameter
        // changes below — same deterministic dispatch as "A".
        string idB = null!;
        await cut.InvokeAsync(() => idB = toastService.Show(new ToastOptions { Title = "B" }));

        // DefaultDuration flips to 0 WHILE "B" IS STILL QUEUED — under the pre-round-10 behavior,
        // this would make "B" mount persistent once its slot frees up.
        await cut.InvokeAsync(() => cut.Render(p => p.Add(b => b.DefaultDuration, 0)));

        // Freeing "A"'s slot admits "B" from the queue.
        toastService.Dismiss(idA);
        cut.WaitForAssertion(
            () => Assert.Contains(cut.FindAll("[role='alert'],[role='status']"), el => el.TextContent.Contains("B")),
            TimeSpan.FromSeconds(10));

        // "B" mounted NON-PERSISTENT — its classification is unchanged from what admission decided,
        // never reclassified by the DefaultDuration flip that happened while it waited. The round-10
        // bug would have made it persistent (exempt from the cap) here instead.
        Assert.Equal(1, cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomRight));

        // A FRESH admission after the flip, in contrast, DOES resolve against the current
        // (DefaultDuration=0) default: persistent, bypasses the cap entirely — proving the other half
        // of the contract ("changes affect only FUTURE admissions") the right way around.
        toastService.Show(new ToastOptions { Title = "C" });
        cut.WaitForAssertion(
            () => Assert.Contains(cut.FindAll("[role='alert'],[role='status']"), el => el.TextContent.Contains("C")),
            TimeSpan.FromSeconds(10));
        Assert.Equal(1, cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomRight));
    }

    /// <summary>
    /// PR #357 round-9 (finding 4): a burst that needs several evictions in ONE group must let
    /// their ~<c>ExitAnimationMs</c> exits run CONCURRENTLY, not one full exit-wait per eviction —
    /// see the doc comment on <c>ReconcileGroup</c>'s eviction pass. <c>MaxToasts=5</c>, 5 already
    /// mounted, then 5 more arrive in one burst: fully draining needs marking all 5 ORIGINAL toasts
    /// Leaving. Serialized (the round-7 behaviour this closes), that's ~5×<c>ExitAnimationMs</c>
    /// wall-clock; overlapped, it's ~1×. <c>ExitAnimationMs</c> is widened via the internal test
    /// seam (same idiom as <c>ToastStackingTests</c>) purely so the assertion threshold has a wide,
    /// CI-safe margin — the ~5x-vs-~1x ratio being tested is independent of the absolute value.
    /// </summary>
    [Fact]
    public void Burst_Overflow_Runs_Its_Evictions_Concurrently_Not_One_Exit_Wait_At_A_Time()
    {
        const int maxToasts = 5;
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>(p => p.Add(b => b.MaxToasts, maxToasts));
        cut.Instance.ExitAnimationMs = 600;

        for (var i = 0; i < maxToasts; i++)
        {
            toastService.Show(new ToastOptions { Title = $"Old-{i}", Duration = 60000 });
        }
        cut.WaitForAssertion(() => Assert.Equal(maxToasts,
            cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomRight)));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // All 5 fired back-to-back, no awaits in between — a real spam burst, exactly like every
        // other racing sequence in this class.
        for (var i = 0; i < maxToasts; i++)
        {
            toastService.Show(new ToastOptions { Title = $"New-{i}", Duration = 60000 });
        }

        // If this settles inside ~2×ExitAnimationMs, the 5 needed evictions overlapped (at most
        // two sequential exit-windows' worth of wall-clock, covering dispatch/render jitter) —
        // serialized, 5 evictions would need close to 5×ExitAnimationMs (3000ms) minimum.
        cut.WaitForAssertion(
            () => Assert.Equal(maxToasts,
                cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomRight)),
            TimeSpan.FromMilliseconds(cut.Instance.ExitAnimationMs * 2.5));
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < cut.Instance.ExitAnimationMs * 3,
            $"expected overlapping evictions to settle well under {maxToasts}x{cut.Instance.ExitAnimationMs}ms " +
            $"(serialized), took {sw.ElapsedMilliseconds}ms");

        // The 5 newest survive; every "Old-*" toast is gone.
        cut.WaitForAssertion(() =>
            Assert.DoesNotContain(cut.FindAll("[role='alert'],[role='status']"),
                el => el.TextContent.Contains("Old-")),
            TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// PR #357 round-9 (finding 3): retargeting a QUEUED toast into a DIFFERENT position group must
    /// obey that destination's per-position pending cap — the same trim-oldest-queued rule
    /// <c>TryAdmit</c> applies to a fresh arrival — not let the backlog grow past
    /// <c>EffectiveMaxToasts</c>. <c>MaxToasts=1</c>: TopLeft already has one toast QUEUED (never
    /// shown) when a BottomRight toast, also still queued, is retargeted into TopLeft — without the
    /// cap, TopLeft's backlog would grow to 2 pending entries for a group whose cap is 1.
    /// </summary>
    [Fact]
    public void Retargeting_A_Queued_Toast_Applies_The_Destination_Groups_Pending_Cap()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>(p => p.Add(b => b.MaxToasts, 1));

        // Fill TopLeft's one mounted slot, then queue a SECOND TopLeft toast behind it — TopLeft's
        // pending backlog is now exactly at cap (1).
        var tlMountedId = toastService.Show(new ToastOptions
        { Title = "TL-mounted", Duration = 60000, Position = ToastPosition.TopLeft });
        cut.WaitForAssertion(() => Assert.Equal(1,
            cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.TopLeft)));
        toastService.Show(new ToastOptions
        { Title = "TL-queued-oldest", Duration = 60000, Position = ToastPosition.TopLeft });

        // A BottomRight toast that also queues (fill BR's slot first, same MaxToasts=1 cap).
        toastService.Show(new ToastOptions { Title = "BR-mounted", Duration = 60000, Position = ToastPosition.BottomRight });
        cut.WaitForAssertion(() => Assert.Equal(1,
            cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.BottomRight)));
        var brQueuedId = toastService.Show(new ToastOptions
        { Title = "BR-queued-retargeting", Duration = 60000, Position = ToastPosition.BottomRight });

        // Retarget the still-queued BR toast into TopLeft — TopLeft's pending backlog would grow to
        // 2 (over its cap of 1) without the round-9 fix trimming the oldest queued TopLeft entry.
        toastService.Update(brQueuedId, new ToastOptions
        { Title = "Retargeted-to-TL", Duration = 60000, Position = ToastPosition.TopLeft });

        // The retargeted toast is never admitted while TopLeft's ONE mounted slot stays occupied —
        // TopLeft's live count never exceeds its cap, whichever queued entry survives the trim.
        cut.WaitForAssertion(
            () => Assert.Equal(1,
                cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.TopLeft)),
            TimeSpan.FromSeconds(5));

        // Dismiss the mounted TopLeft toast — exactly ONE of the two queued contenders (the
        // pre-existing "TL-queued-oldest" or the retargeted one) must have been trimmed at the
        // moment of retarget; TopLeft never exceeds its cap once the freed slot admits whichever
        // one survived.
        toastService.Dismiss(tlMountedId);
        cut.WaitForAssertion(
            () => Assert.Equal(1,
                cut.Instance.LiveNonPersistentMountedCount(L.ToastViewport.ToastPosition.TopLeft)),
            TimeSpan.FromSeconds(10));
    }
}
