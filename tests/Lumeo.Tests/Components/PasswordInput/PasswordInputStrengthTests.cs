using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.PasswordInput;

/// <summary>
/// Pins the password strength-meter behavior: the four-segment bar, its colored
/// fill, and the localized strength label. Strength is computed from a 0–5 score
/// (length ≥ 8, has upper, has lower, has digit, has special) mapped onto four
/// levels: Weak (destructive), Fair (warning), Good (info), Strong (success).
/// The meter only renders when <c>ShowStrength</c> is set and the value is non-empty.
/// </summary>
public class PasswordInputStrengthTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public PasswordInputStrengthTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Strength_meter_hidden_when_show_strength_false()
    {
        var cut = _ctx.Render<L.PasswordInput>(p => p
            .Add(c => c.ShowStrength, false)
            .Add(c => c.Value, "Abcd1234!"));

        // No strength bar segments and no strength label even for a strong value.
        Assert.DoesNotContain("rounded-full", cut.Markup);
        Assert.DoesNotContain("Strong", cut.Markup);
    }

    [Fact]
    public void Strength_meter_hidden_when_value_empty()
    {
        var cut = _ctx.Render<L.PasswordInput>(p => p
            .Add(c => c.ShowStrength, true)
            .Add(c => c.Value, ""));

        // ShowStrength on, but an empty value suppresses the meter entirely.
        Assert.DoesNotContain("rounded-full", cut.Markup);
    }

    [Fact]
    public void Short_simple_password_renders_weak_strength()
    {
        // "abc": only "has lower" scores -> score 1 -> level 1 (Weak/destructive).
        var cut = _ctx.Render<L.PasswordInput>(p => p
            .Add(c => c.ShowStrength, true)
            .Add(c => c.Value, "abc"));

        // Four bar segments always render; exactly one is filled at level 1.
        // Radius-token-aware rounding (was a hardcoded rounded-full that ignored the theme radius).
        var bars = cut.FindAll("div.rounded-\\[calc\\(var\\(--radius\\)\\*2\\)\\]");
        Assert.Equal(4, bars.Count);
        Assert.Equal(1, bars.Count(b => (b.GetAttribute("class") ?? "").Contains("bg-destructive")));

        // Weak label, in the destructive color.
        var label = cut.Find("p.text-destructive");
        Assert.Equal("Weak", label.TextContent);
    }

    [Fact]
    public void Long_mixed_class_password_renders_strong_strength()
    {
        // "Abcd1234!": length>=8 + upper + lower + digit + special -> score 5 -> level 4.
        var cut = _ctx.Render<L.PasswordInput>(p => p
            .Add(c => c.ShowStrength, true)
            .Add(c => c.Value, "Abcd1234!"));

        // All four segments filled with the success color at the top level.
        // Radius-token-aware rounding (was a hardcoded rounded-full that ignored the theme radius).
        var bars = cut.FindAll("div.rounded-\\[calc\\(var\\(--radius\\)\\*2\\)\\]");
        Assert.Equal(4, bars.Count);
        Assert.Equal(4, bars.Count(b => (b.GetAttribute("class") ?? "").Contains("bg-success")));

        // Strong label, in the success color.
        var label = cut.Find("p.text-success");
        Assert.Equal("Strong", label.TextContent);
    }

    [Fact]
    public void Medium_password_renders_fair_strength()
    {
        // "ab12": has lower + has digit -> score 2 -> level 2 (Fair/warning).
        var cut = _ctx.Render<L.PasswordInput>(p => p
            .Add(c => c.ShowStrength, true)
            .Add(c => c.Value, "ab12"));

        // Radius-token-aware rounding (was a hardcoded rounded-full that ignored the theme radius).
        var bars = cut.FindAll("div.rounded-\\[calc\\(var\\(--radius\\)\\*2\\)\\]");
        Assert.Equal(4, bars.Count);
        Assert.Equal(2, bars.Count(b => (b.GetAttribute("class") ?? "").Contains("bg-warning")));

        var label = cut.Find("p.text-warning");
        Assert.Equal("Fair", label.TextContent);
    }
}
