using Lumeo.Docs.Services;
using Xunit;

namespace Lumeo.Docs.Tests.Services;

public class ThemePlaygroundParserTests
{
    [Fact]
    public void Extracts_root_and_dark()
    {
        var r = ThemePlaygroundParser.Parse(":root { --primary: oklch(0.5 0.1 200); --radius: 0.5rem; } .dark { --primary: oklch(0.8 0.1 200); }");
        Assert.Equal("oklch(0.5 0.1 200)", r.Light["primary"]);
        Assert.Equal("0.5rem", r.Light["radius"]);
        Assert.Equal("oklch(0.8 0.1 200)", r.Dark["primary"]);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Ignores_comments_and_missing_dark()
    {
        var r = ThemePlaygroundParser.Parse("/* --primary: red; */ :root { /* x */ --ring: oklch(0.1 0 0); }");
        Assert.False(r.Light.ContainsKey("primary"));
        Assert.Equal("oklch(0.1 0 0)", r.Light["ring"]);
        Assert.Empty(r.Dark);
    }

    [Fact]
    public void Tolerates_wrappers_and_skips_theme_inline()
    {
        var css = """
            @import "tailwindcss";
            @layer base { :root { --background: oklch(1 0 0); } }
            .dark { --background: oklch(0.1 0 0); }
            @theme inline { --color-background: var(--background); --radius-lg: var(--radius); }
            body { color: red; --not-me: 1; }
            """;
        var r = ThemePlaygroundParser.Parse(css);
        Assert.Equal("oklch(1 0 0)", r.Light["background"]);
        Assert.Equal("oklch(0.1 0 0)", r.Dark["background"]);
        Assert.False(r.Light.ContainsKey("color-background"));
        Assert.False(r.Light.ContainsKey("not-me"));
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Flags_unread_variables()
    {
        var r = ThemePlaygroundParser.Parse(":root { --primary: red; --tracking-normal: 0em; --tracking-tight: -0.1em; --shadow-opacity: 0.1; --radius-md: 4px; --foo: 1; }");
        Assert.Equal(4, r.Warnings.Count);
        Assert.DoesNotContain(r.Warnings, w => w.StartsWith("--tracking-normal"));
        Assert.Contains(r.Warnings, w => w.StartsWith("--tracking-tight"));
        Assert.DoesNotContain(r.Warnings, w => w.StartsWith("--primary"));
    }

    [Fact]
    public void Scoped_style_also_sets_color_tokens_and_override_css_drops_unread()
    {
        var r = ThemePlaygroundParser.Parse(":root { --primary: red; --radius: 1rem; --tracking-normal: 0.05em; } .dark { --primary: blue; }");
        var style = ThemePlaygroundParser.ToScopedStyle(r.Light);
        Assert.Contains("--color-primary:red;", style);
        Assert.Contains("--radius-md:", style);
        Assert.Contains("--tracking-normal:0.05em;", style);
        Assert.Contains("--tracking-tight:calc(var(--tracking-normal) - 0.025em);", style);
        Assert.Contains("--tracking-widest:calc(var(--tracking-normal) + 0.1em);", style);
        Assert.Contains("letter-spacing:var(--tracking-normal);", style);
        var css = ThemePlaygroundParser.ToOverrideCss(r);
        Assert.Contains(":root {", css);
        Assert.Contains(".dark {", css);
        Assert.Contains("--tracking-normal: 0.05em;", css);
    }

    [Fact]
    public void Empty_input_is_empty()
    {
        Assert.True(ThemePlaygroundParser.Parse("  ").IsEmpty);
        Assert.True(ThemePlaygroundParser.Parse(null).IsEmpty);
    }
}
