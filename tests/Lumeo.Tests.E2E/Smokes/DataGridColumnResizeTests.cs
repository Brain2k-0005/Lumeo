using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// #443 — dragging a column's right border could move its LEFT border instead.
///
/// This cannot be a bUnit test: the defect lives entirely in CSS table layout. The table is
/// <c>w-full</c> under the default <c>table-layout: auto</c>, where a width written onto a
/// cell is only a hint — the browser still has to make the row add up to the container, so
/// with no slack left it takes the space from whichever column has some, frequently one to
/// the LEFT of the handle. Only a real engine computing real box geometry can show it.
///
/// The setup matters as much as the assertion. My first attempt did NOT reproduce the bug,
/// because the table there was already wider than its container and scrolling — there was
/// slack, so auto layout behaved. The container has to be WIDER than the table's natural
/// width, so that <c>w-full</c> stretches every column and each one carries slack the
/// browser is willing to redistribute.
/// </summary>
public class DataGridColumnResizeTests : PlaywrightTestBase
{
    private sealed record Edge(double Left, double Right, double Width);

    private async Task<List<Edge>> HeaderEdgesAsync()
    {
        var json = await Page.EvaluateAsync<string>(@"() => {
            const ths = [...document.querySelectorAll('table thead tr:last-child th')];
            return JSON.stringify(ths.map(th => {
                const r = th.getBoundingClientRect();
                return { Left: r.left, Right: r.right, Width: r.width };
            }));
        }");

        return JsonSerializer.Deserialize<List<Edge>>(json)!;
    }

    [Fact]
    public async Task Dragging_A_Right_Border_Never_Moves_The_Columns_To_Its_Left()
    {
        await Goto("/components/datagrid/playground");
        await Page.WaitForSelectorAsync("table");
        await Page.WaitForTimeoutAsync(1000);

        // Give the columns slack: widen the scroll container past the table's natural width.
        await Page.EvaluateAsync(@"() => {
            const t = document.querySelector('table');
            const box = t.closest('.overflow-auto') || t.parentElement;
            box.style.width = '1400px';
            box.style.maxWidth = '1400px';
            for (const a of document.querySelectorAll('main, main *')) {
                if (a.contains(t) && a !== t) { a.style.maxWidth = 'none'; a.style.overflow = 'visible'; }
            }
        }");
        await Page.WaitForTimeoutAsync(300);

        var handles = Page.Locator("[aria-label*='esize']");
        if (await handles.CountAsync() < 4) return;   // no resizable columns on this page

        const int target = 2;
        var before = await HeaderEdgesAsync();

        var box = await handles.Nth(target).BoundingBoxAsync();
        Assert.NotNull(box);
        await Page.Mouse.MoveAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + box.Width / 2 + 90, box.Y + box.Height / 2, new() { Steps = 12 });
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(400);

        var after = await HeaderEdgesAsync();

        // The dragged column grew.
        Assert.True(after[target].Width > before[target].Width + 50,
            $"dragged column did not grow: {before[target].Width} -> {after[target].Width}");

        // And nothing to its left moved by even a pixel. This is the assertion that was
        // failing: column 1 measured 239 -> 220 before the fix, its right edge (= the dragged
        // column's LEFT border) walking 19px inwards.
        for (var i = 0; i < target; i++)
        {
            Assert.True(Math.Abs(after[i].Left - before[i].Left) < 1,
                $"column {i} moved left edge {before[i].Left} -> {after[i].Left}");
            Assert.True(Math.Abs(after[i].Width - before[i].Width) < 1,
                $"column {i} changed width {before[i].Width} -> {after[i].Width}");
        }

        // Columns to the right shift along with the wider table, but keep their own widths -
        // the table absorbs the delta, they do not.
        for (var i = target + 1; i < Math.Min(before.Count, after.Count); i++)
        {
            Assert.True(Math.Abs(after[i].Width - before[i].Width) < 1,
                $"column {i} changed width {before[i].Width} -> {after[i].Width}");
        }
    }
}
