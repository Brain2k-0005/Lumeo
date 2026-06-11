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

    // ─── Format dispatch / encoder vectors ──────────────────────────────────

    /// <summary>
    /// Reconstructs the rendered module bit string ('1' = bar, '0' = space)
    /// from the bar rects. Requires BarWidth = 1 so each module is a 1px rect
    /// at x = QuietZone(10) + moduleIndex. Valid symbologies always end with
    /// a bar, so (max bar index + 1) equals the total module count.
    /// </summary>
    private static string RenderedModules(IReadOnlyList<AngleSharp.Dom.IElement> rects)
    {
        var bars = rects
            .Where(r => r.GetAttribute("width") == "1") // excludes the background rect
            .Select(r => (int)double.Parse(r.GetAttribute("x")!, System.Globalization.CultureInfo.InvariantCulture) - 10)
            .ToHashSet();
        if (bars.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i <= bars.Max(); i++)
            sb.Append(bars.Contains(i) ? '1' : '0');
        return sb.ToString();
    }

    private string ErrorText(IRenderedComponent<Lumeo.Barcode> cut)
        => cut.Find("div.text-xs.text-muted-foreground").TextContent.Trim();

    [Fact]
    public void EAN13_Twelve_Digits_Appends_Known_Check_Digit()
    {
        // Known vector: check digit for 590123412345 is 7.
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "590123412345")
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.EAN13));

        Assert.NotNull(cut.Find("svg"));
        var label = cut.Find("[role='img']").GetAttribute("aria-label") ?? "";
        Assert.Equal("Barcode: 5901234123457", label);
        Assert.Contains("5901234123457", cut.Markup); // ShowText shows the full 13 digits
    }

    [Fact]
    public void EAN13_Second_Known_Check_Digit_Vector()
    {
        // Known vector: check digit for 400638133393 is 1.
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "400638133393")
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.EAN13)
            .Add(b => b.BarWidth, 1.0));

        var label = cut.Find("[role='img']").GetAttribute("aria-label") ?? "";
        Assert.Equal("Barcode: 4006381333931", label);

        var modules = RenderedModules(cut.FindAll("rect"));
        Assert.Equal(95, modules.Length);
        Assert.StartsWith("101", modules);
        Assert.EndsWith("101", modules);
    }

    [Fact]
    public void EAN13_Renders_Exact_Known_Module_Sequence()
    {
        // 5901234123457: first digit 5 selects left parity LGGLLG, so
        // 9→L, 0→G, 1→G, 2→L, 3→L, 4→G; right group 1,2,3,4,5,7 uses R codes.
        const string expected =
            "101" +
            "0001011" + "0100111" + "0110011" + "0010011" + "0111101" + "0011101" +
            "01010" +
            "1100110" + "1101100" + "1000010" + "1011100" + "1001110" + "1000100" +
            "101";

        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "590123412345")
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.EAN13)
            .Add(b => b.BarWidth, 1.0));

        Assert.Equal(95, expected.Length); // sanity-check the vector itself
        Assert.Equal(expected, RenderedModules(cut.FindAll("rect")));
        // 95 modules + 2 x 10 quiet zone = svg width 115
        Assert.Equal("115", cut.Find("svg").GetAttribute("width"));
    }

    [Fact]
    public void EAN13_Thirteen_Digits_With_Valid_Check_Digit_Renders_95_Modules()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "5901234123457")
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.EAN13)
            .Add(b => b.BarWidth, 1.0));

        Assert.NotNull(cut.Find("svg"));
        Assert.Equal(95, RenderedModules(cut.FindAll("rect")).Length);
    }

    [Fact]
    public void EAN13_Wrong_Check_Digit_Shows_Error_And_No_Bars()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "5901234123450") // correct check digit is 7
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.EAN13));

        Assert.Empty(cut.FindAll("svg"));
        Assert.Empty(cut.FindAll("rect"));
        Assert.Contains("check digit", ErrorText(cut));

        var label = cut.Find("[role='img']").GetAttribute("aria-label") ?? "";
        Assert.StartsWith("Barcode error:", label);
        Assert.DoesNotContain("Barcode: ", label);
    }

    [Fact]
    public void EAN13_Non_Digit_Input_Shows_Error()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "59012341234X")
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.EAN13));

        Assert.Empty(cut.FindAll("svg"));
        Assert.StartsWith("Barcode error:", cut.Find("[role='img']").GetAttribute("aria-label") ?? "");
    }

    [Fact]
    public void EAN13_Wrong_Length_Shows_Error()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "12345")
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.EAN13));

        Assert.Empty(cut.FindAll("svg"));
        Assert.Contains("12 or 13 digits", ErrorText(cut));
    }

    [Fact]
    public void Code39_Single_A_Renders_Known_Star_A_Star_Pattern()
    {
        // '*' = 100101101101, 'A' = 110101001011, 1-module gaps between symbols.
        const string expected = "100101101101" + "0" + "110101001011" + "0" + "100101101101";

        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "A")
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.Code39)
            .Add(b => b.BarWidth, 1.0));

        var modules = RenderedModules(cut.FindAll("rect"));
        Assert.Equal(expected, modules);
        Assert.Equal(38, modules.Length); // (1+2)*12 + 2 gaps
        Assert.StartsWith("100101101101", modules);
        Assert.EndsWith("100101101101", modules);
    }

    [Fact]
    public void Code39_Module_Count_Follows_Length_Formula()
    {
        // n data chars => (n+2)*12 symbol modules + (n+1) gap modules.
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "AB-12")
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.Code39)
            .Add(b => b.BarWidth, 1.0));

        Assert.Equal((5 + 2) * 12 + 6, RenderedModules(cut.FindAll("rect")).Length);
    }

    [Fact]
    public void Code39_Lowercase_Is_Normalized_To_Uppercase()
    {
        // Documented choice: Code 39 has no lowercase symbols and readers
        // traditionally report uppercase, so lowercase input is uppercased
        // (and the displayed/announced text matches the encoded data).
        var lower = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "lumeo")
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.Code39)
            .Add(b => b.BarWidth, 1.0));
        var upper = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "LUMEO")
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.Code39)
            .Add(b => b.BarWidth, 1.0));

        Assert.NotNull(lower.Find("svg"));
        Assert.Equal("Barcode: LUMEO", lower.Find("[role='img']").GetAttribute("aria-label"));
        Assert.Equal(RenderedModules(upper.FindAll("rect")), RenderedModules(lower.FindAll("rect")));
    }

    [Fact]
    public void Code39_Invalid_Character_Shows_Error_And_No_Bars()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "AB_C") // '_' is not in the 43-char set
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.Code39));

        Assert.Empty(cut.FindAll("svg"));
        Assert.Empty(cut.FindAll("rect"));
        Assert.Contains("not valid for Code 39", ErrorText(cut));
        Assert.StartsWith("Barcode error:", cut.Find("[role='img']").GetAttribute("aria-label") ?? "");
    }

    [Fact]
    public void Code39_Star_Is_Reserved_For_Start_Stop()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "*AB*")
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.Code39));

        Assert.Empty(cut.FindAll("svg"));
        Assert.StartsWith("Barcode error:", cut.Find("[role='img']").GetAttribute("aria-label") ?? "");
    }

    [Fact]
    public void Code128_Non_Ascii_Shows_Error_Instead_Of_Silent_Blank()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "héllo")); // 'é' is outside Code 128B (ASCII 32-126)

        Assert.Empty(cut.FindAll("svg"));
        Assert.Empty(cut.FindAll("rect"));
        Assert.Contains("Code 128B", ErrorText(cut));

        var label = cut.Find("[role='img']").GetAttribute("aria-label") ?? "";
        Assert.StartsWith("Barcode error:", label);
    }

    [Fact]
    public void Code128_Format_Still_Default_And_Renders_Ascii()
    {
        var cut = _ctx.Render<Lumeo.Barcode>(p => p
            .Add(b => b.Value, "HELLO")
            .Add(b => b.Format, Lumeo.Barcode.BarcodeFormat.Code128)
            .Add(b => b.BarWidth, 1.0));

        // Start B + 5 data symbols + check symbol = 7 x 11 modules + 13-module stop.
        Assert.Equal(7 * 11 + 13, RenderedModules(cut.FindAll("rect")).Length);
        Assert.Equal("Barcode: HELLO", cut.Find("[role='img']").GetAttribute("aria-label"));
    }
}
