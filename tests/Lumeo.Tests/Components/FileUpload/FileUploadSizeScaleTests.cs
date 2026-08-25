using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.FileUpload;

// Pins all 7 Lumeo.Size rungs (Xxs, Xs, Sm, Md, Lg, Xl, Xxl) for the Button variant's
// trigger sizing — the only variant that reads Size (see FileUpload.razor XML doc).
// Also pins the documented behaviour that Dropzone/Avatar ignore Size entirely,
// so a future accidental wiring shows up as a test failure, not a silent drift.
//
// IMPORTANT: assertions use exact space-delimited token matching (AssertHasClass),
// never Assert.Contains(substring, cls) — Tailwind's own scale defeats naive
// substring checks (e.g. "px-1" is a substring of "px-1.5", "h-1" of "h-10"/"h-11"/"h-12").
public class FileUploadSizeScaleTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public FileUploadSizeScaleTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static void AssertHasClass(string? cls, string token)
    {
        var tokens = (cls ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(token, tokens);
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs, "h-6", "px-1", "text-[10px]")]
    [InlineData(Lumeo.Size.Xs, "h-7", "px-2", "text-[10px]")]
    [InlineData(Lumeo.Size.Sm, "h-7", "px-2.5", "text-[0.8rem]")]
    [InlineData(Lumeo.Size.Md, "h-8", "px-2.5", "text-sm")]
    [InlineData(Lumeo.Size.Lg, "h-9", "px-2.5", "text-sm")]
    [InlineData(Lumeo.Size.Xl, "h-10", "px-3", "text-base")]
    [InlineData(Lumeo.Size.Xxl, "h-11", "px-4", "text-lg")]
    public void Button_Variant_Size_Per_Rung(Lumeo.Size size, string h, string px, string text)
    {
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Variant, Lumeo.FileUpload.FileUploadVariant.Button)
            .Add(b => b.Size, size));

        var cls = cut.Find("label").GetAttribute("class");
        AssertHasClass(cls, h);
        AssertHasClass(cls, px);
        AssertHasClass(cls, text);
    }

    [Fact]
    public void Sm_Md_Lg_Button_Rungs_Match_Buttons_Own_Ladder()
    {
        var sm = _ctx.Render<Lumeo.FileUpload>(p => p.Add(b => b.Variant, Lumeo.FileUpload.FileUploadVariant.Button).Add(b => b.Size, Lumeo.Size.Sm));
        var md = _ctx.Render<Lumeo.FileUpload>(p => p.Add(b => b.Variant, Lumeo.FileUpload.FileUploadVariant.Button).Add(b => b.Size, Lumeo.Size.Md));
        var lg = _ctx.Render<Lumeo.FileUpload>(p => p.Add(b => b.Variant, Lumeo.FileUpload.FileUploadVariant.Button).Add(b => b.Size, Lumeo.Size.Lg));

        var smCls = sm.Find("label").GetAttribute("class");
        var mdCls = md.Find("label").GetAttribute("class");
        var lgCls = lg.Find("label").GetAttribute("class");

        foreach (var token in new[] { "h-7", "px-2.5", "text-[0.8rem]" }) AssertHasClass(smCls, token);
        foreach (var token in new[] { "h-8", "px-2.5", "text-sm" }) AssertHasClass(mdCls, token);
        foreach (var token in new[] { "h-9", "px-2.5", "text-sm" }) AssertHasClass(lgCls, token);
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs)]
    [InlineData(Lumeo.Size.Xs)]
    [InlineData(Lumeo.Size.Sm)]
    [InlineData(Lumeo.Size.Md)]
    [InlineData(Lumeo.Size.Lg)]
    [InlineData(Lumeo.Size.Xl)]
    [InlineData(Lumeo.Size.Xxl)]
    public void Dropzone_Variant_Ignores_Size_At_Every_Rung(Lumeo.Size size)
    {
        // Documented behaviour: Dropzone's icon (h-8 w-8) never varies with Size.
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Variant, Lumeo.FileUpload.FileUploadVariant.Dropzone)
            .Add(b => b.Size, size));

        var svgCls = cut.Find("svg").GetAttribute("class");
        AssertHasClass(svgCls, "h-8");
        AssertHasClass(svgCls, "w-8");
    }

    [Theory]
    [InlineData(Lumeo.Size.Xxs)]
    [InlineData(Lumeo.Size.Xs)]
    [InlineData(Lumeo.Size.Sm)]
    [InlineData(Lumeo.Size.Md)]
    [InlineData(Lumeo.Size.Lg)]
    [InlineData(Lumeo.Size.Xl)]
    [InlineData(Lumeo.Size.Xxl)]
    public void Avatar_Variant_Ignores_Size_At_Every_Rung(Lumeo.Size size)
    {
        // Documented behaviour: Avatar's circular picker (h-20 w-20, icon h-8 w-8)
        // never varies with Size.
        var cut = _ctx.Render<Lumeo.FileUpload>(p => p
            .Add(b => b.Variant, Lumeo.FileUpload.FileUploadVariant.Avatar)
            .Add(b => b.Size, size));

        var labelCls = cut.Find("label").GetAttribute("class");
        AssertHasClass(labelCls, "h-20");
        AssertHasClass(labelCls, "w-20");

        var svgCls = cut.Find("svg").GetAttribute("class");
        AssertHasClass(svgCls, "h-8");
        AssertHasClass(svgCls, "w-8");
    }
}
