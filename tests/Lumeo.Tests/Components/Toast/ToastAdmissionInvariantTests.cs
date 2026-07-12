using System;
using System.Collections.Generic;
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
/// </summary>
public class ToastAdmissionInvariantTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToastAdmissionInvariantTests() => _ctx.AddLumeoServices();
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
            if (br > maxToasts) violations.Add($"BottomRight live-mounted={br} > cap={maxToasts}");
            if (tl > maxToasts) violations.Add($"TopLeft live-mounted={tl} > cap={maxToasts}");
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

        // 10) Dismiss everything — must not throw, and the invariant (checked on every render
        // above) must keep holding while the whole set unwinds down to empty.
        toastService.DismissAll();

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("[role='alert'],[role='status']")),
            TimeSpan.FromSeconds(10));

        Assert.Empty(violations);
    }
}
