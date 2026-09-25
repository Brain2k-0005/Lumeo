using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// The classic single-table DataGrid layout's OWN default fix (no parameter — see
/// DataGrid.razor's ViewportCssClass/WantsGridHeaderOffset): the header's background
/// band now runs the full width of the grid frame, including the native vertical
/// scrollbar's gutter, and — in Chromium/Safari, since LU-21 restored
/// <c>::-webkit-scrollbar*</c> recognition — the scrollbar thumb's own travel range
/// starts below the header instead of running alongside it. Drives
/// <c>#toolbar-with-internal-scroll</c> on the real docs DataGrid page (a toolbar
/// above the grid, matching the owner's screenshot) in a real Chromium engine.
/// </summary>
public class DataGridNativeHeaderBandTests : PlaywrightTestBase
{
    private static readonly string ScreenshotDir = Path.Combine(Path.GetTempPath(), "lumeo-e2e-grid-scrollbar");

    private async Task GotoDataGrid(string themeMode = "light")
    {
        await Page.AddInitScriptAsync($"try {{ localStorage.setItem('theme-mode', '{themeMode}'); }} catch (e) {{ }}");
        await Goto("/components/datagrid");
    }

    private ILocator Section => Page.Locator("#toolbar-with-internal-scroll");

    private async Task<ILocator> OpenSectionAsync()
    {
        var section = Section;
        await section.ScrollIntoViewIfNeededAsync();
        await Page.WaitForSelectorAsync("#toolbar-with-internal-scroll [data-slot='datagrid-viewport']");
        await Page.WaitForTimeoutAsync(150); // ResizeObserver header-offset measurement settles
        return section;
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Gutter_Strip_Pixel_Matches_The_Header_Background(string mode)
    {
        await GotoDataGrid(mode);
        var section = await OpenSectionAsync();

        var viewport = section.Locator("[data-slot='datagrid-viewport']");
        var box = (await viewport.BoundingBoxAsync())!;

        Directory.CreateDirectory(ScreenshotDir);
        var shotPath = Path.Combine(ScreenshotDir, $"default-gutter-sample-{mode}.png");
        await Page.ScreenshotAsync(new() { Path = shotPath });

        using var image = Image.Load<Rgba32>(shotPath);
        // Reference: well inside the header's own painted band, away from the table/text.
        // Gutter: a couple of px inside the viewport's own right edge, near the header's
        // vertical midpoint — this is the strip the native scrollbar's gutter occupies.
        var reference = image[(int)Math.Round(box.X + 6), (int)Math.Round(box.Y + 6)];
        var gutter = image[(int)Math.Round(box.X + box.Width - 2), (int)Math.Round(box.Y + 6)];

        Assert.True(ColorClose(gutter, reference),
            $"[{mode}] gutter pixel ({gutter.R},{gutter.G},{gutter.B}) should match header background ({reference.R},{reference.G},{reference.B})");
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Scrollbar_Thumb_Top_Is_At_Or_Below_The_Headers_Bottom_Edge(string mode)
    {
        await GotoDataGrid(mode);
        var section = await OpenSectionAsync();

        var viewport = section.Locator("[data-slot='datagrid-viewport']");
        var box = (await viewport.BoundingBoxAsync())!;
        var headerBottom = await section.Locator("[data-slot='datagrid-header']")
            .EvaluateAsync<double>("el => el.getBoundingClientRect().bottom");
        var scrollTop = await viewport.EvaluateAsync<double>("el => el.scrollTop");
        Assert.Equal(0, scrollTop); // the assertion below only means something at the top of the scroll range

        Directory.CreateDirectory(ScreenshotDir);
        var shotPath = Path.Combine(ScreenshotDir, $"default-thumb-sample-{mode}.png");
        await Page.ScreenshotAsync(new() { Path = shotPath });
        using var image = Image.Load<Rgba32>(shotPath);

        // lumeo.css: *::-webkit-scrollbar { width: 6px } — sample the column 3px inside the
        // viewport's own right edge (the middle of that 6px track) and walk down from the
        // viewport's own top, looking for the first pixel that stops being "header/body
        // background" — that row is the thumb's own top edge (the track itself stays
        // transparent, so above the thumb the gutter shows the painted band/body colour).
        var sampleX = (int)Math.Round(box.X + box.Width - 3);
        var backgroundRef = image[sampleX, (int)Math.Round(box.Y + 2)];
        int? thumbTopY = null;
        for (var y = (int)Math.Round(box.Y); y < (int)Math.Round(box.Y + box.Height); y++)
        {
            var px = image[sampleX, y];
            if (!ColorClose(px, backgroundRef, tolerance: 10))
            {
                thumbTopY = y;
                break;
            }
        }

        Assert.True(thumbTopY.HasValue, $"[{mode}] could not find the scrollbar thumb in the gutter column — is the grid tall enough to need a vertical scrollbar?");
        Assert.True(thumbTopY!.Value >= headerBottom - 1,
            $"[{mode}] scrollbar thumb top ({thumbTopY}) should be at or below the header's bottom edge ({headerBottom})");
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Corner_Screenshots_Before_And_After_The_Default_Band_Fix(string mode)
    {
        Directory.CreateDirectory(ScreenshotDir);
        await GotoDataGrid(mode);
        var section = await OpenSectionAsync();

        var frameBox = (await section.Locator("[data-slot='datagrid']").First.BoundingBoxAsync())!;
        var clip = new Clip { X = frameBox.X + frameBox.Width - 60, Y = frameBox.Y, Width = 60, Height = 60 };

        var viewport = section.Locator("[data-slot='datagrid-viewport']");

        // "Before": zero the live-measured --lumeo-grid-header-offset the band gradient and
        // the Chromium/Safari track-margin both key off — this reproduces the pre-fix white
        // notch (gutter strip mismatched against the header band) from the SAME live page.
        await viewport.EvaluateAsync("el => { el.dataset.lumeoE2eSavedOffset = getComputedStyle(el).getPropertyValue('--lumeo-grid-header-offset'); el.style.setProperty('--lumeo-grid-header-offset', '0px'); }");
        await Page.ScreenshotAsync(new() { Path = Path.Combine(ScreenshotDir, $"default-corner-before-{mode}.png"), Clip = clip });

        // "After": restore it.
        await viewport.EvaluateAsync("el => { const saved = el.dataset.lumeoE2eSavedOffset; if (saved) el.style.setProperty('--lumeo-grid-header-offset', saved); delete el.dataset.lumeoE2eSavedOffset; }");
        await Page.ScreenshotAsync(new() { Path = Path.Combine(ScreenshotDir, $"default-corner-after-{mode}.png"), Clip = clip });

        Assert.True(File.Exists(Path.Combine(ScreenshotDir, $"default-corner-before-{mode}.png")));
        Assert.True(File.Exists(Path.Combine(ScreenshotDir, $"default-corner-after-{mode}.png")));
    }

    private static bool ColorClose(Rgba32 a, Rgba32 b, int tolerance = 6) =>
        Math.Abs(a.R - b.R) <= tolerance && Math.Abs(a.G - b.G) <= tolerance && Math.Abs(a.B - b.B) <= tolerance;
}
