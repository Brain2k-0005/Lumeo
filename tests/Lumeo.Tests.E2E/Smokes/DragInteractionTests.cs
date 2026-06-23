using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Real pointer-drag interactions bUnit cannot perform (no real input device): the
/// Splitter divider resizes panes when dragged, and a Sortable list reorders when an
/// item is dragged. These are the drag behaviours the components exist for.
///
/// Requires the docs dev-server. See project README.md.
/// </summary>
public class DragInteractionTests : PlaywrightTestBase
{
    [Fact]
    public async Task Dragging_the_splitter_separator_moves_it()
    {
        await Goto("/components/splitter");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var sep = Page.Locator("[role=separator]").First;
        await sep.ScrollIntoViewIfNeededAsync();

        var before = await sep.BoundingBoxAsync();
        Assert.NotNull(before);

        // Drag the divider ~90px to the right.
        await Page.Mouse.MoveAsync(before!.X + before.Width / 2, before.Y + before.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(before.X + 90, before.Y + before.Height / 2, new() { Steps = 10 });
        await Page.Mouse.UpAsync();

        var after = await sep.BoundingBoxAsync();
        Assert.NotNull(after);
        Assert.True(Math.Abs(after!.X - before.X) > 20,
            $"Splitter separator did not move on drag (before X={before.X}, after X={after.X}) — drag-resize not working.");
    }

    [Fact]
    public async Task Dragging_a_sortable_item_reorders_the_list()
    {
        await Goto("/components/sortable");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var items = Page.Locator("[draggable=true]");
        await items.First.ScrollIntoViewIfNeededAsync();
        var firstBefore = (await items.Nth(0).InnerTextAsync()).Trim();

        // Drag the first item down onto the third — its text should leave slot 0.
        await items.Nth(0).DragToAsync(items.Nth(2));

        // Give the reorder a tick to settle.
        await Page.WaitForFunctionAsync(
            "(before) => { const el = document.querySelectorAll('[draggable=true]')[0]; return el && el.innerText.trim() !== before; }",
            firstBefore, new() { Timeout = 5000 });

        var firstAfter = (await items.Nth(0).InnerTextAsync()).Trim();
        Assert.NotEqual(firstBefore, firstAfter);
    }
}
