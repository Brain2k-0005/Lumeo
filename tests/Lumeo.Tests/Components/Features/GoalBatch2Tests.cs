using System;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.Features;

/// <summary>
/// Goal batch 2:
///   #196 Code — pluggable Highlighter + Source/Language (syntax-highlighting support).
///   #275 Sparkline — opt-in per-point markers with native &lt;title&gt; tooltips.
///   #276 SparkCard — inline chart delegates to the full Sparkline.
/// </summary>
public class GoalBatch2Tests
{
    private static BunitContext NewCtx()
    {
        var ctx = new BunitContext();
        ctx.AddLumeoServices();
        return ctx;
    }

    [Fact]
    public void Code_uses_highlighter_hook_for_source()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Code>(p => p
            .Add(x => x.Source, "let x = 1;")
            .Add(x => x.Language, "ts")
            .Add(x => x.Highlighter, (Func<string, string?, MarkupString>)((c, l) => (MarkupString)$"<span class=\"kw\">{c}</span>")));

        Assert.Contains("<span class=\"kw\">let x = 1;</span>", cut.Markup);
        Assert.Equal("ts", cut.Find("code").GetAttribute("data-language"));
    }

    [Fact]
    public void Code_without_highlighter_escapes_source()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Code>(p => p.Add(x => x.Source, "<script>alert(1)</script>"));

        Assert.DoesNotContain("<script>", cut.Markup); // escaped, not injected
        Assert.Contains("alert(1)", cut.Markup);
    }

    [Fact]
    public void Sparkline_tooltips_emit_titles_per_point()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Sparkline>(p => p
            .Add(x => x.Values, new double[] { 3, 1, 4 })
            .Add(x => x.ShowTooltips, true));

        Assert.Contains("<title>", cut.Markup);
        Assert.Contains("<title>3</title>", cut.Markup);
        Assert.Contains("<title>4</title>", cut.Markup);
    }

    [Fact]
    public void Sparkline_no_tooltips_by_default()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.Sparkline>(p => p.Add(x => x.Values, new double[] { 3, 1, 4 }));
        Assert.DoesNotContain("<title>", cut.Markup);
    }

    [Fact]
    public void SparkCard_renders_delegated_sparkline()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.SparkCard>(p => p
            .Add(x => x.Label, "Revenue")
            .Add(x => x.Data, new double[] { 1, 2, 3, 2, 5 })
            .Add(x => x.ShowTooltips, true));

        // Delegated <Sparkline> renders an svg; tooltips flow through.
        Assert.NotEmpty(cut.FindAll("svg"));
        Assert.Contains("<title>", cut.Markup);
    }
}
