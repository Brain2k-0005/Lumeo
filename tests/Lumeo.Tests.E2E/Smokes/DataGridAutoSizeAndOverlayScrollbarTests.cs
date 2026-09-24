using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Issue #519 (bulk column auto-size) and #517 (overlay scrollbar) — both features
/// live largely in JS (real DOM measurement, real fixed-position thumbs synced to a
/// real scroll container), so bUnit coverage (<c>DataGridAutoSizeTests</c>,
/// <c>DataGridOverlayScrollbarTests</c>) only exercises the C# side against a fake
/// interop. These two smokes drive the actual docs demos
/// (<c>docs/Lumeo.Docs/Pages/Components/DataGridPage.razor</c>, sections
/// <c>#autosize-columns</c> / <c>#overlay-scrollbar</c>) in a real Chromium engine.
/// </summary>
public class DataGridAutoSizeAndOverlayScrollbarTests : PlaywrightTestBase
{
    [Fact]
    public async Task AutoSizeAllColumns_Grows_A_Truncated_Column_To_Fit_Its_Longest_Value()
    {
        await Goto("/components/datagrid");
        var section = Page.Locator("#autosize-columns");
        await section.ScrollIntoViewIfNeededAsync();
        await Page.WaitForSelectorAsync("#autosize-columns table");

        // "Department" starts at Width=80/MinWidth=60 — narrower than "Engineering"/"Marketing".
        var header = section.Locator("th[data-slot='datagrid-header-cell']", new() { HasText = "Department" });
        var widthBefore = (await header.BoundingBoxAsync())!.Width;

        await section.GetByRole(AriaRole.Button, new() { Name = "Autosize all columns" }).ClickAsync();
        await Page.WaitForTimeoutAsync(300); // measure + commit + re-render round trip

        var widthAfter = (await header.BoundingBoxAsync())!.Width;
        Assert.True(widthAfter > widthBefore + 15,
            $"Department column did not grow to fit its content: {widthBefore} -> {widthAfter}");

        // No text is clipped anymore: the widest cell's scrollWidth now fits its clientWidth.
        var stillClipped = await section.Locator("td", new() { HasText = "Engineering" }).First
            .EvaluateAsync<bool>("el => el.scrollWidth > el.clientWidth + 1");
        Assert.False(stillClipped, "a Department cell still overflows its own box after autosize");
    }

    [Fact]
    public async Task ResetColumnWidths_Restores_The_Declared_Width_After_AutoSize()
    {
        await Goto("/components/datagrid");
        var section = Page.Locator("#autosize-columns");
        await section.ScrollIntoViewIfNeededAsync();
        await Page.WaitForSelectorAsync("#autosize-columns table");

        var header = section.Locator("th[data-slot='datagrid-header-cell']", new() { HasText = "Department" });
        var declaredWidth = (await header.BoundingBoxAsync())!.Width;

        await section.GetByRole(AriaRole.Button, new() { Name = "Autosize all columns" }).ClickAsync();
        await Page.WaitForTimeoutAsync(300);
        var grownWidth = (await header.BoundingBoxAsync())!.Width;
        Assert.True(grownWidth > declaredWidth + 15);

        await section.GetByRole(AriaRole.Button, new() { Name = "Reset widths" }).ClickAsync();
        await Page.WaitForTimeoutAsync(200);
        var resetWidth = (await header.BoundingBoxAsync())!.Width;

        Assert.True(Math.Abs(resetWidth - declaredWidth) < 2,
            $"width did not return to its declared value: declared {declaredWidth}, after reset {resetWidth}");
    }

    [Fact]
    public async Task Overlay_Scrollbar_Vertical_Thumb_Drag_Scrolls_The_Grid()
    {
        await Goto("/components/datagrid");
        var section = Page.Locator("#overlay-scrollbar");
        await section.ScrollIntoViewIfNeededAsync();
        await Page.WaitForSelectorAsync("#overlay-scrollbar .lumeo-dg-overlay-scroll");

        var viewportBox = await section.Locator(".lumeo-dg-overlay-scroll").BoundingBoxAsync();
        Assert.NotNull(viewportBox);

        // Reveal the overlay thumbs — they fade in on hover/scroll (pointer-events:none
        // otherwise, by design, so they never steal clicks from grid content underneath).
        await Page.Mouse.MoveAsync(viewportBox!.X + viewportBox.Width / 2, viewportBox.Y + viewportBox.Height / 2);
        await Page.WaitForTimeoutAsync(250);

        var vThumb = Page.Locator(".lumeo-dg-scrollbar-track-y .lumeo-dg-scrollbar-thumb");
        await vThumb.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var thumbBox = await vThumb.BoundingBoxAsync();
        Assert.NotNull(thumbBox);

        var scrollTopBefore = await section.Locator(".lumeo-dg-overlay-scroll")
            .EvaluateAsync<double>("el => el.scrollTop");
        Assert.Equal(0, scrollTopBefore);

        await Page.Mouse.MoveAsync(thumbBox!.X + thumbBox.Width / 2, thumbBox.Y + thumbBox.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(thumbBox.X + thumbBox.Width / 2, thumbBox.Y + thumbBox.Height / 2 + 60, new() { Steps = 8 });
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(150);

        var scrollTopAfter = await section.Locator(".lumeo-dg-overlay-scroll")
            .EvaluateAsync<double>("el => el.scrollTop");
        Assert.True(scrollTopAfter > scrollTopBefore + 10,
            $"dragging the vertical thumb down did not scroll the grid: {scrollTopBefore} -> {scrollTopAfter}");
    }

    [Fact]
    public async Task Overlay_Scrollbar_Horizontal_Thumb_Drag_Scrolls_The_Grid()
    {
        await Goto("/components/datagrid");
        var section = Page.Locator("#overlay-scrollbar");
        await section.ScrollIntoViewIfNeededAsync();
        await Page.WaitForSelectorAsync("#overlay-scrollbar .lumeo-dg-overlay-scroll");

        var viewportBox = await section.Locator(".lumeo-dg-overlay-scroll").BoundingBoxAsync();
        Assert.NotNull(viewportBox);

        await Page.Mouse.MoveAsync(viewportBox!.X + viewportBox.Width / 2, viewportBox.Y + viewportBox.Height / 2);
        await Page.WaitForTimeoutAsync(250);

        var hThumb = Page.Locator(".lumeo-dg-scrollbar-track-x .lumeo-dg-scrollbar-thumb");
        await hThumb.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var thumbBox = await hThumb.BoundingBoxAsync();
        Assert.NotNull(thumbBox);

        var scrollLeftBefore = await section.Locator(".lumeo-dg-overlay-scroll")
            .EvaluateAsync<double>("el => el.scrollLeft");
        Assert.Equal(0, scrollLeftBefore);

        await Page.Mouse.MoveAsync(thumbBox!.X + thumbBox.Width / 2, thumbBox.Y + thumbBox.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(thumbBox.X + thumbBox.Width / 2 + 80, thumbBox.Y + thumbBox.Height / 2, new() { Steps = 8 });
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(150);

        var scrollLeftAfter = await section.Locator(".lumeo-dg-overlay-scroll")
            .EvaluateAsync<double>("el => el.scrollLeft");
        Assert.True(scrollLeftAfter > scrollLeftBefore + 10,
            $"dragging the horizontal thumb right did not scroll the grid: {scrollLeftBefore} -> {scrollLeftAfter}");
    }

    [Fact]
    public async Task Overlay_Scrollbar_Native_Scrollbar_Is_Hidden()
    {
        // The whole point of #517: the native bar reserves no layout space. Compare the
        // viewport's own clientWidth against its offsetWidth — a visible native scrollbar
        // would make clientWidth smaller than offsetWidth by its own track width (~15-17px
        // on most platforms); with it hidden they're equal (barring border/padding, which
        // this element has none of).
        await Goto("/components/datagrid");
        var section = Page.Locator("#overlay-scrollbar");
        await section.ScrollIntoViewIfNeededAsync();
        await Page.WaitForSelectorAsync("#overlay-scrollbar .lumeo-dg-overlay-scroll");

        var diff = await section.Locator(".lumeo-dg-overlay-scroll")
            .EvaluateAsync<double>("el => el.offsetWidth - el.clientWidth");
        Assert.Equal(0, diff);
    }
}
