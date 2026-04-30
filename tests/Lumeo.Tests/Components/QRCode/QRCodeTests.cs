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
}
