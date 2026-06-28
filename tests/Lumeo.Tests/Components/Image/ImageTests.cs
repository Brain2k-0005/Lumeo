using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Lumeo.Tests.Components.Image;

public class ImageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ImageTests()
    {
        _ctx.AddLumeoServices();
        // Observe focus-trap lifecycle on the preview overlay.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_Img_Element_With_Src()
    {
        var cut = _ctx.Render<Lumeo.Image>(p => p
            .Add(i => i.Src, "photo.jpg")
            .Add(i => i.Alt, "A photo"));

        var img = cut.Find("img");
        Assert.Equal("photo.jpg", img.GetAttribute("src"));
        Assert.Equal("A photo", img.GetAttribute("alt"));
    }

    [Fact]
    public void Container_Has_Relative_InlineBlock_Classes()
    {
        var cut = _ctx.Render<Lumeo.Image>(p => p
            .Add(i => i.Src, "photo.jpg"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("relative", cls);
        Assert.Contains("inline-block", cls);
    }

    [Fact]
    public void Lazy_True_Sets_Loading_Lazy_Attribute()
    {
        var cut = _ctx.Render<Lumeo.Image>(p => p
            .Add(i => i.Src, "photo.jpg")
            .Add(i => i.Lazy, true));

        Assert.Equal("lazy", cut.Find("img").GetAttribute("loading"));
    }

    [Fact]
    public void Width_And_Height_Are_Forwarded_To_Img()
    {
        var cut = _ctx.Render<Lumeo.Image>(p => p
            .Add(i => i.Src, "photo.jpg")
            .Add(i => i.Width, "200")
            .Add(i => i.Height, "100"));

        var img = cut.Find("img");
        Assert.Equal("200", img.GetAttribute("width"));
        Assert.Equal("100", img.GetAttribute("height"));
    }

    [Fact]
    public void Custom_Class_On_Container()
    {
        var cut = _ctx.Render<Lumeo.Image>(p => p
            .Add(i => i.Src, "photo.jpg")
            .Add(i => i.Class, "my-image"));

        var cls = cut.Find("div").GetAttribute("class");
        Assert.Contains("my-image", cls);
    }

    // ── Preview overlay focus trap (#287) ────────────────────────────────────
    // aria-modal="true" without focus containment is a hard a11y failure. The
    // overlay must install a focus trap on open and tear it down on close.

    [Fact]
    public void Opening_Preview_Installs_Focus_Trap()
    {
        var cut = _ctx.Render<Lumeo.Image>(p => p
            .Add(i => i.Src, "photo.jpg")
            .Add(i => i.Preview, true));

        Assert.Empty(_interop.FocusTrapSetups);

        // Click the image to open the fullscreen preview.
        cut.Find("img").Click();

        var setup = Assert.Single(_interop.FocusTrapSetups);
        // The trap targets the overlay element (role=dialog aria-modal).
        var overlay = cut.Find("[role='dialog']");
        Assert.Equal(overlay.Id, setup.ElementId);
        Assert.Empty(_interop.FocusTrapRemovals);
    }

    [Fact]
    public void Closing_Preview_With_Escape_Removes_Focus_Trap()
    {
        var cut = _ctx.Render<Lumeo.Image>(p => p
            .Add(i => i.Src, "photo.jpg")
            .Add(i => i.Preview, true));

        cut.Find("img").Click();
        var setup = Assert.Single(_interop.FocusTrapSetups);

        // Escape on the overlay closes it.
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(setup.ElementId, Assert.Single(_interop.FocusTrapRemovals));
    }

    // ── #9 state-on-data-change: error latch must reset when Src changes ───────
    // _hasError latched true on a failed load and was never reset, so swapping in
    // a new, valid Src left it permanently hidden behind the fallback.

    [Fact]
    public void Changing_Src_After_Error_Resets_Latch_And_Renders_New_Image()
    {
        var cut = _ctx.Render<Lumeo.Image>(p => p
            .Add(i => i.Src, "broken.jpg")
            .Add(i => i.Alt, "x"));

        // The image fails to load → it stops rendering (no Fallback set ⇒ no <img>).
        cut.Find("img").TriggerEvent("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());
        Assert.Empty(cut.FindAll("img"));

        // A genuinely new Src must clear the error latch so the new image renders
        // instead of staying hidden behind the fallback.
        cut.Render(p => p.Add(i => i.Src, "valid.jpg"));

        var img = cut.Find("img");
        Assert.Equal("valid.jpg", img.GetAttribute("src"));
    }

    // ── #11 keyboard-a11y: preview trigger must be keyboard reachable ─────────
    // The preview was only openable by clicking a bare <img>/<div> with @onclick;
    // there was no focusable control, so keyboard users could never open it.

    [Fact]
    public void Preview_Trigger_Is_A_Keyboard_Reachable_Button_With_Accessible_Name()
    {
        var cut = _ctx.Render<Lumeo.Image>(p => p
            .Add(i => i.Src, "photo.jpg")
            .Add(i => i.Alt, "A scenic photo")
            .Add(i => i.Preview, true));

        // The overlay trigger must now be a native <button> (in the tab order,
        // Enter/Space activates it) carrying an accessible name. Before the fix
        // there was no <button> at all in the closed state.
        var triggers = cut.FindAll("button")
            .Where(b => (b.GetAttribute("aria-label") ?? "").StartsWith("Open preview"))
            .ToList();
        var trigger = Assert.Single(triggers);
        Assert.Contains("A scenic photo", trigger.GetAttribute("aria-label"));

        // Activating it opens the fullscreen preview.
        trigger.Click();
        Assert.NotNull(cut.Find("[role='dialog']"));
    }

    // ── #10 edge-data: zoom transform must use invariant culture ──────────────
    // "transform: scale(1.25)" was built via raw double→string concat, so a
    // comma-decimal locale (de-DE) produced invalid CSS "scale(1,25)".

    [Fact]
    public void Preview_Zoom_Transform_Uses_Invariant_Decimal_Separator()
    {
        var prev = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo("de-DE");

            var cut = _ctx.Render<Lumeo.Image>(p => p
                .Add(i => i.Src, "photo.jpg")
                .Add(i => i.Preview, true));

            // Open the preview, then zoom to 125% so the scale factor is fractional.
            cut.Find("img").Click();
            cut.FindAll("button")
                .First(b => b.GetAttribute("aria-label") == "Zoom in")
                .Click();

            var previewImg = cut.Find("[role='dialog'] img");
            var style = previewImg.GetAttribute("style") ?? "";
            Assert.Contains("scale(1.25)", style);
            Assert.DoesNotContain("1,25", style);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = prev;
        }
    }
}
