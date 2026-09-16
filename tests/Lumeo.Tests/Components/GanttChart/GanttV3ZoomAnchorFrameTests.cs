using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Bunit;
using Lumeo.GanttV3;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.GanttV3;

/// <summary>
/// Issue #385 — the pointer-anchored zoom must not get a frame at the NEW scale
/// under the OLD scroll position.
///
/// Every recenter in GanttV3 is applied by an interop call issued from
/// <c>GanttTimeline.OnAfterRenderAsync</c>, i.e. a SECOND server-&gt;client
/// message sent only after the render batch that moved every bar to the new
/// scale has already been applied in the browser. For a wheel zoom that ordering
/// IS the bug: the guarantee is "the date under the cursor stays under the
/// cursor", and in the window between those two messages the browser shows the
/// new scale under the old <c>scrollLeft</c> — the anchored date jumps by the
/// whole scroll delta and only snaps back a round trip later. That is exactly
/// what <c>GanttV3WheelZoomTests.CtrlWheel_Anchors_The_Zoom_On_The_Pointer_Not_
/// The_Viewport_Center</c> intermittently measured in (drift 752px instead of
/// 40px, with a BIT-IDENTICAL scroll journal in passing and failing runs — the
/// write was always correct, it just had not landed yet).
///
/// The fix carries the same intent in the RENDER itself, as
/// <c>data-gantt-v3-zoom-anchor</c> on the timeline root, so gantt-v3.js can
/// apply it from the microtask that follows the very DOM patch that repaints the
/// bars. bUnit runs no JS, so what these tests pin is the half that makes that
/// possible and that a C# regression could silently break: the stamp is present
/// in the DOM, carries the EXACT pixel the (deliberately suspended) interop call
/// carries, and exists only for a live pointer-anchored intent.
///
/// The <c>TaskCompletionSource</c>-gated interop mirrors
/// <c>GanttV3MountScrollRaceTests</c>' own style: suspending
/// <c>GanttV3ScrollToOffsetAsync</c> reproduces the interleaving deterministically
/// — the scroll write provably has NOT landed while the assertions below run.
/// </summary>
public class GanttV3ZoomAnchorFrameTests : IAsyncLifetime
{
    private const string AnchorAttribute = "data-gantt-v3-zoom-anchor";

    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public GanttV3ZoomAnchorFrameTests()
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
    };

    private static string Px(double value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:0.###}");

    [Fact]
    public async Task The_Render_That_Repaints_The_New_Scale_Already_Carries_The_Anchor_Pixel()
    {
        var state = new GanttState();
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        // Suspend the anchoring scroll write. From here until the gate is
        // released, the interop call has been ISSUED (its arguments are
        // recorded) but has provably not landed — the exact window a real
        // browser spends showing the un-anchored frame.
        var gate = new TaskCompletionSource();
        _interop.GanttV3ScrollToOffsetGate = gate;
        try
        {
            await cut.InvokeAsync(() => _interop.SimulateGanttV3WheelZoom("Week", 500.0, 42.5));

            // The mode switch itself landed: the bars in the DOM are already
            // laid out at Week's own scale.
            Assert.Equal(L.GanttViewMode.Week, state.ViewMode);
            var call = Assert.Single(_interop.GanttV3ScrollToOffsetCalls);

            // ...and the SAME render carries the scroll position that anchors
            // them. Without the fix there is no such attribute at all and the
            // browser has nothing to apply until the suspended call above
            // eventually resolves — one full round trip of visible drift.
            var stamp = cut.Find($"[{AnchorAttribute}]").GetAttribute(AnchorAttribute);
            Assert.NotNull(stamp);
            var parts = stamp!.Split('|');
            Assert.Equal(3, parts.Length);
            // Segments 2 and 3 are the load-bearing pair: byte-for-byte the
            // targetX/offsetPx the interop backstop will write, so whichever of
            // the two paths applies first, the viewport lands on the identical
            // pixel and the other is an idempotent no-op — never a second move.
            Assert.Equal(Px(call.TargetX), parts[1]);
            Assert.Equal(Px(call.OffsetPx), parts[2]);
            Assert.Equal(Px(42.5), parts[2]); // the pointer's own local offset, forwarded verbatim
        }
        finally
        {
            _interop.GanttV3ScrollToOffsetGate = null;
            gate.TrySetResult();
        }
    }

    [Fact]
    public async Task The_Anchor_Stamp_Is_Cleared_Once_Its_Intent_Has_Been_Applied()
    {
        // The stamp is a ONE-SHOT, exactly like the scroll intent it mirrors
        // (GanttTimeline._lastConsumedScrollRequestId): a stamp that survived
        // its own intent would re-apply a stale scroll position on every later
        // render, and — because the JS side keys on the id — would also stop a
        // LATER zoom to the same pixel from being applied at all.
        var state = new GanttState();
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        await cut.InvokeAsync(() => _interop.SimulateGanttV3WheelZoom("Week", 500.0, 42.5));
        for (var i = 0; i < 20; i++) await Task.Yield();

        // The intent was consumed by the render pass above; one more render is
        // what proves the stamp does not survive it.
        cut.Render();
        Assert.Empty(cut.FindAll($"[{AnchorAttribute}]"));
    }

    [Fact]
    public void A_Toolbar_Driven_Zoom_Never_Stamps_An_Anchor()
    {
        // Negative control (and a scope guard): only a POINTER-anchored intent
        // — the one whose contract is "this exact pixel must not move" — gets
        // the in-render stamp. Every other recenter keeps its existing,
        // unchanged single-write path, so a stamp appearing here would mean the
        // offset-override gate had been wired unconditionally.
        var state = new GanttState();
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day)
            .Add(c => c.ShowZoomControl, true));

        cut.Find("button[aria-label='Zoom out']").Click();

        Assert.Equal(L.GanttViewMode.Week, state.ViewMode);
        Assert.Empty(cut.FindAll($"[{AnchorAttribute}]"));
    }

    [Fact]
    public void A_Chart_At_Rest_Carries_No_Anchor_Stamp()
    {
        // Disable-check for the two "Empty" assertions above: they must be able
        // to FAIL, i.e. the attribute is genuinely absent at rest rather than
        // the selector being wrong. Mount alone recenters through the
        // viewport-centered path, so nothing is stamped.
        var state = new GanttState();
        var cut = _ctx.Render<L.GanttChart>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Tasks, Fixture())
            .Add(c => c.ViewMode, L.GanttViewMode.Day));

        Assert.Empty(cut.FindAll($"[{AnchorAttribute}]"));
        Assert.Empty(_interop.GanttV3ScrollToOffsetCalls);
    }
}
