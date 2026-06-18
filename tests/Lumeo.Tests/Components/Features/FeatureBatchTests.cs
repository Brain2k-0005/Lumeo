using System;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Features;

/// <summary>
/// Feature batch (3.17.0):
///   #291 Barcode — OnError validation hook + quiet zone that scales with BarWidth.
///   #293 Highlighter — opt-in RegexMode (patterns instead of literal terms).
/// </summary>
public class FeatureBatchTests
{
    private static BunitContext NewCtx()
    {
        var ctx = new BunitContext();
        ctx.AddLumeoServices();
        return ctx;
    }

    [Fact]
    public void Barcode_OnError_fires_with_message_for_invalid_value()
    {
        using var ctx = NewCtx();
        string? captured = "SENTINEL";
        ctx.Render<Lumeo.Barcode>(p => p
            .Add(x => x.Format, Lumeo.Barcode.BarcodeFormat.EAN13)
            .Add(x => x.Value, "abc")
            .Add(x => x.OnError, EventCallback.Factory.Create<string?>(this, v => captured = v)));

        Assert.False(string.IsNullOrEmpty(captured)); // a real encoding error message
    }

    [Fact]
    public void Barcode_OnError_fires_null_for_valid_value()
    {
        using var ctx = NewCtx();
        string? captured = "SENTINEL";
        ctx.Render<Lumeo.Barcode>(p => p
            .Add(x => x.Value, "HELLO")
            .Add(x => x.OnError, EventCallback.Factory.Create<string?>(this, v => captured = v)));

        Assert.Null(captured); // encoded OK -> null
    }

    [Theory]
    [InlineData(2, "20")]
    [InlineData(3, "30")]
    public void Barcode_quiet_zone_scales_with_bar_width(double barWidth, string expectedX)
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Barcode>(p => p
            .Add(x => x.Value, "A")
            .Add(x => x.BarWidth, barWidth));

        // rect[0] is the background (x=0); rect[1] is the first bar, offset by the
        // quiet zone (= 10 × BarWidth).
        var rects = cut.FindAll("rect");
        Assert.True(rects.Count >= 2);
        Assert.Equal(expectedX, rects[1].GetAttribute("x"));
    }

    [Fact]
    public void Highlighter_regex_mode_matches_pattern()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Highlighter>(p => p
            .Add(x => x.RegexMode, true)
            .Add(x => x.Highlight, @"\d+")
            .Add(x => x.Text, "a 12 b 34"));

        var marks = cut.FindAll("mark");
        Assert.Equal(2, marks.Count);
        Assert.Equal("12", marks[0].TextContent);
        Assert.Equal("34", marks[1].TextContent);
    }

    [Fact]
    public void Highlighter_literal_mode_does_not_treat_pattern_as_regex()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Highlighter>(p => p
            .Add(x => x.Highlight, @"\d+")
            .Add(x => x.Text, "a 12 b"));

        // Default literal mode: "\d+" is not present verbatim -> nothing highlighted.
        Assert.Empty(cut.FindAll("mark"));
        Assert.Contains("a 12 b", cut.Markup);
    }

    [Fact]
    public void Highlighter_invalid_regex_falls_back_to_plain_text()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Highlighter>(p => p
            .Add(x => x.RegexMode, true)
            .Add(x => x.Highlight, "[")  // invalid pattern
            .Add(x => x.Text, "hello"));

        Assert.Empty(cut.FindAll("mark"));
        Assert.Contains("hello", cut.Markup);
    }

    [Fact]
    public void Grid_responsive_uses_breakpoint_columns()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Grid>(p => p
            .Add(x => x.Columns, 3)
            .Add(x => x.Responsive, true));

        var cls = cut.Find("div").GetAttribute("class") ?? "";
        Assert.Contains("sm:grid-cols-2", cls);
        Assert.Contains("lg:grid-cols-3", cls);
    }

    [Fact]
    public void Grid_default_stays_fixed_columns()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Grid>(p => p.Add(x => x.Columns, 3));

        var cls = cut.Find("div").GetAttribute("class") ?? "";
        Assert.Contains("grid-cols-3", cls);
        Assert.DoesNotContain("sm:grid-cols", cls);
    }
}
