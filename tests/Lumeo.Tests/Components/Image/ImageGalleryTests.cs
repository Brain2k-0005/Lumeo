using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Image;

/// <summary>
/// Battle-test wave 3 regressions for ImageGallery (comp == "Image"):
///  • #12 keyboard-a11y — the icon-only overlay controls (close / prev / next /
///    zoom) must each carry an accessible name.
///  • #10 edge-data — the preview zoom transform must be formatted with the
///    invariant culture so comma-decimal locales don't emit invalid CSS.
/// </summary>
public class ImageGalleryTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ImageGalleryTests() => _ctx.AddLumeoServices();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.ImageGallery> RenderGallery(params (string Src, string Alt)[] items)
    {
        var images = items.Select(i => new L.ImageGallery.ImageItem(i.Src, i.Alt)).ToList();
        return _ctx.Render<L.ImageGallery>(p => p.Add(g => g.Images, images));
    }

    private static void OpenPreview(IRenderedComponent<L.ImageGallery> cut) =>
        cut.FindAll("button")
            .First(b => (b.GetAttribute("aria-label") ?? "").StartsWith("Open image"))
            .Click();

    // ── #12 keyboard-a11y ─────────────────────────────────────────────────────

    [Fact]
    public void Preview_Overlay_Buttons_Have_Accessible_Names()
    {
        // Two images so the prev/next controls (only rendered when Count > 1) show.
        var cut = RenderGallery(("a.jpg", "First"), ("b.jpg", "Second"));

        OpenPreview(cut);

        var labels = cut.FindAll("[role='dialog'] button")
            .Select(b => b.GetAttribute("aria-label"))
            .ToList();

        Assert.Contains("Close preview", labels);
        Assert.Contains("Previous image", labels);
        Assert.Contains("Next image", labels);
        Assert.Contains("Zoom out", labels);
        Assert.Contains("Zoom in", labels);
    }

    // ── #10 edge-data ─────────────────────────────────────────────────────────

    [Fact]
    public void Preview_Zoom_Transform_Uses_Invariant_Decimal_Separator()
    {
        var prev = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo("de-DE");

            var cut = RenderGallery(("a.jpg", "First"));

            OpenPreview(cut);

            // Zoom in once → 125% → scale(1.25). Under de-DE a naive double→string
            // would emit the invalid CSS "scale(1,25)".
            cut.FindAll("[role='dialog'] button")
                .First(b => b.GetAttribute("aria-label") == "Zoom in")
                .Click();

            var style = cut.Find("[role='dialog'] img").GetAttribute("style") ?? "";
            Assert.Contains("scale(1.25)", style);
            Assert.DoesNotContain("1,25", style);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = prev;
        }
    }
}
