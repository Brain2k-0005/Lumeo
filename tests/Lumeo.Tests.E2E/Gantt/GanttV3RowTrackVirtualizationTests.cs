using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// v3-ONLY: drag-create row-track virtualization regression coverage (design
/// spec Phase 3, T1 — "Virtualize RowTrackItems the same way [as the bars]").
///
/// Before this fix, <c>GanttTimeline</c> rendered ONE <c>[data-gantt-row-track]</c>
/// div per <c>EffectiveRows</c> entry via a separate <c>@foreach</c> loop
/// rendered BEFORE the row canvas's <c>&lt;Virtualize&gt;</c> block entirely —
/// materializing all N of them on every render regardless of scroll position,
/// unlike the bars/tree rows beside them (already virtualized — Codex round 3,
/// P2 #1, pulled forward from Phase 3 T1; see <c>GanttV3StickyHeaderTests</c>'
/// own remarks on that fix and its identical hard-count pattern for bars/tree
/// rows, which this spec mirrors for the row-track divs). The track div now
/// renders INSIDE the same virtualized item as its own row's bar, so it is
/// virtualized in lockstep with it instead of separately.
///
/// v2 has no drag-create at all (a v3-only, REUI-parity addition — see
/// <c>Gantt3.AllowCreate</c>'s own remarks), so there is no v2 counterpart to
/// compare against here.
/// </summary>
public class GanttV3RowTrackVirtualizationTests : GanttParityTestBase
{
    [Fact]
    public async Task Fewer_row_track_divs_than_total_rows_materialize_for_the_tall_allow_create_fixture()
    {
        await GotoHost("/e2e/gantt-v3?fixture=tall&viewMode=Day&allowCreate=1");

        var scrollPane = Page.Locator("[data-testid='gantt-v3-root'] div[style*='overflow']").First;
        await scrollPane.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(scrollPane).ToHaveAttributeAsync("data-gantt-v3-initial-scroll", "done", new() { Timeout = 15000 });
        await scrollPane.EvaluateAsync("el => el.scrollLeft = 0");

        // Sanity check the fixture is ACTUALLY tall enough to need vertical
        // scrolling — same reasoning as GanttV3StickyHeaderTests' own sibling
        // bar/tree hard-count spec: asserting culling on a fixture that fits
        // entirely in the viewport would prove nothing.
        var scrollHeight = await scrollPane.EvaluateAsync<double>("el => el.scrollHeight");
        var clientHeight = await scrollPane.EvaluateAsync<double>("el => el.clientHeight");
        Assert.True(scrollHeight > clientHeight + 200,
            $"Tall fixture isn't actually tall enough to need vertical scrolling (scrollHeight={scrollHeight}, clientHeight={clientHeight}) — the regression this spec guards against can't be reproduced.");

        // TallFixture is 60 FLAT tasks (no GroupLabel/ParentId — see
        // GanttParityFixtures.TallFixture's own remarks), so every row is a
        // Task row: every [data-gantt-row-track] element that could possibly
        // render comes from the per-task-row track div this spec targets,
        // with no group-header stripe contribution to muddy the count.
        var tracks = Page.Locator("[data-testid='gantt-v3-root'] [data-gantt-row-track]");
        // Poll: the same mount-settle reasoning GanttV3StickyHeaderTests'
        // bar/tree assertions rely on — Virtualize can render a placeholder
        // pass before its real item count settles.
        await Assertions.Expect(tracks).Not.ToHaveCountAsync(60, new() { Timeout = 10000 });

        var trackCount = await tracks.CountAsync();
        Assert.True(trackCount > 0 && trackCount < 60,
            $"expected the row-track divs to virtualize (some but not all 60 materialized), got {trackCount}");
    }
}
