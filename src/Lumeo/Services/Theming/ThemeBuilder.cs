using Microsoft.JSInterop;

namespace Lumeo;

/// <summary>
/// Fluent builder that emits a custom CSS-variable override block on top of
/// a Lumeo base scheme (zinc / slate / stone / …). Lets consumers tweak
/// design tokens without writing a CSS file or forking a scheme.
///
/// <code>
/// await Theme.Customize(Js)
///     .WithPrimary("#3b82f6")
///     .WithBorderRadius(0.5)
///     .WithFontFamily("'Inter', system-ui, sans-serif")
///     .ApplyAsync();
/// </code>
///
/// Calls are idempotent — each <see cref="ApplyAsync"/> replaces the
/// previous `&lt;style id="lumeo-custom-theme"&gt;` block, so consumers
/// can rebuild on every settings change without leaking stale rules.
/// </summary>
public sealed class ThemeBuilder
{
    private readonly IJSRuntime _js;
    private readonly Dictionary<string, string> _vars = new(StringComparer.Ordinal);

    internal ThemeBuilder(IJSRuntime js) { _js = js; }

    /// <summary>Override <c>--color-primary</c>. Accepts any CSS color
    /// string (hex, rgb(), hsl(), oklch(), CSS named color).</summary>
    public ThemeBuilder WithPrimary(string color) => Set("--color-primary", color);
    public ThemeBuilder WithPrimaryForeground(string color) => Set("--color-primary-foreground", color);

    public ThemeBuilder WithSecondary(string color) => Set("--color-secondary", color);
    public ThemeBuilder WithSecondaryForeground(string color) => Set("--color-secondary-foreground", color);

    public ThemeBuilder WithAccent(string color) => Set("--color-accent", color);
    public ThemeBuilder WithAccentForeground(string color) => Set("--color-accent-foreground", color);

    public ThemeBuilder WithBackground(string color) => Set("--color-background", color);
    public ThemeBuilder WithForeground(string color) => Set("--color-foreground", color);

    public ThemeBuilder WithMuted(string color) => Set("--color-muted", color);
    public ThemeBuilder WithMutedForeground(string color) => Set("--color-muted-foreground", color);

    public ThemeBuilder WithBorder(string color) => Set("--color-border", color);
    public ThemeBuilder WithRing(string color) => Set("--color-ring", color);
    public ThemeBuilder WithDestructive(string color) => Set("--color-destructive", color);

    /// <summary>Override the global border-radius scale. Accepts a rem value
    /// — Lumeo uses <c>0.5</c> by default. Components derive from
    /// <c>calc(var(--radius) - 2px)</c> / <c>calc(var(--radius) + 4px)</c>.</summary>
    public ThemeBuilder WithBorderRadius(double rem) => Set("--radius", rem.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "rem");

    /// <summary>Override the root font family. Pass the full CSS
    /// font-family stack including quotes / fallbacks.</summary>
    public ThemeBuilder WithFontFamily(string fontStack) => Set("--font-family", fontStack);

    /// <summary>Set an arbitrary CSS custom property. Use this for tokens
    /// that don't have a typed setter yet (e.g. component-scoped vars).</summary>
    public ThemeBuilder WithVariable(string name, string value)
    {
        if (string.IsNullOrEmpty(name)) return this;
        // Normalize: accept "primary" → "--color-primary", "--ring" → "--ring".
        var key = name.StartsWith("--", StringComparison.Ordinal) ? name : "--" + name;
        return Set(key, value);
    }

    /// <summary>Write the accumulated overrides to a managed
    /// <c>&lt;style id="lumeo-custom-theme"&gt;</c> block. Replaces the
    /// previous block on each call, so successive Apply calls don't leak.</summary>
    public ValueTask ApplyAsync()
    {
        if (_vars.Count == 0)
            return _js.InvokeVoidAsync("lumeoThemeCustom.clear");
        var sb = new System.Text.StringBuilder(":root{");
        foreach (var kv in _vars)
        {
            sb.Append(kv.Key).Append(':').Append(kv.Value).Append(';');
        }
        sb.Append('}');
        return _js.InvokeVoidAsync("lumeoThemeCustom.apply", sb.ToString());
    }

    /// <summary>Remove the custom-theme block, reverting to the base scheme.</summary>
    public ValueTask ClearAsync() => _js.InvokeVoidAsync("lumeoThemeCustom.clear");

    private ThemeBuilder Set(string key, string value)
    {
        _vars[key] = value;
        return this;
    }
}

/// <summary>Entry point for the fluent <see cref="ThemeBuilder"/>.</summary>
public static class Theme
{
    public static ThemeBuilder Customize(IJSRuntime js) => new(js);
}
