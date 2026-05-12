using Bunit;
using Xunit;
using Lumeo;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Barcode;

public class BarcodeTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public BarcodeTests()
    {
        _ctx.AddLumeoServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Renders_SVG_For_Valid_Value()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "HELLO"));

        Assert.NotNull(cut.Find("svg"));
    }

    [Fact]
    public void Has_Img_Role_With_Aria_Label()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "TEST123"));

        var el = cut.Find("[role='img']");
        Assert.NotNull(el);
        Assert.Contains("TEST123", el.GetAttribute("aria-label") ?? "");
    }

    [Fact]
    public void Renders_Bar_Rects_For_Encoded_Value()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "ABC"));

        // Should have many rect elements (one per bar module)
        var rects = cut.FindAll("rect");
        Assert.True(rects.Count > 5, $"Expected many rect elements for bars, got {rects.Count}");
    }

    [Fact]
    public void ShowText_True_Shows_Value_Text()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "HELLO")
            .Add(b => b.ShowText, true));

        Assert.Contains("HELLO", cut.Markup);
    }

    [Fact]
    public void ShowText_False_Hides_Text()
    {
        // With ShowText=false there should be no foreignObject
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "HELLO")
            .Add(b => b.ShowText, false));

        Assert.Empty(cut.FindAll("foreignObject"));
    }

    [Fact]
    public void Empty_Value_Renders_No_SVG()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, ""));

        Assert.Empty(cut.FindAll("svg"));
    }

    [Fact]
    public void Custom_Class_Is_Applied()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "TEST")
            .Add(b => b.Class, "my-barcode"));

        Assert.Contains("my-barcode", cut.Markup);
    }

    [Fact]
    public void Different_Values_Produce_Different_Bar_Counts()
    {
        var cut1 = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "A"));
        var cut2 = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "ABCDEF"));

        var rects1 = cut1.FindAll("rect").Count;
        var rects2 = cut2.FindAll("rect").Count;

        // Longer value should produce more bars
        Assert.True(rects2 > rects1);
    }

    [Fact]
    public void Aria_Label_Contains_Barcode_Prefix()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "XYZ"));

        var label = cut.Find("[role='img']").GetAttribute("aria-label") ?? "";
        Assert.StartsWith("Barcode:", label);
    }
}
