using Bunit;
using Xunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Lumeo.Tests.Components.PullToRefresh;

/// <summary>
/// Battle-test #117 (medium, keyboard-a11y) — refresh start/finish was never
/// announced to assistive tech. The visible spinner is a purely decorative
/// pull of <c>divs</c> with a spinning <c>RefreshCw</c> icon: it carried no
/// <c>role</c>/<c>aria-live</c>, and the icon was not marked
/// <c>aria-hidden</c>. A screen-reader user therefore got no feedback that a
/// refresh was in progress.
///
/// The fix wraps the announcement in a polite live region
/// (<c>role="status" aria-live="polite"</c>) that holds a localized
/// "Refreshing…" message ONLY while <c>_refreshing</c> is true (empty otherwise),
/// and marks the decorative icon <c>aria-hidden="true"</c>.
///
/// bUnit cannot drive real touch or move real focus, so — per the a11y test
/// rules — these assert the OBSERVABLE MARKUP MECHANISM (role / aria-live /
/// aria-hidden / live-region text) before, during, and after a gesture-driven
/// refresh, never real assistive-tech state.
/// </summary>
public class PullToRefreshA11yTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public PullToRefreshA11yTests()
    {
        _ctx.AddLumeoServices();
        // Last interface registration wins, so PullToRefresh resolves the spy.
        _ctx.Services.AddScoped<IComponentInteropService>(_ => _interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<Lumeo.PullToRefresh> Render(
        EventCallback onRefresh = default,
        double thresholdPx = 80) =>
        _ctx.Render<Lumeo.PullToRefresh>(p => p
            .Add(c => c.OnRefresh, onRefresh)
            .Add(c => c.ThresholdPx, thresholdPx)
            .AddChildContent("<p>content</p>"));

    [Fact]
    public void Exposes_A_Polite_Status_LiveRegion_That_Is_Empty_While_Idle()
    {
        var cut = Render();

        // The live region must exist with the correct ARIA so the platform
        // accessibility tree treats it as a polite status announcer.
        var live = cut.Find("[role='status']");
        Assert.Equal("polite", live.GetAttribute("aria-live"));

        // Nothing is announced until a refresh actually runs.
        Assert.True(string.IsNullOrEmpty(live.TextContent.Trim()));
    }

    [Fact]
    public void Decorative_Spinner_Icon_Is_Hidden_From_Assistive_Tech()
    {
        var cut = Render();

        // The spinning RefreshCw glyph is purely visual; with the live region
        // carrying the announcement it must not be read out as well.
        var icon = cut.Find("svg");
        Assert.Equal("true", icon.GetAttribute("aria-hidden"));
    }

    [Fact]
    public async Task Announces_In_The_LiveRegion_While_Refreshing_Then_Clears()
    {
        // A handler we can hold open to observe the in-flight "refreshing" state,
        // mirroring the spinner-spins behaviour test.
        var gate = new TaskCompletionSource();
        var cb = EventCallback.Factory.Create(this, () => gate.Task);
        var cut = Render(onRefresh: cb, thresholdPx: 80);

        // Before any gesture the polite region is silent.
        Assert.True(string.IsNullOrEmpty(cut.Find("[role='status']").TextContent.Trim()));

        var root = cut.Find("div[id^='ptr']");
        root.PointerDown(new PointerEventArgs { PointerId = 1, ClientY = 0 });
        // Visual pull is delta × 0.5; ≥160px finger travel clears the 80px threshold.
        root.PointerMove(new PointerEventArgs { PointerId = 1, ClientY = 220 });
        // Don't await — the handler is still pending on the gate.
        var pointerUp = root.PointerUpAsync(new PointerEventArgs { PointerId = 1, ClientY = 220 });

        // While the refresh handler is in flight the live region announces it.
        cut.WaitForAssertion(() =>
            Assert.False(string.IsNullOrWhiteSpace(cut.Find("[role='status']").TextContent)));

        // Complete the handler; the announcement clears so it isn't re-read.
        gate.SetResult();
        await pointerUp;

        cut.WaitForAssertion(() =>
            Assert.True(string.IsNullOrEmpty(cut.Find("[role='status']").TextContent.Trim())));
    }
}
