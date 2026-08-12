using System.Globalization;

namespace Lumeo.GanttV3;

/// <summary>
/// Pure, static colour-resolution logic for the GanttV3 canvas chrome (design
/// spec Phase 3, T7 — "<c>ColorByGroup</c> bool: default palette assigns stable
/// per-group colors (chart-1..chart-N vars) when <c>BarColor</c> is null" and
/// "in-bar labels ... contrast handling"). Mirrors <see cref="GanttRowModel"/>/
/// <see cref="GanttReorderModel"/>/<see cref="GanttRollupModel"/>'s own shape:
/// no Blazor/DOM dependency, unit-testable in isolation.
/// </summary>
internal static class GanttColorModel
{
    /// <summary>
    /// Lumeo's theme palette exposes exactly five chart tokens
    /// (<c>--color-chart-1</c>..<c>--color-chart-5</c> — verified against every
    /// theme file under <c>src/Lumeo/wwwroot/css/themes/</c>), the same palette
    /// size <c>Lumeo.AreaChart</c>'s own <c>SeriesColorAt</c> (Lumeo.Charts
    /// project — not referenced from here, so plain text rather than a
    /// <c>cref</c>) already assumes for its identical "no explicit palette
    /// supplied" fallback.
    /// </summary>
    private const int PaletteSize = 5;

    /// <summary>
    /// <c>ColorByGroup</c>'s per-task colour: a stable <c>var(--color-chart-N)</c>
    /// reference keyed on the task's own reorder "bucket" (<see
    /// cref="GanttReorderModel.BucketKey(bool, GanttTask)"/> — hierarchy mode:
    /// <see cref="GanttTask.ParentId"/>; flat mode: normalized <see
    /// cref="GanttTask.GroupLabel"/>; same concept T6 already established for
    /// "which siblings this task belongs to").
    ///
    /// <b>Stability (design spec Phase 3, T7 — "same group keeps its colour
    /// across re-renders, task edits, and reorder"):</b> the palette index is a
    /// deterministic hash of the bucket-key STRING's own content (FNV-1a, see
    /// <see cref="StablePaletteIndex"/>) — content-addressed, not
    /// position-addressed. This is deliberately NOT "index of first appearance
    /// scanning the task list", which — while ALSO provably stable across a T6
    /// row reorder specifically (a reorder only permutes members WITHIN a
    /// bucket's own already-occupied list slots; <see
    /// cref="GanttReorderModel.Move"/> never relocates a slot to a DIFFERENT
    /// bucket, so the set of positions "belonging" to a bucket, and therefore
    /// its first-appearance rank relative to every OTHER bucket, is invariant
    /// under any reorder this library itself ever performs) — would NOT be
    /// stable across a task ADD/REMOVE that shifts which bucket is
    /// encountered first while scanning (e.g. deleting the very first task of
    /// group "A" changes group "B"'s first-appearance rank from 1 to 0, and
    /// therefore its assigned colour, even though nothing about group "B"
    /// itself changed). A hash keyed purely on the bucket string's own content
    /// has no such dependency on any OTHER task's presence, count, or order at
    /// all — provably stronger than the stated requirement, not merely
    /// sufficient for it, and .NET's own <c>string.GetHashCode()</c> is
    /// deliberately NOT used here because it is randomized PER PROCESS (a
    /// hash-flood mitigation, stable only for the lifetime of one running
    /// app/test process) — the identical group would get a DIFFERENT colour
    /// after every app restart/reconnect, and a bUnit test asserting an exact
    /// palette index would only pass by the coincidence of that run's random
    /// seed.
    /// </summary>
    internal static string ResolveGroupColorVar(bool usesHierarchy, GanttTask task)
    {
        var key = GanttReorderModel.BucketKey(usesHierarchy, task) ?? GanttReorderModel.RootBucketSentinel;
        var index = StablePaletteIndex(key);
        return string.Create(CultureInfo.InvariantCulture, $"var(--color-chart-{index + 1})");
    }

    /// <summary>
    /// Deterministic (cross-process, cross-run, cross-machine — unlike
    /// <c>string.GetHashCode()</c>) FNV-1a hash of <paramref name="key"/>,
    /// reduced to a <see cref="PaletteSize"/>-wide bucket. Pure function of
    /// the string's own content; O(key length), independent of how many other
    /// tasks/groups exist.
    /// </summary>
    private static int StablePaletteIndex(string key)
    {
        unchecked
        {
            const uint fnvOffsetBasis = 2166136261;
            const uint fnvPrime = 16777619;
            var hash = fnvOffsetBasis;
            foreach (var c in key)
            {
                hash ^= c;
                hash *= fnvPrime;
            }
            return (int)(hash % PaletteSize);
        }
    }

    /// <summary>
    /// The in-bar label's contrast-aware foreground <c>var(...)</c> reference
    /// against a custom <paramref name="color"/> (design spec Phase 3, T7 —
    /// "contrast handling (CSS color-mix/var-based foreground pick vs custom
    /// bar colors)"). Chose the plan's own "var-based foreground pick"
    /// alternative over a <c>color-mix</c>/relative-color-syntax lightness
    /// extraction (the well-known "<c>oklch(from var(--x) l 0 0)</c> +
    /// <c>@property</c> + <c>clamp(..., infinity, ...)</c>" trick, or the
    /// still-experimental CSS Color 5 <c>contrast-color()</c> function): both
    /// carry real, unresolved cross-engine risk (neither is reliably shipped
    /// in stable Chrome/Firefox/Safari at once as of this writing, and this
    /// sandboxed environment only has a Chromium runtime available to verify
    /// against directly — Firefox/WebKit behavior for either trick could not
    /// be empirically confirmed here). A plain <c>var()</c> reference has NO
    /// such risk (CSS custom properties are universally supported), so this
    /// entire mechanism is a discrete choice made once, here, in C# — never a
    /// CSS-side computation — trading a small amount of coverage (an
    /// unparseable colour, e.g. a raw <c>var(...)</c> reference passed as
    /// <c>BarColor</c>, falls back to the caller's existing default rather
    /// than being contrast-corrected) for zero rendering risk.
    ///
    /// Returns <c>null</c> when <paramref name="color"/> is <c>null</c> (the
    /// caller keeps its EXISTING <c>text-foreground</c> styling — the default
    /// chart-palette-coloured bar's look is unchanged, out of this feature's scope)
    /// or when it cannot be parsed as a hex (<c>#rgb</c>/<c>#rgba</c>/
    /// <c>#rrggbb</c>/<c>#rrggbbaa</c>) or <c>rgb()</c>/<c>rgba()</c> literal —
    /// an honest, documented limitation: a <c>var()</c> reference, named CSS
    /// colour, or <c>hsl()</c>/<c>oklch()</c> literal cannot be resolved to a
    /// concrete lightness at C# render time without a browser round trip, and
    /// adding one would reintroduce exactly the per-frame-adjacent JS-interop
    /// cost this campaign has consistently avoided for purely cosmetic gain
    /// (see <c>GanttInteropOptions</c>'s own remarks on the same principle
    /// applied to drag registration).
    /// </summary>
    internal static string? PickLabelForegroundVar(string? color, bool themeIsDark)
    {
        if (color is null) return null;
        if (!TryParseRgb(color, out var r, out var g, out var b)) return null;

        // Same luminance formula/threshold Lumeo's own ColorPicker.IsLight
        // already uses for the identical "contrast against an arbitrary
        // caller-supplied swatch, not against the page background" problem
        // (ITU-R BT.601 luma, not the stricter/gamma-linearized WCAG relative
        // luminance formula) — reused for consistency with existing,
        // already-shipped Lumeo behavior rather than introducing a second,
        // subtly-different contrast formula into the codebase.
        var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        var colorIsLight = luminance > 0.6;

        // --color-foreground/--color-background are ALWAYS an
        // opposite-lightness pair in every Lumeo theme (that pairing IS the
        // definition of a foreground/background token) — but WHICH of the two
        // currently evaluates light vs dark depends on the ambient theme
        // mode, which flips between light/dark (unlike, say,
        // --color-destructive-foreground, which stays a fixed white in both
        // modes because a status colour's own foreground is deliberately
        // NOT part of the light/dark inversion story). themeIsDark picks the
        // variable that CURRENTLY resolves to the wanted pole rather than
        // assuming either name is fixed.
        if (colorIsLight) return themeIsDark ? "var(--color-background)" : "var(--color-foreground)";
        return themeIsDark ? "var(--color-foreground)" : "var(--color-background)";
    }

    private static bool TryParseRgb(string value, out int r, out int g, out int b)
    {
        var s = value.Trim();
        if (s.Length > 0 && s[0] == '#') return TryParseHex(s, out r, out g, out b);
        if (s.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseRgbFunction(s, out r, out g, out b);
        }
        r = g = b = 0;
        return false;
    }

    private static bool TryParseHex(string hex, out int r, out int g, out int b)
    {
        r = g = b = 0;
        var body = hex.AsSpan(1);
        if (body.Length is not (3 or 4 or 6 or 8)) return false;

        try
        {
            if (body.Length is 3 or 4)
            {
                r = int.Parse(Duplicate(body[0]), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                g = int.Parse(Duplicate(body[1]), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                b = int.Parse(Duplicate(body[2]), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            else
            {
                r = int.Parse(body.Slice(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                g = int.Parse(body.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                b = int.Parse(body.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            return true;
        }
        catch (FormatException)
        {
            r = g = b = 0;
            return false;
        }

        static string Duplicate(char c) => new(c, 2);
    }

    private static bool TryParseRgbFunction(string value, out int r, out int g, out int b)
    {
        r = g = b = 0;
        var open = value.IndexOf('(');
        var close = value.IndexOf(')');
        if (open < 0 || close < 0 || close <= open) return false;

        var parts = value[(open + 1)..close].Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out r)) return false;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out g)) return false;
        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out b)) return false;

        return r is >= 0 and <= 255 && g is >= 0 and <= 255 && b is >= 0 and <= 255;
    }
}
