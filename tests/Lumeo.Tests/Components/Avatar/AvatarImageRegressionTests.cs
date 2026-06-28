using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Avatar;

/// <summary>
/// Battle-test wave 3 regressions for AvatarImage:
///  - #2  (state-on-data-change): a broken image must stay hidden across an
///         unrelated re-render — the error latch is only cleared when Src changes.
///  - #29 (edge-data): a whitespace-only Src is treated like a blank Src
///         (no wasted request to the current page; fallback shows immediately).
///  - #30 (keyboard-a11y): the img always carries an alt attribute (WCAG 1.1.1),
///         defaulting to an empty/decorative alt when Alt is unset.
/// </summary>
public class AvatarImageRegressionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    public AvatarImageRegressionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Errored_Image_Stays_Hidden_Across_Unrelated_Rerender()
    {
        var cut = _ctx.Render<L.AvatarImage>(p => p.Add(a => a.Src, "/missing.png"));

        // Image fails to load → it stops rendering.
        cut.Find("img").TriggerEvent("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());
        Assert.Empty(cut.FindAll("img"));

        // An unrelated re-render with the SAME Src must NOT un-hide the broken
        // image (this re-runs OnParametersSet with an unchanged Src).
        cut.Render(p => p.Add(a => a.Src, "/missing.png"));
        Assert.Empty(cut.FindAll("img"));
    }

    [Fact]
    public void Errored_Image_Retries_When_Src_Genuinely_Changes()
    {
        var cut = _ctx.Render<L.AvatarImage>(p => p.Add(a => a.Src, "/missing.png"));
        cut.Find("img").TriggerEvent("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());
        Assert.Empty(cut.FindAll("img"));

        // A genuinely new Src clears the error latch and renders again.
        cut.Render(p => p.Add(a => a.Src, "/other.png"));
        Assert.Equal("/other.png", cut.Find("img").GetAttribute("src"));
    }

    [Fact]
    public void Whitespace_Only_Src_Is_Treated_As_Blank_And_Hides_Img()
    {
        var cut = _ctx.Render<L.AvatarImage>(p => p.Add(a => a.Src, "   "));

        // IsNullOrWhiteSpace fast path: no img request to the current page.
        Assert.Empty(cut.FindAll("img"));
    }

    [Fact]
    public void Img_Always_Has_Alt_Attribute_When_Alt_Unset()
    {
        var cut = _ctx.Render<L.AvatarImage>(p => p.Add(a => a.Src, "/avatar.png"));

        var img = cut.Find("img");
        Assert.True(img.HasAttribute("alt"));
        Assert.Equal(string.Empty, img.GetAttribute("alt"));
    }
}
