using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.QRCode;

public class QRCodeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public QRCodeTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_default_without_value()
    {
        var cut = _ctx.Render<L.QRCode>();
        // With no Value, the SVG is still rendered (empty QR)
        Assert.NotEmpty(cut.FindAll("svg"));
    }

    [Fact]
    public void Merges_class_parameter()
    {
        var cut = _ctx.Render<L.QRCode>(p => p.Add(c => c.Class, "qr-cls"));
        Assert.Contains("qr-cls", cut.Markup);
    }

    [Fact]
    public void Forwards_additional_attributes()
    {
        var cut = _ctx.Render<L.QRCode>(p => p
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object> { ["data-testid"] = "qr" }));
        Assert.Contains("data-testid=\"qr\"", cut.Markup);
    }

    [Fact]
    public void Generates_svg_modules_when_value_provided()
    {
        var cut = _ctx.Render<L.QRCode>(p => p.Add(c => c.Value, "https://example.com"));
        var rects = cut.FindAll("rect");
        // Should have at least the background rect plus some dark module rects
        Assert.True(rects.Count > 1, "QR code should render dark module rects");
    }

    [Fact]
    public void Respects_size_parameter()
    {
        var cut = _ctx.Render<L.QRCode>(p => p
            .Add(c => c.Value, "test")
            .Add(c => c.Size, 300));
        Assert.Contains("width: 300px", cut.Markup);
        Assert.Contains("height: 300px", cut.Markup);
    }

    [Fact]
    public void Logo_Overlay_Is_Scaled_From_Pixels_To_Module_Units()
    {
        // Regression: ImageSize (pixels of the rendered box) was used RAW in
        // viewBox module units — the default 40 covered 60-130% of the code.
        var cut = _ctx.Render<L.QRCode>(p => p
            .Add(c => c.Value, "https://example.com")
            .Add(c => c.Size, 200)
            .Add(c => c.ImageSize, 40)
            .Add(c => c.ImageSrc, "logo.png"));

        var svg = cut.Find("svg");
        var viewBox = svg.GetAttribute("viewBox")!.Split(' ');
        var total = double.Parse(viewBox[2], System.Globalization.CultureInfo.InvariantCulture);

        var image = cut.Find("image");
        var width = double.Parse(image.GetAttribute("width")!, System.Globalization.CultureInfo.InvariantCulture);

        // 40px of a 200px box = 20% of the code, regardless of QR version.
        Assert.Equal(0.20, width / total, 2);
        Assert.True(width < total / 3, "logo must not dominate the code");
    }

    private static List<AngleSharp.Dom.IElement> DarkModuleRects(IRenderedComponent<L.QRCode> cut) =>
        cut.FindAll("rect").Where(r => r.GetAttribute("width") == "1").ToList();

    [Fact]
    public void Default_Margin_Is_Single_QuietZone_Not_Doubled()
    {
        // Regression (n17): QRCoder's ModuleMatrix already carries a built-in
        // 4-module quiet zone. The component added ANOTHER 4-module margin on top,
        // so the default code sat inside an 8-module quiet zone (over-padded).
        // The smallest dark module must start at x=4 (one quiet zone), not 8.
        var cut = _ctx.Render<L.QRCode>(p => p.Add(c => c.Value, "https://example.com"));

        var minX = DarkModuleRects(cut)
            .Select(r => int.Parse(r.GetAttribute("x")!, System.Globalization.CultureInfo.InvariantCulture))
            .Min();
        Assert.Equal(4, minX);
    }

    [Fact]
    public void IncludeMargin_False_Strips_BuiltIn_QuietZone()
    {
        // Regression (n17): with IncludeMargin=false the built-in quiet zone must
        // be stripped so the code fills the box — the top-left finder pattern's
        // corner module must touch (0,0). Before the fix the matrix's own 4-module
        // zone remained, so no dark module ever reached the edge.
        var cut = _ctx.Render<L.QRCode>(p => p
            .Add(c => c.Value, "https://example.com")
            .Add(c => c.IncludeMargin, false));

        var dark = DarkModuleRects(cut);
        Assert.Contains(dark, r => r.GetAttribute("x") == "0" && r.GetAttribute("y") == "0");
    }

    [Fact]
    public void Empty_Value_Drops_Img_Role_And_AriaLabel()
    {
        // Regression (n52): with no Value the empty box was still announced as
        // role=img "QR code:" — drop role/aria-label entirely instead.
        var cut = _ctx.Render<L.QRCode>();

        var div = cut.Find("div");
        Assert.False(div.HasAttribute("role"));
        Assert.False(div.HasAttribute("aria-label"));
    }

    [Fact]
    public void Whitespace_Value_Is_Treated_As_Empty_For_Accessibility()
    {
        // Regression (n52): a whitespace Value passed the IsNullOrEmpty guard and
        // was announced as a QR code. Whitespace must be treated as empty.
        var cut = _ctx.Render<L.QRCode>(p => p.Add(c => c.Value, "   "));

        var div = cut.Find("div");
        Assert.False(div.HasAttribute("role"));
        Assert.False(div.HasAttribute("aria-label"));
    }

    [Fact]
    public void Whitespace_AriaLabel_Falls_Back_To_Default_Label()
    {
        // Regression (n52): the `??` operator only caught null, so an empty/
        // whitespace AriaLabel bypassed the "QR code: {Value}" fallback.
        var cut = _ctx.Render<L.QRCode>(p => p
            .Add(c => c.Value, "hello")
            .Add(c => c.AriaLabel, "   "));

        Assert.Equal("QR code: hello", cut.Find("div").GetAttribute("aria-label"));
    }

    [Fact]
    public void Custom_AriaLabel_Is_Still_Honored()
    {
        // Guard: a genuine AriaLabel must still override the default.
        var cut = _ctx.Render<L.QRCode>(p => p
            .Add(c => c.Value, "hello")
            .Add(c => c.AriaLabel, "Scan to pay"));

        Assert.Equal("Scan to pay", cut.Find("div").GetAttribute("aria-label"));
    }
}
