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
}
