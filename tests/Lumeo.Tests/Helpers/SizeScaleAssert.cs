using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Lumeo.Tests.Helpers;

/// <summary>
/// Shared helpers for the cross-component Size scale-ordering test (PR #388 task
/// brief: "add a scale-ordering test that ... asserts the ramp is monotonically
/// non-decreasing across Xxs &lt; Xs &lt; Sm &lt; Md &lt; Lg &lt; Xl &lt; Xxl").
///
/// Two things every component-level Size test needs and gets wrong by default:
///
///  1. <see cref="VisualOrder"/> is NOT <c>Enum.GetValues&lt;Lumeo.Size&gt;()</c> —
///     <c>Lumeo.Size.Xxs</c> is declared LAST (backing value 6, added in 3.3) so its
///     growth position is FIRST. Enumerating declaration order silently checks
///     Xs&lt;Sm&lt;Md&lt;Lg&lt;Xl&lt;Xxl&lt;Xxs, which is nonsense.
///  2. Tailwind class tokens need converting to a comparable number before two
///     rungs can be compared: "h-4" and "h-11" are NOT comparable as strings, and
///     "px-2" is a *prefix* of "px-2.5" so naive substring checks lie. This parses
///     the handful of token shapes actually used across the PR's components
///     (bare spacing units, arbitrary `[Npx]` brackets, and named text-size
///     tokens) into pixels.
/// </summary>
public static class SizeScaleAssert
{
    /// <summary>Growth order used by every Lumeo Size-driven component, left to right.</summary>
    public static readonly Lumeo.Size[] VisualOrder =
    {
        Lumeo.Size.Xxs, Lumeo.Size.Xs, Lumeo.Size.Sm, Lumeo.Size.Md,
        Lumeo.Size.Lg, Lumeo.Size.Xl, Lumeo.Size.Xxl
    };

    // Tailwind v4's default font-size scale (the named tokens actually used across
    // this PR's components). line-height companions are irrelevant here — only the
    // font-size half of each pair drives visual growth.
    private static readonly Dictionary<string, double> NamedTextSizePx = new()
    {
        ["text-xs"] = 12,
        ["text-sm"] = 14,
        ["text-base"] = 16,
        ["text-lg"] = 18,
        ["text-xl"] = 20,
        ["text-2xl"] = 24,
        ["text-3xl"] = 30,
        ["text-4xl"] = 36,
        ["text-5xl"] = 48,
        ["text-6xl"] = 60,
    };

    private static readonly Regex ArbitraryPxRegex = new(@"^\[(?<n>[0-9.]+)px\]$", RegexOptions.Compiled);
    private static readonly Regex TextArbitraryPxRegex = new(@"^text-\[(?<n>[0-9.]+)px\]$", RegexOptions.Compiled);
    // A geometry token (#434): h-[var(--lumeo-control-h,calc(var(--spacing,0.25rem)*8))] is 8 spacing steps.
    private static readonly Regex GeometryTokenRegex = new(@"^\[var\(--lumeo-[a-z0-9-]+,calc\(var\(--spacing,0\.25rem\)\*(?<n>[0-9.]+)\)\)\]$", RegexOptions.Compiled);

    /// <summary>
    /// Finds the FIRST space-delimited token starting with "<paramref name="prefix"/>-"
    /// (e.g. prefix "h" matches "h-4" or "h-[52px]", never "hover:..." — the dash is
    /// part of the match) and parses its value into pixels under Tailwind's default
    /// spacing step (--spacing: 0.25rem = 4px). Returns null if no such token exists,
    /// or its suffix isn't numeric/arbitrary-pixel (e.g. "w-full", "h-px").
    /// </summary>
    public static double? SpacingPx(string? classAttr, string prefix)
    {
        var needle = prefix + "-";
        var token = FindToken(classAttr, needle);
        if (token is null) return null;
        var suffix = token[needle.Length..];

        var arbitrary = ArbitraryPxRegex.Match(suffix);
        if (arbitrary.Success)
            return double.Parse(arbitrary.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        var geometry = GeometryTokenRegex.Match(suffix);
        if (geometry.Success)
            return double.Parse(geometry.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture) * 4.0;

        return double.TryParse(suffix, NumberStyles.Float, CultureInfo.InvariantCulture, out var units)
            ? units * 4.0
            : null;
    }

    /// <summary>
    /// Finds a Tailwind text-size token (named, e.g. "text-sm", or arbitrary
    /// "text-[10px]") among the space-delimited classes and returns its font-size
    /// in pixels, or null if none is present.
    /// </summary>
    public static double? TextSizePx(string? classAttr)
    {
        foreach (var token in Tokens(classAttr))
        {
            if (NamedTextSizePx.TryGetValue(token, out var named)) return named;
            var arbitrary = TextArbitraryPxRegex.Match(token);
            if (arbitrary.Success)
                return double.Parse(arbitrary.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        return null;
    }

    private static string? FindToken(string? classAttr, string prefixWithDash)
    {
        foreach (var token in Tokens(classAttr))
            if (token.StartsWith(prefixWithDash, StringComparison.Ordinal))
                return token;
        return null;
    }

    private static IEnumerable<string> Tokens(string? classAttr) =>
        (classAttr ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Asserts that <paramref name="series"/> — pixel values already collected in
    /// <see cref="VisualOrder"/> order — is monotonically NON-DECREASING (ties
    /// allowed; only a strict decrease fails). On failure, names both rungs and
    /// both values so a violation is diagnosable straight from the test output.
    /// </summary>
    public static void AssertMonotonicNonDecreasing(IReadOnlyList<(Lumeo.Size Size, double Value)> series, string dimensionLabel)
    {
        for (var i = 1; i < series.Count; i++)
        {
            var (prevSize, prevVal) = series[i - 1];
            var (curSize, curVal) = series[i];
            Assert.True(curVal >= prevVal,
                $"{dimensionLabel}: {curSize} ({curVal}px) < {prevSize} ({prevVal}px) at position {i} of the Xxs..Xxl ramp — not monotonically non-decreasing.");
        }
    }
}
