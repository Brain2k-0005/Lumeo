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

        // Widen the exit window through the internal test seam: the deliberate
        // 150ms mid-exit gap below raced the real 220ms timer (70ms margin) and
        // still flaked under full-suite starvation. 800ms gives the same
        // overlap semantics with a 650ms margin; the CSS animation length is
        // irrelevant to the frozen-index bookkeeping under test.
        cut.Instance.ExitAnimationMs = 800;

        var two = cut.FindAll("[role='alert'],[role='status']").Single(e => e.TextContent.Contains("Two"));
        Assert.Equal("3", Attr(two, "data-index"));

        // "Four" starts leaving FIRST, at T0.
        var four = cut.FindAll("[role='alert'],[role='status']").Single(e => e.TextContent.Contains("Four"));
        four.QuerySelector("button")!.Click();
        // Explicit generous timeout (not bUnit's 1s default): this test's whole point is real
        // overlapping 220ms exit timers, and the full local/CI suite runs thousands of tests in
        // parallel — under that contention, the thread pool can genuinely take longer than 1s to
        // service a background render. A longer timeout costs nothing on the happy path (each
        // WaitForAssertion still returns the instant the assertion first passes).
        cut.WaitForAssertion(() =>
        {
            var leaving = cut.FindAll("[role='alert'],[role='status']").SingleOrDefault(e => e.TextContent.Contains("Four"));
            Assert.NotNull(leaving);
            Assert.Contains("animate-toast-out", Attr(leaving!, "class") ?? "");
        }, TimeSpan.FromSeconds(5));

        // Deliberate fixed real-time gap (well under the 220ms exit window —
        // ToastProvider.ExitAnimationMs) before starting "Two"'s own exit, so
        // the two overlapping animations have a KNOWN, comfortable ordering
        // margin instead of an incidental few-millisecond gap that a busy CI
        // runner (many tests in parallel) can easily erase — that's exactly
        // what made an earlier version of this test flaky under full-suite
        // load. `Task.Delay` (not `Thread.Sleep`): a blocking sleep would tie
        // up a whole thread-pool worker for 150ms doing nothing, which under
        // xUnit's parallel test execution can itself starve OTHER tests'
        // background Task.Delay continuations — an unrelated hover-delay test
        // elsewhere in the suite flaked from exactly that when this used
        // Thread.Sleep. "Four" is
        // still present (Leaving, not yet removed — 150 < 800), so the group
        // hasn't reshuffled yet and "Two"'s frozen index is correctly
        // captured as 3.
        await Task.Delay(150);

        // Re-find "Two" (not the `two` reference captured before "Four"'s
        // dismissal re-rendered the tree — its event handler id is stale now)
        // AND click it inside the SAME InvokeAsync dispatch. "Four" is mid-exit
        // with its own live 220ms timer, so a background render from that
        // timer's machinery (PauseTimer/ResumeTimer, StateHasChanged, …) can
        // land on bUnit's renderer thread in the split second between a plain
        // Find and a separate Click call, invalidating the just-found button's
        // event handler id (UnknownEventHandlerIdException — flaked in CI:
        // https://github.com/Brain2k-0005/Lumeo/actions — "Four" and "Two"'s
        // independent timers overlapping is the whole point of this test, so
        // the race is real, not incidental). Doing the find-then-click as one
        // synchronous unit on the renderer's own dispatcher — bUnit's own
        // documented workaround for this exception — closes that window.
        await cut.InvokeAsync(() =>
            cut.FindAll("[role='alert'],[role='status']").Single(e => e.TextContent.Contains("Two"))
                .QuerySelector("button")!.Click());
        cut.WaitForAssertion(() =>
        {
            var leaving = cut.FindAll("[role='alert'],[role='status']").SingleOrDefault(e => e.TextContent.Contains("Two"));
            Assert.NotNull(leaving);
            Assert.Contains("animate-toast-out", Attr(leaving!, "class") ?? "");
            Assert.Equal("3", Attr(leaving!, "data-index"));
        }, TimeSpan.FromSeconds(5));

        // Wait for "Four" to fully finish its exit and unmount — a newer
        // sibling actually leaving the list, not just being marked Leaving.
        // "Four" started ~150ms before "Two", so it reaches its own 220ms
        // mark ~150ms before "Two" would reach its (independent) 220ms mark —
        // "Two" should still be present and still mid-exit right here.
        cut.WaitForAssertion(() =>
            Assert.DoesNotContain(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("Four")),
            TimeSpan.FromSeconds(5));

        // The regression: "Two" must NOT have been promoted to a visible
        // index by "Four"'s removal — its data-index stays frozen at 3 for
        // the rest of ITS OWN exit, so the CSS depth>=3 (visibility:hidden)
        // rule keeps matching it throughout.
        var twoStillLeaving = cut.FindAll("[role='alert'],[role='status']").SingleOrDefault(e => e.TextContent.Contains("Two"));
        Assert.NotNull(twoStillLeaving);
        Assert.Equal("3", Attr(twoStillLeaving!, "data-index"));

        // Let "Two"'s own exit finish too — no leftover element, no stale index.
        cut.WaitForAssertion(() =>
            Assert.DoesNotContain(cut.FindAll("[role='alert'],[role='status']"), e => e.TextContent.Contains("Two")),
            TimeSpan.FromSeconds(5));
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
