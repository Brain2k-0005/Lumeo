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
        await items.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await items.First.ScrollIntoViewIfNeededAsync();
        var firstBefore = (await items.Nth(0).InnerTextAsync()).Trim();

        // Native HTML5 drag-and-drop is unreliable to drive with synthetic pointer
        // input in headless Chromium (DragToAsync flakes). The Sortable listens to the
        // standard *bubbling* drag events (@ondragstart/@ondragover/@ondrop), so
        // dispatch them directly with a shared DataTransfer — deterministic, and they
        // bubble to Blazor's delegated handler. Drag item 0 onto item 2.
        await Page.EvaluateAsync(@"() => {
            const els = document.querySelectorAll('[draggable=true]');
            if (els.length < 3) return;
            const src = els[0], dst = els[2];
            const dt = new DataTransfer();
            const fire = (el, type) => el.dispatchEvent(new DragEvent(type, { bubbles: true, cancelable: true, dataTransfer: dt }));
            fire(src, 'dragstart');
            fire(dst, 'dragenter');
            fire(dst, 'dragover');
            fire(dst, 'drop');
            fire(src, 'dragend');
        }");

        // Give the reorder a tick to settle.
        await Page.WaitForFunctionAsync(
            "(before) => { const el = document.querySelectorAll('[draggable=true]')[0]; return el && el.innerText.trim() !== before; }",
            firstBefore, new() { Timeout = 15000 });

        var firstAfter = (await items.Nth(0).InnerTextAsync()).Trim();
        Assert.NotEqual(firstBefore, firstAfter);
    }
}
