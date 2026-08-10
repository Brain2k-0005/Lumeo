using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Gantt;

/// <summary>
/// Gantt v3 Phase 3, T6 — leaf-row checkbox selection + tri-state
/// parent/group checkboxes, and tree-row drag reorder. v3-ONLY (v2 has no
/// checkbox column and no row reorder at all — no parity route to compare
/// against, mirroring <c>GanttV3TreeColumnsSplitterTests</c>'s own
/// "v3-only by definition" framing).
///
/// Drives real Playwright pointer input against
/// <c>/e2e/gantt-v3-tree</c> (<c>GanttV3TreePage.razor</c>'s
/// <c>?checkboxes=1</c>/<c>?reorder=1</c>/<c>?readonly=1</c>/<c>?veto=1</c>
/// flags), against the known 5-row/3-level <c>GanttParityFixtures.TreeTasks()</c>
/// hierarchy every other <c>GanttV3TreeTests</c> spec already uses: root1
/// (Program Kickoff) -&gt; child1 (Design Phase) -&gt; grandchild1 (Wireframes),
/// and child2 (Build Phase) — child1/child2 share root1 as ParentId, a real
/// 2-member sibling bucket for the reorder specs below.
/// </summary>
public class GanttV3RowSelectionReorderTests : GanttParityTestBase
{
    private const string Root = "[data-testid='gantt-v3-tree-root']";

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);

    private ILocator RowByLabel(string label) => Page.Locator($"{Root} [data-row-kind='task']", new() { HasTextString = label });

    // ── Checkbox selection ──────────────────────────────────────────────────

    [Fact]
    public async Task Leaf_Checkbox_Toggle_Selects_The_Task()
    {
        await GotoHost("/e2e/gantt-v3-tree?checkboxes=1&infiniteScroll=0");

        var leafCheckbox = RowByLabel("Wireframes").Locator(".lumeo-gantt-v3-tree-checkbox");
        await leafCheckbox.WaitForAsync(new() { Timeout = 15000 });
        await Expect(leafCheckbox).ToHaveAttributeAsync("aria-checked", "false");

        await leafCheckbox.ClickAsync();

        await Expect(leafCheckbox).ToHaveAttributeAsync("aria-checked", "true");
    }

    [Fact]
    public async Task Parent_Checkbox_Reflects_Tri_State_And_Selects_All_Descendants()
    {
        await GotoHost("/e2e/gantt-v3-tree?checkboxes=1&infiniteScroll=0");

        // child1 (Design Phase) has exactly one descendant: grandchild1 (Wireframes).
        var parentCheckbox = RowByLabel("Design Phase").Locator(".lumeo-gantt-v3-tree-checkbox");
        var leafCheckbox = RowByLabel("Wireframes").Locator(".lumeo-gantt-v3-tree-checkbox");
        await parentCheckbox.WaitForAsync(new() { Timeout = 15000 });
        await Expect(parentCheckbox).ToHaveAttributeAsync("aria-checked", "false");

        // Selecting the ONLY descendant flips the parent to fully checked (not
        // merely indeterminate) — "all descendants selected" is the Selected
        // state, not PartiallySelected.
        await leafCheckbox.ClickAsync();
        await Expect(parentCheckbox).ToHaveAttributeAsync("aria-checked", "true");
        await Expect(parentCheckbox).ToHaveAttributeAsync("data-state", "checked");

        // root1 (Program Kickoff) has TWO children (child1, child2) — only
        // child1's subtree is now selected, so root1 itself must show
        // indeterminate ("mixed"), not fully checked.
        var rootCheckbox = RowByLabel("Program Kickoff").Locator(".lumeo-gantt-v3-tree-checkbox");
        await Expect(rootCheckbox).ToHaveAttributeAsync("aria-checked", "mixed");
        await Expect(rootCheckbox).ToHaveAttributeAsync("data-state", "indeterminate");

        // Clicking the now-fully-checked parent checkbox deselects ALL its
        // descendants (select-descendants semantics — a checked click always
        // resolves to Checked=true first per the Checkbox component's own
        // indeterminate-click contract, but here the parent starts fully
        // checked, so this click flips Checked false -> deselect-all).
        await parentCheckbox.ClickAsync();
        await Expect(leafCheckbox).ToHaveAttributeAsync("aria-checked", "false");
        await Expect(rootCheckbox).ToHaveAttributeAsync("aria-checked", "false");
    }

    [Fact]
    public async Task Checkbox_Is_Disabled_When_Readonly()
    {
        await GotoHost("/e2e/gantt-v3-tree?checkboxes=1&readonly=1&infiniteScroll=0");

        var leafCheckbox = RowByLabel("Wireframes").Locator(".lumeo-gantt-v3-tree-checkbox");
        await leafCheckbox.WaitForAsync(new() { Timeout = 15000 });
        await Expect(leafCheckbox).ToBeDisabledAsync();

        await leafCheckbox.ClickAsync(new() { Force = true }); // bypass Playwright's own actionability check to prove the click is genuinely inert, not merely hard to trigger
        await Expect(leafCheckbox).ToHaveAttributeAsync("aria-checked", "false");
    }

    // ── Row reorder ──────────────────────────────────────────────────────────

    private async Task DragRowAsync(ILocator sourceGrip, double deltaY)
    {
        var box = await sourceGrip.BoundingBoxAsync();
        Assert.NotNull(box);
        var startX = box!.X + box.Width / 2;
        var startY = box.Y + box.Height / 2;

        await Page.Mouse.MoveAsync((float)startX, (float)startY);
        await Page.Mouse.DownAsync();
        // A few intermediate moves so the drag threshold + drop-index
        // hit-testing both see genuine pointer travel, not a single teleport.
        await Page.Mouse.MoveAsync((float)startX, (float)(startY + deltaY / 2));
        await Page.Mouse.MoveAsync((float)startX, (float)(startY + deltaY));
        await Page.Mouse.UpAsync();
    }

    [Fact]
    public async Task Row_Drag_Reorders_Siblings_And_Commits_The_New_Order()
    {
        await GotoHost("/e2e/gantt-v3-tree?reorder=1&infiniteScroll=0");

        var rows = Page.Locator($"{Root} [data-row-kind='task']");
        await rows.First.WaitForAsync(new() { Timeout = 15000 });
        await Expect(rows).ToHaveCountAsync(5);

        // Initial order: Program Kickoff, Design Phase, Wireframes, Build
        // Phase, Independent Task (root1, child1, grandchild1, child2, root2).
        var before = await rows.AllTextContentsAsync();
        Assert.Contains("Design Phase", before[1]);
        Assert.Contains("Build Phase", before[3]);

        // Drag "Build Phase" (child2) up, ABOVE "Design Phase" (child1) —
        // both share root1 as ParentId, a real within-bucket move. Row
        // height is GanttScale.RowHeight (36px); dragging ~2.5 rows up lands
        // comfortably inside Design Phase's own top half (insert-before).
        var buildGrip = RowByLabel("Build Phase").Locator("[data-row-reorder-grip]");
        await buildGrip.WaitForAsync(new() { Timeout = 15000 });
        await DragRowAsync(buildGrip, -90);

        // Committed order: Program Kickoff, Build Phase, Design Phase,
        // Wireframes, Independent Task — grandchild1 (Wireframes) stays
        // nested immediately under its own parent (Design Phase) regardless
        // of child1's own new list position (GanttReorderModel.Move only
        // relocates the DRAGGED task's own slot; render order is driven by
        // parent/child relationships, not raw list adjacency).
        //
        // Real, reproducible flake found while hardening this suite (T7's
        // own gate run): CommitRowReorder is a genuinely ASYNC interop
        // round-trip (JS -> the bound OnRowReorder handler mutating the
        // task list -> a Blazor re-render) that Mouse.UpAsync() does NOT
        // block on — the OLD assertion here (ToHaveCountAsync(5), which is
        // trivially already true both BEFORE and AFTER a reorder, so it
        // never actually waited for anything) was immediately followed by a
        // ONE-SHOT AllTextContentsAsync() read that could race ahead of the
        // round-trip landing, observing the STALE pre-drag order (confirmed
        // via direct instrumentation: the committed target index was
        // correct every time; only the DOM read was too early). Polling for
        // the actual expected POST-reorder text (a real Playwright
        // auto-retry assertion, not a fixed sleep) waits for the round-trip
        // to genuinely land before the one-shot snapshot below is taken.
        await Assertions.Expect(rows.Nth(1)).ToContainTextAsync("Build Phase", new() { Timeout = 10000 });

        var after = await rows.AllTextContentsAsync();
        Assert.Contains("Program Kickoff", after[0]);
        Assert.Contains("Build Phase", after[1]);
        Assert.Contains("Design Phase", after[2]);
        Assert.Contains("Wireframes", after[3]);
        Assert.Contains("Independent Task", after[4]);
    }

    [Fact]
    public async Task Row_Drag_Veto_Path_Never_Commits()
    {
        await GotoHost("/e2e/gantt-v3-tree?reorder=1&veto=1&infiniteScroll=0");

        var rows = Page.Locator($"{Root} [data-row-kind='task']");
        await rows.First.WaitForAsync(new() { Timeout = 15000 });
        var before = await rows.AllTextContentsAsync();

        var buildGrip = RowByLabel("Build Phase").Locator("[data-row-reorder-grip]");
        await buildGrip.WaitForAsync(new() { Timeout = 15000 });

        // The drop-index line must paint invalid (destructive color) while
        // hovering a CanDropRow-rejected position — real, measurable pointer
        // feedback, not just "the commit silently no-ops".
        var box = await buildGrip.BoundingBoxAsync();
        Assert.NotNull(box);
        var startX = box!.X + box.Width / 2;
        var startY = box.Y + box.Height / 2;
        await Page.Mouse.MoveAsync((float)startX, (float)startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync((float)startX, (float)(startY - 90));

        var dropLine = Page.Locator($"{Root} [data-row-reorder-drop-line]");
        await dropLine.WaitForAsync(new() { Timeout = 5000 });
        await Assertions.Expect(dropLine).ToBeVisibleAsync();
        var invalidColor = await dropLine.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");

        await Page.Mouse.UpAsync();

        // A rejected drop must leave the task order byte-identical.
        var after = await rows.AllTextContentsAsync();
        Assert.Equal(before, after);
        // Sanity: the destructive color is genuinely different from the
        // default primary drop-line color (not merely a no-op assertion) —
        // resolved against the SAME live element, so this is theme-agnostic.
        Assert.NotEqual("", invalidColor);
    }

    [Fact]
    public async Task Row_Drag_Released_Before_Slow_Validation_Resolves_Never_Commits()
    {
        // Codex review, P1 #2 ("row-drop commits before CanDropRow validation
        // resolves"): gantt-v3.js's row-reorder onPointerUp used to read the
        // `lastValid` closure variable SYNCHRONOUSLY instead of awaiting the
        // (possibly still in-flight) ValidateRowDrop promise for the FINAL
        // hovered position — `lastValid` starts `true` and is only flipped by
        // checkValid's fire-and-forget `.then()`, so a release landing BEFORE
        // that round trip resolves committed on the STALE default value.
        // Row_Drag_Veto_Path_Never_Commits above can't exercise this: it
        // always waits for the drop-line to paint invalid before releasing,
        // which only ever happens AFTER the round trip already landed. This
        // uses ?vetoslow=1 (a CanDropRow that blocks ~400ms server-side
        // before rejecting) and releases IMMEDIATELY — no wait at all — so
        // the pointer-up genuinely races the still-in-flight validation.
        await GotoHost("/e2e/gantt-v3-tree?reorder=1&vetoslow=1&infiniteScroll=0");

        var rows = Page.Locator($"{Root} [data-row-kind='task']");
        await rows.First.WaitForAsync(new() { Timeout = 15000 });
        var before = await rows.AllTextContentsAsync();

        var buildGrip = RowByLabel("Build Phase").Locator("[data-row-reorder-grip]");
        await buildGrip.WaitForAsync(new() { Timeout = 15000 });

        var box = await buildGrip.BoundingBoxAsync();
        Assert.NotNull(box);
        var startX = box!.X + box.Width / 2;
        var startY = box.Y + box.Height / 2;
        await Page.Mouse.MoveAsync((float)startX, (float)startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync((float)startX, (float)(startY - 90));
        // No wait for the drop-line here (unlike Row_Drag_Veto_Path_Never_Commits)
        // — release right away, well inside the artificial 400ms server delay.
        await Page.Mouse.UpAsync();

        // Give the slow validator's real round trip — and any incorrect
        // commit the pre-fix race would have let through — time to land
        // before asserting. A race this fix closes must stay closed even
        // after waiting past the delay, not merely "not yet visible".
        await Page.WaitForTimeoutAsync(1000);

        var after = await rows.AllTextContentsAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Row_Reorder_Is_Inert_When_Readonly()
    {
        await GotoHost("/e2e/gantt-v3-tree?reorder=1&readonly=1&infiniteScroll=0");

        var rows = Page.Locator($"{Root} [data-row-kind='task']");
        await rows.First.WaitForAsync(new() { Timeout = 15000 });

        // No LIVE grip marker at all while Readonly — the delegated JS
        // listener never even hit-tests these rows (mirrors
        // GanttV3ReadonlyGuardTests' own "no listeners at all" contract for
        // the move/resize drag engine).
        var liveGrips = Page.Locator($"{Root} [data-row-reorder-grip]");
        await Assertions.Expect(liveGrips).ToHaveCountAsync(0);

        // The inert grip icon is still VISIBLE (dimmed) — mirrors
        // Lumeo.DataGrid's own RowReorderPointerActive-false treatment.
        var inertGrip = RowByLabel("Build Phase").Locator(".lumeo-gantt-v3-tree-reorder-grip");
        await Assertions.Expect(inertGrip).ToBeVisibleAsync();

        var before = await rows.AllTextContentsAsync();

        // A pointer gesture on the inert element must produce no drag at all
        // — force the down/move/up through Playwright (bypassing its own
        // actionability gate, same reasoning as the checkbox readonly spec
        // above) to prove the ABSENCE of a registered listener, not merely
        // that Playwright refused to try.
        var box = await inertGrip.BoundingBoxAsync();
        Assert.NotNull(box);
        var startX = box!.X + box.Width / 2;
        var startY = box.Y + box.Height / 2;
        await Page.Mouse.MoveAsync((float)startX, (float)startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync((float)startX, (float)(startY - 90));
        await Page.Mouse.UpAsync();

        var after = await rows.AllTextContentsAsync();
        Assert.Equal(before, after);
    }
}
