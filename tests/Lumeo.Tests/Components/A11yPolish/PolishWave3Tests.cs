using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;

namespace Lumeo.Tests.Components.A11yPolish;

/// <summary>
/// Polish wave 3:
///   #305 ReasoningDisplay — opt-in markdown rendering (safe built-in renderer
///        or a consumer MarkdownRenderer hook); plain-text default unchanged.
/// (#238 Collapsible controlled-state gate is a small logic-only change.)
/// </summary>
public class PolishWave3Tests
{
    private static BunitContext NewCtx()
    {
        var ctx = new BunitContext();
        ctx.AddLumeoServices();
        return ctx;
    }

    [Fact]
    public void ReasoningDisplay_renders_markdown_when_enabled()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(x => x.Markdown, true)
            .Add(x => x.Text, "**bold** and `code`"));

        Assert.Contains("<strong>bold</strong>", cut.Markup);
        Assert.Contains("<code>code</code>", cut.Markup);
    }

    [Fact]
    public void ReasoningDisplay_keeps_plain_text_by_default()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(x => x.Text, "**bold**"));

        // No markdown pass -> the asterisks survive as literal text, no <strong>.
        Assert.DoesNotContain("<strong>", cut.Markup);
        Assert.Contains("**bold**", cut.Markup);
    }

    [Fact]
    public void ReasoningDisplay_uses_custom_markdown_renderer_hook()
    {
        using var ctx = NewCtx();
        var cut = ctx.Render<Lumeo.ReasoningDisplay>(p => p
            .Add(x => x.Markdown, true)
            .Add(x => x.MarkdownRenderer, (Func<string, MarkupString>)(t => (MarkupString)$"<span class=\"custom\">{t}</span>"))
            .Add(x => x.Text, "hi"));

        Assert.Contains("<span class=\"custom\">hi</span>", cut.Markup);
    }
}
