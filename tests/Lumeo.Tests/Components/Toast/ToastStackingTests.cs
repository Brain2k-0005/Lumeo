using System;
using System.Linq;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Toast;

/// <summary>
/// Sonner-style toast stacking (<see cref="L.ToastProvider.StackToasts"/>, default on).
/// Covers the markup contract the CSS in lumeo.css's "Toast stacking" section relies on:
/// <c>data-stacked</c>/<c>data-expanded</c>/<c>data-stack-edge</c> on the group
/// (<see cref="L.ToastViewport"/>) and <c>data-index</c> on each <see cref="L.Toast"/>.
/// </summary>
public class ToastStackingTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ToastStackingTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private ToastService GetToastService() =>
        (ToastService)_ctx.Services.GetRequiredService(typeof(ToastService));

    private static string? Attr(AngleSharp.Dom.IElement el, string name) => el.GetAttribute(name);

    // ── Collapsed markup: data-index ordering, front toast is newest ──────────

    [Fact]
    public void Collapsed_Group_Assigns_DataIndex_Newest_First_And_Caps_At_Three_Visible()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show(new ToastOptions { Title = "One", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "Two", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "Three", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "Four", Duration = 60000 });

        cut.WaitForAssertion(() =>
            Assert.Equal(4, cut.FindAll("[role='alert'],[role='status']").Count));

        // Newest ("Four") is the front toast: data-index="0".
        var four = cut.FindAll("[role='alert'],[role='status']")
            .Single(e => e.TextContent.Contains("Four"));
        Assert.Equal("0", Attr(four, "data-index"));

        // Oldest ("One") is deepest in the stack: data-index="3" — beyond the
        // "at most 3 visible" (indices 0/1/2) cap the CSS enforces via opacity.
        var one = cut.FindAll("[role='alert'],[role='status']")
            .Single(e => e.TextContent.Contains("One"));
        Assert.Equal("3", Attr(one, "data-index"));

        // Every index 0..3 is present and unique — the CSS contract the
        // "at most 3 visible" rule depends on (indices >= 3 collapse to the
        // same hidden slot; here that's exactly index 3, the deepest toast).
        var indices = cut.FindAll("[role='alert'],[role='status']")
            .Select(e => Attr(e, "data-index"))
            .OrderBy(i => i)
            .ToList();
        Assert.Equal(new[] { "0", "1", "2", "3" }, indices);
    }

    // ── Hover expands, restores list semantics ─────────────────────────────

    [Fact]
    public void Hover_On_Group_Sets_DataExpanded_True_And_Leave_Collapses_Again()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show(new ToastOptions { Title = "A", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "B", Duration = 60000 });

        cut.WaitForAssertion(() =>
            Assert.Equal(2, cut.FindAll("[role='alert'],[role='status']").Count));

        var group = cut.Find("[data-stacked='true']");
        Assert.Equal("false", Attr(group, "data-expanded"));

        // WaitForAssertion (not a bare Assert) because OnMouseEnter/Leave bubble
        // through Toast's EventCallback-bound OnMouseEnter/Leave (PauseTimer /
        // ResumeTimer, themselves async-capable via SafeAsyncDispatcher) before
        // reaching ToastViewport's own plain handler — the render that commits
        // _expanded can land a tick after MouseEnter()/MouseLeave() returns.
        group.MouseEnter();
        cut.WaitForAssertion(() =>
            Assert.Equal("true", Attr(cut.Find("[data-stacked='true']"), "data-expanded")));

        group.MouseLeave();
        cut.WaitForAssertion(() =>
            Assert.Equal("false", Attr(cut.Find("[data-stacked='true']"), "data-expanded")));
    }

    // ── Focus-within expands (a11y path) ───────────────────────────────────

    [Fact]
    public void FocusIn_On_A_Toast_Inside_The_Group_Expands_It()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show(new ToastOptions { Title = "A", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "B", Duration = 60000 });

        cut.WaitForAssertion(() =>
            Assert.Equal(2, cut.FindAll("[role='alert'],[role='status']").Count));

        Assert.Equal("false", Attr(cut.Find("[data-stacked='true']"), "data-expanded"));

        // Focus lands on a close button inside one of the toasts — focusin bubbles
        // through the toast root up to the group container (same bubbling contract
        // proven by the #64 battle-test regression for PauseTimer). Toast's own
        // OnFocusIn/OnFocusOut are EventCallback-bound (PauseTimer/ResumeTimer,
        // async-capable via SafeAsyncDispatcher) ahead of ToastViewport's plain
        // handler in the bubble chain, so — like the MouseEnter/Leave case above —
        // assert via WaitForAssertion, not a bare Assert right after the call.
        cut.Find("button").FocusIn();

        cut.WaitForAssertion(() =>
            Assert.Equal("true", Attr(cut.Find("[data-stacked='true']"), "data-expanded")));

        cut.Find("button").FocusOut();

        cut.WaitForAssertion(() =>
            Assert.Equal("false", Attr(cut.Find("[data-stacked='true']"), "data-expanded")));
    }

    // PR #357 round-2 (CodeRabbit + Codex): ToastViewport used to track hover
    // and focus in a SINGLE shared `_expanded` flag, so a mouseleave (pointer
    // wandering off the group while focus is still on e.g. a toast's close
    // button — a completely normal keyboard-then-mouse sequence) collapsed the
    // stack right out from under the focused control. Hover and focus are now
    // tracked independently (Expanded = hovered || focused) — a stack that's
    // expanded because of keyboard focus must survive an unrelated mouseleave.
    [Fact]
    public void FocusIn_Survives_An_Unrelated_MouseLeave_Keeps_Group_Expanded()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show(new ToastOptions { Title = "A", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "B", Duration = 60000 });

        cut.WaitForAssertion(() =>
            Assert.Equal(2, cut.FindAll("[role='alert'],[role='status']").Count));

        // Mouse hovers the group first (e.g. moving toward it), THEN keyboard
        // focus lands inside — the realistic sequence for a mouse user who
        // then presses the Tab key to reach the toast's close button.
        // (PR #357 round-4 P3: the prior wording of this comment word-matched
        // an unrelated component's name and mislisted this file as that
        // component's test coverage — see
        // PerComponentEnricher.HasRealComponentMention.)
        var group = cut.Find("[data-stacked='true']");
        group.MouseEnter();
        cut.WaitForAssertion(() =>
            Assert.Equal("true", Attr(cut.Find("[data-stacked='true']"), "data-expanded")));

        cut.Find("button").FocusIn();
        cut.WaitForAssertion(() =>
            Assert.Equal("true", Attr(cut.Find("[data-stacked='true']"), "data-expanded")));

        // Pointer leaves the group — with a single shared flag this used to
        // collapse the stack (and reposition/hide the very control that still
        // has keyboard focus). Focus is still active, so it must stay expanded.
        group.MouseLeave();
        cut.WaitForAssertion(() =>
            Assert.Equal("true", Attr(cut.Find("[data-stacked='true']"), "data-expanded")));

        // Only once focus ALSO leaves does the group finally collapse.
        cut.Find("button").FocusOut();
        cut.WaitForAssertion(() =>
            Assert.Equal("false", Attr(cut.Find("[data-stacked='true']"), "data-expanded")));
    }

    // PR #357 (finding — Codex): ToastViewport.OnExpandedChanged is edge-triggered — it fires only
    // when Expanded flips (NotifyExpandedChangeAsync). A toast admitted WHILE the group is already
    // expanded therefore never gets its own HandleGroupExpandedChanged(Group) pause call, because
    // Expanded never flips for it. Pre-fix, MountToast started that toast's auto-dismiss timer
    // unconditionally, so it could count down and vanish under the cursor while the user was still
    // reading the (already fanned-out) stack, unlike every sibling that WAS mounted before the
    // hover began. Fixed via ToastProvider.StartOrPauseTimer consulting the live _expandedGroups
    // set at admission time.
    [Fact]
    public async Task Toast_Added_While_Group_Already_Expanded_Starts_Paused_And_Survives_Its_Own_Duration()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        // Two long-duration toasts first so the group is already IsStacked (data-stacked="true",
        // ToastCount > 1) before hovering — matches every other hover test's setup in this file.
        toastService.Show(new ToastOptions { Title = "A", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "A2", Duration = 60000 });

        cut.WaitForAssertion(() =>
            Assert.Equal(2, cut.FindAll("[role='alert'],[role='status']").Count));

        // Hover the group BEFORE the third toast ever arrives — the group is already Expanded
        // (data-expanded="true") when "B" gets admitted below.
        var group = cut.Find("[data-stacked='true']");
        group.MouseEnter();
        cut.WaitForAssertion(() =>
            Assert.Equal("true", Attr(cut.Find("[data-stacked='true']"), "data-expanded")));

        // "B" arrives with a short real Duration WHILE the group is already hovered/expanded.
        // Expanded stays true (it was already true), so ToastViewport never re-fires
        // OnExpandedChanged for this admission — the exact edge-triggered gap under test.
        toastService.Show(new ToastOptions { Title = "B", Duration = 100 });
        cut.WaitForAssertion(() =>
            Assert.Equal(3, cut.FindAll("[role='alert'],[role='status']").Count));

        // Wait well past "B"'s 100ms duration while STILL hovered — pre-fix, "B"'s timer started
        // running unpaused at admission and would have auto-dismissed here even though the group
        // (and "B" itself, fanned out in the expanded list) is fully visible to the user.
        await Task.Delay(400);
        Assert.Equal(3, cut.FindAll("[role='alert'],[role='status']").Count);
        Assert.Contains(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("B"));

        // Leaving the group resumes "B" with its full remaining duration (it never ran while
        // paused) — it now dismisses normally, proving this isn't just a timer that got lost.
        group.MouseLeave();
        cut.WaitForAssertion(() =>
            Assert.DoesNotContain(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("B")),
            TimeSpan.FromSeconds(5));
    }

    // ── Opt-out renders the legacy list (no stacking markup at all) ────────

    [Fact]
    public void StackToasts_False_Renders_Legacy_List_Without_DataIndex()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>(p => p.Add(b => b.StackToasts, false));

        toastService.Show(new ToastOptions { Title = "A", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "B", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "C", Duration = 60000 });

        cut.WaitForAssertion(() =>
            Assert.Equal(3, cut.FindAll("[role='alert'],[role='status']").Count));

        Assert.Equal("false", Attr(cut.Find("[data-stacked]"), "data-stacked"));
        foreach (var toastEl in cut.FindAll("[role='alert'],[role='status']"))
        {
            Assert.Null(Attr(toastEl, "data-index"));
        }
    }

    // ── Per-position groups are independent ────────────────────────────────

    [Fact]
    public void Independent_Position_Groups_Number_DataIndex_Separately()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>(p => p
            .Add(b => b.Position, L.ToastViewport.ToastPosition.BottomRight));

        // Two toasts in the default (BottomRight) group.
        toastService.Show(new ToastOptions { Title = "BR-1", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "BR-2", Duration = 60000 });
        // Two toasts explicitly routed to TopLeft — a separate group.
        toastService.Show(new ToastOptions { Title = "TL-1", Duration = 60000, Position = ToastPosition.TopLeft });
        toastService.Show(new ToastOptions { Title = "TL-2", Duration = 60000, Position = ToastPosition.TopLeft });

        cut.WaitForAssertion(() =>
            Assert.Equal(4, cut.FindAll("[role='alert'],[role='status']").Count));

        // Both groups independently front their own newest toast at index 0 —
        // numbering does not leak across position groups.
        var br2 = cut.FindAll("[role='alert'],[role='status']").Single(e => e.TextContent.Contains("BR-2"));
        var tl2 = cut.FindAll("[role='alert'],[role='status']").Single(e => e.TextContent.Contains("TL-2"));
        Assert.Equal("0", Attr(br2, "data-index"));
        Assert.Equal("0", Attr(tl2, "data-index"));

        var groups = cut.FindAll("[data-stacked='true']");
        Assert.Equal(2, groups.Count);
        // The peek points AWAY from the anchored edge: bottom groups peek above
        // the front toast, top groups below it (user feedback 2026-07-11).
        Assert.Contains(groups, g => Attr(g, "data-stack-edge") == "up");   // BottomRight
        Assert.Contains(groups, g => Attr(g, "data-stack-edge") == "down"); // TopLeft
    }

    // ── Exit-during-collapsed does not strand transforms ───────────────────

    [Fact]
    public void Dismissing_The_Front_Toast_While_Collapsed_Reindexes_Cleanly()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show(new ToastOptions { Title = "Older", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "Newest", Duration = 0 }); // manual dismiss only

        cut.WaitForAssertion(() =>
            Assert.Equal(2, cut.FindAll("[role='alert'],[role='status']").Count));

        var newest = cut.FindAll("[role='alert'],[role='status']").Single(e => e.TextContent.Contains("Newest"));
        Assert.Equal("0", Attr(newest, "data-index"));

        // Dismiss the front toast via its close button while the group is
        // collapsed (never hovered) — this is the front/index-0 element the
        // stacking CSS gives `position: relative` + no transform.
        newest.QuerySelector("button")!.Click();

        // While leaving, it carries the exit animation class alongside its
        // (still valid) data-index — no stranded/orphaned stacking transform.
        cut.WaitForAssertion(() =>
        {
            var leaving = cut.FindAll("[role='alert'],[role='status']")
                .SingleOrDefault(e => e.TextContent.Contains("Newest"));
            Assert.NotNull(leaving);
            Assert.Contains("animate-toast-out", Attr(leaving!, "class") ?? "");
        });

        // After the exit completes, only "Older" remains and gets promoted to
        // the front slot (index 0) — no leftover element, no stale index.
        cut.WaitForAssertion(() =>
        {
            var remaining = cut.FindAll("[role='alert'],[role='status']");
            Assert.Single(remaining);
            Assert.Contains("Older", remaining[0].TextContent);
            Assert.Equal("0", Attr(remaining[0], "data-index"));
        });
    }

    // PR #357 round-2 (Codex): a depth>=3 (hidden) toast that already started
    // its exit must stay hidden for its WHOLE exit, even when a NEWER sibling
    // ahead of it finishes dismissing mid-exit — which (pre-fix) recomputed
    // its data-index every render and could promote it to a visible index
    // (e.g. 3 -> 2), letting `.animate-toast-out`'s `from { opacity: 1 }`
    // flash it visible for the remainder of its exit. ToastProvider now
    // freezes a toast's data-index (ToastItem.FrozenIndex) the instant it
    // starts leaving; this proves the freeze survives a sibling's ACTUAL
    // removal (not just that sibling being marked Leaving).
    //
    // PR #357 round-5 (P2): once "Four" (a NEWER sibling ahead of "Two") is marked Leaving,
    // round-3's live-only ranking already re-renders "Two" from live index 3 down to live index
    // 2 (it fills the hole "Four" left among the four still-live toasts) — a plain, expected
    // re-render, nothing to do with dismissing "Two" yet. ComputeStackIndex previously ranked
    // against the RAW group (including "Four" as an ordinary member), so freezing "Two" at the
    // instant IT starts leaving produced 3 — one MORE than what was actually on screen the
    // render before, i.e. a backward jump from visible (depth 2) to hidden (depth 3) the moment
    // its own exit begins. Fixed by ranking the freeze the same live-only way the markup does,
    // so "Two" freezes at 2 — exactly where it was already rendered — and its own exit is
    // visually continuous.
    [Fact]
    public async Task Depth3_Toast_Keeps_Its_Frozen_Index_Even_When_A_Newer_Sibling_Finishes_Dismissing_Mid_Exit()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        // Oldest-to-newest: One, Two, Three, Four, Five. Collapsed data-index
        // is newest-first: Five=0, Four=1, Three=2, Two=3, One=4. "Two"
        // (index 3, hidden — depth>=3) is the toast under test; "Four"
        // (index 1, a NEWER sibling ahead of it) is dismissed first so its
        // removal lands WHILE "Two" is still mid-exit.
        toastService.Show(new ToastOptions { Title = "One", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "Two", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "Three", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "Four", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "Five", Duration = 60000 });

        cut.WaitForAssertion(() =>
            Assert.Equal(5, cut.FindAll("[role='alert'],[role='status']").Count));

        var two = cut.FindAll("[role='alert'],[role='status']").Single(e => e.TextContent.Contains("Two"));
        Assert.Equal("3", Attr(two, "data-index"));

        // CI-flake root cause (Assert.NotNull at the post-unmount snapshot, e.g. CI run
        // 29350360141 attempt 2): the old choreography raced two REAL exit timers — "Four"
        // 800ms, a fixed Task.Delay(150) stagger, then "Two" 800ms — and finally took a BARE
        // one-shot snapshot of "Two" right after a WaitForAssertion observed "Four"'s unmount.
        // The whole proof rested on that 150ms stagger surviving CI starvation: when the thread
        // pool delivered the two Task.Delay continuations back-to-back (or the checker woke
        // late), "Two" was already gone by the time the snapshot ran. Deterministic
        // re-choreography, same invariant:
        //
        //  1. BOTH dismissals happen inside ONE renderer dispatch. RemoveWithExitAsync stamps
        //     Leaving + FrozenIndex synchronously (before its first await), so "Two"'s freeze
        //     is computed while "Four" is Leaving-but-still-mounted BY CONSTRUCTION — there is
        //     no wall-clock gap for a starved runner to erase anymore.
        //  2. The exits get ASYMMETRIC windows via the internal test seam, mutated between the
        //     two clicks: "Four" captures 1000ms, "Two" captures 5000ms (each dismissal reads
        //     ExitAnimationMs when it reaches its own Task.Delay, still inside this dispatch —
        //     every await before it, incl. the swipe-unregister interop, completes
        //     synchronously under bUnit's loose-mode JSInterop). "Four" therefore always
        //     unmounts ~4s before "Two": the "newer sibling finishes first" ordering is
        //     enforced by construction, not by a 150ms-vs-800ms scheduling race.
        //  3. Every subsequent check reads ONE FindAll snapshot inside ONE WaitForAssertion —
        //     never a WaitForAssertion followed by a bare re-read that a late unmount can
        //     invalidate in between.
        await cut.InvokeAsync(() =>
        {
            cut.Instance.ExitAnimationMs = 1000; // "Four"'s exit window
            cut.FindAll("[role='alert'],[role='status']").Single(e => e.TextContent.Contains("Four"))
                .QuerySelector("button")!.Click();
            // "Four" is now Leaving (frozen at 1) and still mounted — stamped synchronously.
            cut.Instance.ExitAnimationMs = 5000; // "Two"'s exit window — outlives every check below
            cut.FindAll("[role='alert'],[role='status']").Single(e => e.TextContent.Contains("Two"))
                .QuerySelector("button")!.Click();
        });

        // Both mid-exit in one snapshot: "Four" (newer, frozen at 1) still mounted and leaving;
        // "Two" leaving at frozen index 3 — PR #357 P2 (AvailableLiveSlots): "Four"'s frozen
        // slot 1 stays reserved for the live ranking, so "Three" shifts to 2 and "Two" to 3. A
        // 2 here would mean the freeze ranked against a group "Four" had already left. The
        // first check runs inline immediately (straight-line code, well inside "Four"'s 1000ms
        // window); the generous ceiling only buys headroom for re-checks under CI load.
        cut.WaitForAssertion(() =>
        {
            var all = cut.FindAll("[role='alert'],[role='status']");
            var fourLeaving = all.SingleOrDefault(e => e.TextContent.Contains("Four"));
            Assert.NotNull(fourLeaving);
            Assert.Contains("animate-toast-out", Attr(fourLeaving!, "class") ?? "");
            var twoLeaving = all.SingleOrDefault(e => e.TextContent.Contains("Two"));
            Assert.NotNull(twoLeaving);
            Assert.Contains("animate-toast-out", Attr(twoLeaving!, "class") ?? "");
            Assert.Equal("3", Attr(twoLeaving!, "data-index"));
        }, TimeSpan.FromSeconds(5));

        // The regression: once "Four" ACTUALLY unmounts (~1s — a newer sibling truly leaving
        // the list, not just being marked Leaving), "Two" must still be mid-exit (its own
        // window runs another ~4s) and must NOT be re-promoted (or demoted) by "Four"'s
        // removal — data-index stays frozen at 3 for the rest of ITS OWN exit, matching
        // whatever CSS selector it was already in the moment dismissal began. Checked in the
        // SAME atomic snapshot that observes "Four" gone — the old split (WaitForAssertion,
        // then a bare re-read) was exactly the recorded CI failure.
        cut.WaitForAssertion(() =>
        {
            var all = cut.FindAll("[role='alert'],[role='status']");
            Assert.DoesNotContain(all, e => e.TextContent.Contains("Four"));
            var twoStillLeaving = all.SingleOrDefault(e => e.TextContent.Contains("Two"));
            Assert.NotNull(twoStillLeaving);
            Assert.Contains("animate-toast-out", Attr(twoStillLeaving!, "class") ?? "");
            Assert.Equal("3", Attr(twoStillLeaving!, "data-index"));
        }, TimeSpan.FromSeconds(10));

        // Let "Two"'s own exit finish too — no leftover element, no stale index.
        cut.WaitForAssertion(() =>
            Assert.DoesNotContain(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("Two")),
            TimeSpan.FromSeconds(10));
    }

    // PR #357 round-4 (P2): dismissing the FRONT toast while an older sibling remains live must
    // not hand BOTH of them data-index="0" — lumeo.css only keeps index 0 in normal flow
    // (position: relative), every other index is position: absolute, so two elements at index 0
    // simultaneously expand the collapsed stack into two rows for the whole 220ms exit. The
    // live-only ranking (liveCount/liveCursor) independently guarantees SOME live toast always
    // gets index 0 (round-3) — which collides with the front toast's OWN frozen index 0 the
    // instant it starts leaving. The next live toast must sit at index 1 (still hidden behind the
    // leaving front, position: absolute) until the front actually unmounts, then take over index 0.
    [Fact]
    public void Live_Toast_Does_Not_Also_Claim_Index_Zero_While_The_Front_Toast_Is_Leaving()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show(new ToastOptions { Title = "Older", Duration = 60000 });
        toastService.Show(new ToastOptions { Title = "Newest", Duration = 60000 });

        cut.WaitForAssertion(() =>
            Assert.Equal(2, cut.FindAll("[role='alert'],[role='status']").Count));

        var newest = cut.FindAll("[role='alert'],[role='status']").Single(e => e.TextContent.Contains("Newest"));
        Assert.Equal("0", Attr(newest, "data-index"));

        // Dismiss the front toast ("Newest") — it keeps data-index="0" (FrozenIndex) for its
        // whole 220ms exit.
        newest.QuerySelector("button")!.Click();

        cut.WaitForAssertion(() =>
        {
            var all = cut.FindAll("[role='alert'],[role='status']");
            var leaving = all.SingleOrDefault(e => e.TextContent.Contains("Newest"));
            var older = all.SingleOrDefault(e => e.TextContent.Contains("Older"));
            Assert.NotNull(leaving);
            Assert.NotNull(older);
            Assert.Contains("animate-toast-out", Attr(leaving!, "class") ?? "");
            // The regression: exactly ONE element at data-index="0" (the leaving front toast),
            // never two. "Older" — the only live toast left — must be pushed to index 1.
            Assert.Equal("0", Attr(leaving!, "data-index"));
            Assert.Equal("1", Attr(older!, "data-index"));
        }, TimeSpan.FromSeconds(5));

        // Once "Newest" actually unmounts, "Older" takes over index 0 — no frozen occupant left.
        cut.WaitForAssertion(() =>
        {
            var all = cut.FindAll("[role='alert'],[role='status']");
            Assert.DoesNotContain(all, e => e.TextContent.Contains("Newest"));
            var older = all.Single(e => e.TextContent.Contains("Older"));
            Assert.Equal("0", Attr(older, "data-index"));
        }, TimeSpan.FromSeconds(5));
    }

    // PR #357 round-3 (Codex): focus moving BETWEEN two focusable controls inside the same
    // expanded group (e.g. tabbing from one toast's close button to another's) bubbles a
    // focusout from the first control immediately followed by a focusin on the second — both
    // dispatch synchronously in that order. The group must not flicker/collapse in between.
    [Fact]
    public async Task Focus_Moving_Between_Two_Controls_In_The_Group_Never_Collapses_It()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show(new ToastOptions { Title = "A", Duration = 60000, Dismissible = true });
        toastService.Show(new ToastOptions { Title = "B", Duration = 60000, Dismissible = true });

        cut.WaitForAssertion(() =>
            Assert.Equal(2, cut.FindAll("[role='alert'],[role='status']").Count));

        Assert.Equal(2, cut.FindAll("[role='alert'],[role='status'] button").Count); // one close button per toast

        // Focus lands on the FIRST toast's close button — group expands. Each interaction
        // RE-QUERIES the button immediately before acting on it (not a cached reference from
        // before the previous FocusIn/FocusOut's own re-render) — a stale element's event
        // handler id is no longer valid once the group's markup has re-rendered, same reasoning
        // as the "Two"/"Four" re-find above, just without a background timer in the mix here.
        cut.FindAll("[role='alert'],[role='status'] button")[0].FocusIn();
        cut.WaitForAssertion(() =>
            Assert.Equal("true", Attr(cut.Find("[data-stacked='true']"), "data-expanded")));

        // Tab to the SECOND toast's close button: focusout on the first, focusin on the second,
        // back-to-back, with NO intervening wait — the deferred-collapse guard (OnFocusIn
        // cancelling any pending OnFocusOut collapse) must keep the group expanded the whole way
        // through, never rendering data-expanded="false" for even one frame in between.
        cut.FindAll("[role='alert'],[role='status'] button")[0].FocusOut();
        cut.FindAll("[role='alert'],[role='status'] button")[1].FocusIn();
        Assert.Equal("true", Attr(cut.Find("[data-stacked='true']"), "data-expanded"));

        // Give the (0ms, but still asynchronous) deferred-collapse timer every chance to fire if
        // it were somehow still pending — it must NOT be, because focusing button[1] already
        // cancelled it. The group stays expanded well past the timer's window.
        await Task.Delay(100);
        Assert.Equal("true", Attr(cut.Find("[data-stacked='true']"), "data-expanded"));

        // Focus leaving the group entirely (no focusin follows) DOES still collapse it — the fix
        // only suppresses the collapse when a focusin for the SAME group lands right after.
        cut.FindAll("[role='alert'],[role='status'] button")[1].FocusOut();
        cut.WaitForAssertion(() =>
            Assert.Equal("false", Attr(cut.Find("[data-stacked='true']"), "data-expanded")));
    }

    // PR #357 (Codex finding): ToastViewport.Dispose() used to tear down without notifying
    // OnExpandedChanged(false) when the group was still expanded at teardown. A non-default-
    // position viewport is only rendered while its group is non-empty (@key diffing in the
    // render loop above) — hovering it, then dismissing its LAST toast while still hovered, tore
    // the instance down with no mouseleave/focusout ever firing. ToastProvider's _expandedGroups
    // kept that position marked expanded forever afterward, so the NEXT toast shown at the same
    // position started (and stayed) paused with PauseReason.Group, never auto-dismissing.
    [Fact]
    public void Toast_Auto_Dismisses_After_Its_Groups_Viewport_Was_Disposed_While_Expanded()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>(); // default Position = BottomRight

        // Non-default position: its ToastViewport only renders while the group is non-empty.
        toastService.Show(new ToastOptions
        {
            Title = "Solo", Duration = 60000, Position = ToastPosition.TopLeft, Dismissible = true,
        });

        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("[role='alert'],[role='status']")));

        // Hover the TopLeft group's own viewport container (the toast's direct DOM parent).
        // data-stack-edge is stable/unique to the TopLeft group throughout (BottomRight's default,
        // always-rendered viewport emits "up") — re-queried fresh each time via cut.Find rather than
        // reusing a captured element reference, which a re-render (post-hover, post-dismiss) can
        // leave stale, same as every other hover test in this file.
        cut.Find("[role='alert'],[role='status']").ParentElement!.MouseEnter();
        cut.WaitForAssertion(() =>
            Assert.Equal("true", Attr(cut.Find("[data-stack-edge='down']"), "data-expanded")));

        // Dismiss the group's only toast WHILE still hovered — once its exit finishes, this
        // ToastViewport instance is disposed (positionsToRender drops TopLeft) with no
        // mouseleave/focusout ever having fired.
        cut.Find("[role='alert'],[role='status'] button").Click();
        cut.WaitForAssertion(() =>
            Assert.Empty(cut.FindAll("[role='alert'],[role='status']")),
            TimeSpan.FromSeconds(5));

        // A fresh toast lands at the SAME position with a short real duration. Pre-fix, it starts
        // paused with PauseReason.Group from the stale _expandedGroups entry and never resumes —
        // nothing here ever hovers/focuses it. It must auto-dismiss on its own.
        toastService.Show(new ToastOptions { Title = "Next", Duration = 100, Position = ToastPosition.TopLeft });
        cut.WaitForAssertion(() =>
            Assert.Contains(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("Next")));

        cut.WaitForAssertion(() =>
            Assert.DoesNotContain(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("Next")),
            TimeSpan.FromSeconds(5));
    }

    // PR #357 finding (review, round-13): the DEFAULT-position viewport is ALWAYS rendered, even
    // at ToastCount=0 (see the rc.42 doc comment on the render loop above) — unlike a non-default
    // viewport, it never disposes when its last toast goes away, so the false-edge notification
    // ToastViewport.Dispose() fires (covered by the test above) never runs for it. Dismissing the
    // default group's last toast via DismissAll (no mouseleave/focusout edge at all — the pointer
    // never moves) while the group is still hovered used to leave ToastProvider's _expandedGroups
    // holding the default position forever, so the next default-position toast started (and
    // stayed) paused with PauseReason.Group and never auto-dismissed.
    [Fact]
    public void Toast_Auto_Dismisses_After_DismissAll_Empties_A_Still_Hovered_Default_Group()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>(); // default Position = BottomRight

        toastService.Show(new ToastOptions { Title = "Solo", Duration = 60000 });

        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("[role='alert'],[role='status']")));

        // Hover the default group's own viewport container — data-stack-edge="up" is unique to
        // the always-rendered default (BottomRight) viewport throughout this test, since no
        // non-default position is ever used.
        cut.Find("[role='alert'],[role='status']").ParentElement!.MouseEnter();
        cut.WaitForAssertion(() =>
            Assert.Equal("true", Attr(cut.Find("[data-stack-edge='up']"), "data-expanded")));

        // Empty the group via DismissAll WHILE still hovered — the pointer never leaves, so no
        // mouseleave ever fires, and the always-rendered default viewport never disposes either.
        toastService.DismissAll();
        cut.WaitForAssertion(() =>
            Assert.Empty(cut.FindAll("[role='alert'],[role='status']")),
            TimeSpan.FromSeconds(5));

        // A fresh toast lands at the same (default) position with a short real duration. Pre-fix,
        // it starts paused with PauseReason.Group from the stale _expandedGroups entry and never
        // resumes — nothing here ever hovers/focuses it again. It must auto-dismiss on its own.
        toastService.Show(new ToastOptions { Title = "Next", Duration = 100 });
        cut.WaitForAssertion(() =>
            Assert.Contains(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("Next")));

        cut.WaitForAssertion(() =>
            Assert.DoesNotContain(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("Next")),
            TimeSpan.FromSeconds(5));
    }

    // PR #357 round-14 (P2 — Codex): a queued REPLACEMENT admitted by ReconcileGroup's own
    // eviction loop (MaxToasts=1: hover toast A, then Show() toast B — B evicts A to make room)
    // used to lose the group's pause state. RemoveWithExitAsync (evicting A) used to clear
    // ToastProvider's _expandedGroups the instant `_toasts` had no more entries at that position —
    // which, mid-reconcile, is true even though B is about to be mounted into the SAME position by
    // the very reconcile pass driving the eviction. B's StartOrPauseTimer then saw the position as
    // no longer expanded and started its timer live, even though the viewport was never actually
    // un-hovered (same DOM element, no mouseleave ever fired). B could auto-dismiss while the
    // pointer was still sitting in the expanded group.
    [Fact]
    public async Task Queued_Replacement_Admitted_By_Eviction_Stays_Paused_While_Its_Group_Is_Still_Hovered()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>(p => p.Add(x => x.MaxToasts, 1));

        toastService.Show(new ToastOptions { Title = "A", Duration = 60000 });
        cut.WaitForAssertion(() =>
            Assert.Contains(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("A")));

        // Hover the (default, always-rendered) group's own viewport container.
        cut.Find("[role='alert'],[role='status']").ParentElement!.MouseEnter();
        cut.WaitForAssertion(() =>
            Assert.Equal("true", Attr(cut.Find("[data-stack-edge='up']"), "data-expanded")));

        // Showing B at MaxToasts=1 evicts A (its ~220ms exit) and, once that completes, admits B
        // into the freed slot — all within the SAME ReconcileGroup call the eviction started, with
        // the pointer never having left the group.
        toastService.Show(new ToastOptions { Title = "B", Duration = 100 });
        cut.WaitForAssertion(() =>
            Assert.Contains(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("B")),
            TimeSpan.FromSeconds(2));

        // Wait well past B's 100ms duration (and past A's ~220ms exit) while STILL hovered —
        // pre-fix, B's timer started running unpaused the instant it was admitted (the stale
        // `_expandedGroups` clear from A's eviction already ran by then) and would have
        // auto-dismissed here even though the pointer never left the group.
        await Task.Delay(400);
        Assert.Contains(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("B"));

        // Leaving now resumes B's timer (it never ran while paused) — it auto-dismisses like any
        // other real-duration toast, proving this isn't just a timer that got lost.
        cut.Find("[role='alert'],[role='status']").ParentElement!.MouseLeave();
        cut.WaitForAssertion(() =>
            Assert.DoesNotContain(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("B")),
            TimeSpan.FromSeconds(5));
    }

    // ── Held-fill entrance class: never park animate-toast-in ──────────────
    //
    // animate-toast-in uses animation-fill-mode:both, so a settled entrance
    // permanently pins opacity:1/transform:none on the toast — overriding the
    // stacking transforms (translateY/scale) at equal specificity forever,
    // and racing animate-toast-out on dismiss (both classes present at once).
    // Toast.razor strips the class on its own animationend (with a timer
    // fallback for the no-DOM-animation-ever-fired case) — see
    // HandleEntranceAnimationEnd / FinishEntering.

    [Fact]
    public void Toast_Carries_Entrance_Class_Immediately_After_Mount()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show(new ToastOptions { Title = "Fresh", Duration = 60000 });

        cut.WaitForAssertion(() =>
        {
            var toast = cut.Find("[role='alert'],[role='status']");
            Assert.Contains("animate-toast-in", Attr(toast, "class") ?? "");
        });
    }

    [Fact]
    public void Entrance_Class_Is_Removed_After_The_Fallback_Window_Elapses()
    {
        // PR #357 round-4 (P1): Toast.razor no longer drives entrance-end cleanup
        // via a JS animationend callback (Interop.AttachToastEnterEnd /
        // IToastEnterCallback) — that interop surface would have to be brand-new
        // in every referenced Lumeo package, which a component vendored verbatim
        // into a consumer project can never assume. Cleanup is a plain local
        // timer now (Toast.razor's EnterFallbackMs), so this test waits for real
        // time to elapse instead of simulating a JS callback.
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show(new ToastOptions { Title = "Fresh", Duration = 60000 });

        cut.WaitForAssertion(() =>
            Assert.Contains("animate-toast-in", Attr(cut.Find("[role='alert'],[role='status']"), "class") ?? ""));

        cut.WaitForAssertion(() =>
        {
            var el = cut.Find("[role='alert'],[role='status']");
            Assert.DoesNotContain("animate-toast-in", Attr(el, "class") ?? "");
        }, TimeSpan.FromSeconds(2));

        // And it does NOT pick up animate-toast-out — this toast never left.
        Assert.DoesNotContain("animate-toast-out", Attr(cut.Find("[role='alert'],[role='status']"), "class") ?? "");
    }

    [Fact]
    public void Dismiss_During_Entrance_Strips_The_Entrance_Class_Immediately()
    {
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        // Duration 0 = manual dismiss only, so the close click is the only
        // thing that can end this toast — never a race with the auto-timer.
        toastService.Show(new ToastOptions { Title = "Fresh", Duration = 0 });

        cut.WaitForAssertion(() =>
            Assert.Contains("animate-toast-in", Attr(cut.Find("[role='alert'],[role='status']"), "class") ?? ""));

        // Dismiss WHILE still entering — no animationend was ever raised for
        // the entrance keyframe. Leaving must strip animate-toast-in on its
        // own (via OnParametersSet -> FinishEntering) rather than waiting for
        // an animationend that will never come once the class list has moved
        // on to animate-toast-out.
        cut.Find("[role='alert'],[role='status'] button").Click();

        cut.WaitForAssertion(() =>
        {
            var el = cut.FindAll("[role='alert'],[role='status']").SingleOrDefault(e => e.TextContent.Contains("Fresh"));
            Assert.NotNull(el);
            var cls = Attr(el!, "class") ?? "";
            Assert.Contains("animate-toast-out", cls);
            Assert.DoesNotContain("animate-toast-in", cls);
        });
    }

    [Fact]
    public void Entrance_Class_Clears_Via_Fallback_When_No_AnimationEnd_Ever_Fires()
    {
        // bUnit renders no real DOM/CSS, so a live browser's animationend
        // never fires here unless a test triggers it (as the two tests
        // above do). This exercises the OTHER path: the timer fallback that
        // guards the no-DOM-animation-ever-fired case (prerender, a toast
        // mounted inside a display:none ancestor, or — in a real browser —
        // prefers-reduced-motion environments that, for whatever reason,
        // never deliver the (still-fired, per lumeo.css's 1ms override)
        // animationend event).
        var toastService = GetToastService();
        var cut = _ctx.Render<L.ToastProvider>();

        toastService.Show(new ToastOptions { Title = "Fresh", Duration = 60000 });

        cut.WaitForAssertion(() =>
            Assert.Contains("animate-toast-in", Attr(cut.Find("[role='alert'],[role='status']"), "class") ?? ""));

        // No TriggerEvent call — rely solely on Toast's own fallback timer
        // (EnterFallbackMs, scheduled from OnAfterRender) to strip the class.
        cut.WaitForAssertion(() =>
        {
            var el = cut.Find("[role='alert'],[role='status']");
            Assert.DoesNotContain("animate-toast-in", Attr(el, "class") ?? "");
        }, timeout: TimeSpan.FromSeconds(2));
    }
}
