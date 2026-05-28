using System.Globalization;

namespace Lumeo.Internal;

/// <summary>
/// Minimal <c>tailwind-merge</c>-style conflict resolver used by
/// <see cref="Cx.Merge"/>. Classifies each utility into a conflict group; for
/// every (variant-chain, group) pair only the last token in source order wins.
/// A handful of groups are "supersets" (e.g. <c>p-*</c> clears <c>px-*</c> /
/// <c>pt-*</c>); those relationships are modelled explicitly. Anything that
/// cannot be classified is treated as a unique class and never dropped.
/// </summary>
internal static class TailwindMerge
{
    internal static string Resolve(List<string> tokens)
    {
        // For each token compute the set of conflict-group ids it occupies plus
        // the set of group ids it invalidates (superset relationships). A token
        // survives only if, among all later tokens, none invalidates any of the
        // groups this token occupies under the same variant chain.
        var infos = new ClassInfo[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
            infos[i] = Classify(tokens[i]);

        // groupKey -> index of the last token that occupies that group.
        var lastByGroup = new Dictionary<string, int>(StringComparer.Ordinal);
        // Walk forwards; for each token, the groups it invalidates take ownership.
        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Groups is null) continue; // unknown class, always kept
            foreach (var g in info.Invalidates!)
                lastByGroup[info.Variant + "\0" + g] = i;
        }

        // A classified token is kept iff, for every group it occupies, it is the
        // current owner (i.e. no later token invalidated that group).
        var keep = new bool[infos.Length];
        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Groups is null) { keep[i] = true; continue; }

            var survives = true;
            foreach (var g in info.Groups)
            {
                if (lastByGroup.TryGetValue(info.Variant + "\0" + g, out var owner) && owner != i)
                {
                    survives = false;
                    break;
                }
            }
            keep[i] = survives;
        }

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (!keep[i]) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(tokens[i]);
        }
        return sb.ToString();
    }

    private readonly struct ClassInfo
    {
        /// <summary>Variant chain (e.g. <c>hover:focus:</c>); part of every key.</summary>
        public string Variant { get; init; }
        /// <summary>Groups this token occupies, or null for unknown classes.</summary>
        public string[]? Groups { get; init; }
        /// <summary>Groups this token invalidates (occupied groups + subordinates).</summary>
        public string[]? Invalidates { get; init; }
    }

    private static ClassInfo Classify(string token)
    {
        // 1. Peel off variant prefixes. Variants are ':'-separated segments, but
        //    a ':' inside brackets [...] / parens (...) is NOT a separator.
        var variantEnd = FindVariantBoundary(token);
        var variant = token[..variantEnd];
        var body = token[variantEnd..];

        // 2. Strip the important flag (leading '!' or trailing '!') — not part of
        //    the conflict group.
        if (body.StartsWith('!')) body = body[1..];
        if (body.EndsWith('!')) body = body[..^1];

        // 3. Strip a single leading negative sign for classification purposes.
        var negative = body.StartsWith('-');
        if (negative) body = body[1..];

        var (groups, invalidates) = GroupFor(body);
        if (groups is null)
        {
            // Unknown — give it a per-index unique identity so it is never merged.
            return new ClassInfo { Variant = variant, Groups = null, Invalidates = null };
        }

        return new ClassInfo { Variant = variant, Groups = groups, Invalidates = invalidates };
    }

    private static int FindVariantBoundary(string token)
    {
        var depthBracket = 0;
        var depthParen = 0;
        var lastSep = -1;
        for (var i = 0; i < token.Length; i++)
        {
            var c = token[i];
            switch (c)
            {
                case '[': depthBracket++; break;
                case ']': if (depthBracket > 0) depthBracket--; break;
                case '(': depthParen++; break;
                case ')': if (depthParen > 0) depthParen--; break;
                case ':':
                    if (depthBracket == 0 && depthParen == 0) lastSep = i;
                    break;
            }
        }
        return lastSep + 1; // everything up to and including the last top-level ':'
    }

    // Returns (occupied groups, invalidated groups) for the utility body or
    // (null, null) when unrecognised. Most utilities occupy exactly one group
    // and invalidate exactly that group; supersets invalidate subordinates too.
    private static (string[]? Groups, string[]? Invalidates) GroupFor(string body)
    {
        // Arbitrary property e.g. [mask-type:luminance] — its own unique group.
        if (body.StartsWith('[') && body.EndsWith(']'))
        {
            var g = new[] { "arb:" + body };
            return (g, g);
        }

        // Split into a leading "prefix" (the utility name) and the value, where
        // the value is everything after the first '-' that is followed by a value
        // (number, fraction, keyword, arbitrary [..], or CSS var (..)). We don't
        // actually need the value for grouping in most cases — the prefix decides
        // the group. So match against known prefixes greedily (longest first).

        // --- Padding (superset: p clears px/py/pt/pr/pb/pl/ps/pe) ---
        if (Is(body, "p")) return Super("pad", "pad-x", "pad-y", "pad-t", "pad-r", "pad-b", "pad-l", "pad-s", "pad-e");
        if (Is(body, "px")) return SuperOf("pad-x", "pad-l", "pad-r");
        if (Is(body, "py")) return SuperOf("pad-y", "pad-t", "pad-b");
        if (Is(body, "pt")) return One("pad-t");
        if (Is(body, "pr")) return One("pad-r");
        if (Is(body, "pb")) return One("pad-b");
        if (Is(body, "pl")) return One("pad-l");
        if (Is(body, "ps")) return One("pad-s");
        if (Is(body, "pe")) return One("pad-e");

        // --- Margin (same shape as padding) ---
        if (Is(body, "m")) return Super("mar", "mar-x", "mar-y", "mar-t", "mar-r", "mar-b", "mar-l", "mar-s", "mar-e");
        if (Is(body, "mx")) return SuperOf("mar-x", "mar-l", "mar-r");
        if (Is(body, "my")) return SuperOf("mar-y", "mar-t", "mar-b");
        if (Is(body, "mt")) return One("mar-t");
        if (Is(body, "mr")) return One("mar-r");
        if (Is(body, "mb")) return One("mar-b");
        if (Is(body, "ml")) return One("mar-l");
        if (Is(body, "ms")) return One("mar-s");
        if (Is(body, "me")) return One("mar-e");

        // --- Sizing ---
        if (Is(body, "size")) return Super("size", "w", "h");
        if (Is(body, "w")) return One("w");
        if (Is(body, "h")) return One("h");
        if (Is(body, "min-w")) return One("min-w");
        if (Is(body, "max-w")) return One("max-w");
        if (Is(body, "min-h")) return One("min-h");
        if (Is(body, "max-h")) return One("max-h");

        // --- Inset (superset: inset clears x/y/t/r/b/l; inset-x/y clear sides) ---
        if (Is(body, "inset-x")) return SuperOf("inset-x", "inset-l", "inset-r");
        if (Is(body, "inset-y")) return SuperOf("inset-y", "inset-t", "inset-b");
        if (Is(body, "inset")) return Super("inset", "inset-x", "inset-y", "inset-t", "inset-r", "inset-b", "inset-l");
        if (Is(body, "top")) return One("inset-t");
        if (Is(body, "right")) return One("inset-r");
        if (Is(body, "bottom")) return One("inset-b");
        if (Is(body, "left")) return One("inset-l");
        if (Is(body, "start")) return One("inset-s");
        if (Is(body, "end")) return One("inset-e");

        // --- Gap ---
        if (Is(body, "gap-x")) return One("gap-x");
        if (Is(body, "gap-y")) return One("gap-y");
        if (Is(body, "gap")) return Super("gap", "gap-x", "gap-y");

        // --- Border radius (superset: rounded clears all corners/sides) ---
        if (Is(body, "rounded-tl")) return One("rnd-tl");
        if (Is(body, "rounded-tr")) return One("rnd-tr");
        if (Is(body, "rounded-br")) return One("rnd-br");
        if (Is(body, "rounded-bl")) return One("rnd-bl");
        if (Is(body, "rounded-t")) return SuperOf("rnd-t", "rnd-tl", "rnd-tr");
        if (Is(body, "rounded-r")) return SuperOf("rnd-r", "rnd-tr", "rnd-br");
        if (Is(body, "rounded-b")) return SuperOf("rnd-b", "rnd-bl", "rnd-br");
        if (Is(body, "rounded-l")) return SuperOf("rnd-l", "rnd-tl", "rnd-bl");
        if (Is(body, "rounded-s")) return One("rnd-s");
        if (Is(body, "rounded-e")) return One("rnd-e");
        if (Is(body, "rounded-ss")) return One("rnd-ss");
        if (Is(body, "rounded-se")) return One("rnd-se");
        if (Is(body, "rounded-ee")) return One("rnd-ee");
        if (Is(body, "rounded-es")) return One("rnd-es");
        if (body == "rounded" || Is(body, "rounded"))
            return Super("rnd", "rnd-t", "rnd-r", "rnd-b", "rnd-l", "rnd-tl", "rnd-tr", "rnd-br", "rnd-bl");

        // --- Border style (solid/dashed/dotted/double/hidden/none) is its own
        //     group, distinct from border width and border color so consumers can
        //     layer e.g. `border border-border/40` + `border-dashed`. ---
        if (body is "border-solid" or "border-dashed" or "border-dotted"
            or "border-double" or "border-hidden" or "border-none")
            return One("border-style");

        // --- Border width (per-side) vs border color. The bare `border` token and
        //     numeric/arbitrary `border-2`, `border-x`, etc. are widths; named
        //     colors like `border-red-500` are colors. We disambiguate by value. ---
        if (body == "border") return One("bw");
        if (Is(body, "border-x")) return BorderEdge(body, "bw-x", "bw-l", "bw-r", isSuper: true);
        if (Is(body, "border-y")) return BorderEdge(body, "bw-y", "bw-t", "bw-b", isSuper: true);
        if (Is(body, "border-t")) return BorderEdge(body, "bw-t");
        if (Is(body, "border-r")) return BorderEdge(body, "bw-r");
        if (Is(body, "border-b")) return BorderEdge(body, "bw-b");
        if (Is(body, "border-l")) return BorderEdge(body, "bw-l");
        if (Is(body, "border-s")) return BorderEdge(body, "bw-s");
        if (Is(body, "border-e")) return BorderEdge(body, "bw-e");
        if (Is(body, "border"))
        {
            // border-2 / border-[3px] => width; border-red-500 / border-current => color
            var v = ValueOf(body, "border");
            return LooksLikeWidth(v) ? One("bw") : One("border-color");
        }

        // --- Background: distinct CSS properties live in distinct groups so a
        //     later `bg-cover` does not delete an earlier `bg-red-500` (and vice
        //     versa). Mirrors tailwind-merge's bg-color / bg-image / bg-size /
        //     bg-position / bg-repeat / bg-origin / bg-clip / bg-blend /
        //     bg-attachment split. ---
        if (Is(body, "bg")) return One(BackgroundGroup(ValueOf(body, "bg")));

        // --- Text: size vs color must coexist (different groups). ---
        if (Is(body, "text"))
        {
            var v = ValueOf(body, "text");
            if (IsTextSize(v)) return One("font-size");
            if (IsAlign(v)) return One("text-align");
            return One("text-color");
        }

        // --- Font weight ---
        if (IsFontWeight(body)) return One("font-weight");

        // --- Leading / tracking ---
        if (Is(body, "leading")) return One("leading");
        if (Is(body, "tracking")) return One("tracking");

        // --- Display ---
        if (IsDisplay(body)) return One("display");

        // --- Position ---
        if (body is "static" or "fixed" or "absolute" or "relative" or "sticky") return One("position");

        // --- Overflow ---
        if (Is(body, "overflow-x")) return One("overflow-x");
        if (Is(body, "overflow-y")) return One("overflow-y");
        if (Is(body, "overflow")) return Super("overflow", "overflow-x", "overflow-y");

        // --- Z-index / opacity / shadow / ring ---
        if (Is(body, "z")) return One("z");
        if (Is(body, "opacity")) return One("opacity");
        if (body == "shadow" || Is(body, "shadow"))
        {
            var v = ValueOf(body, "shadow");
            // shadow color (e.g. shadow-red-500) vs box-shadow size.
            return LooksLikeColor(v) ? One("shadow-color") : One("shadow");
        }
        if (body == "ring" || Is(body, "ring"))
        {
            // ring-inset is a style flag, not a color or width — its own group so
            // it coexists with both `ring-2` and `ring-ring`.
            if (body == "ring-inset") return One("ring-inset");
            if (Is(body, "ring-offset"))
            {
                var ov = ValueOf(body, "ring-offset");
                return LooksLikeWidth(ov) ? One("ring-offset-w") : One("ring-offset-color");
            }
            var v = ValueOf(body, "ring");
            return LooksLikeWidth(v) ? One("ring-w") : One("ring-color");
        }

        // --- Flex / grid basics ---
        if (Is(body, "basis")) return One("basis");
        if (body == "flex" || Is(body, "flex"))
        {
            if (body is "flex-row" or "flex-row-reverse" or "flex-col" or "flex-col-reverse")
                return One("flex-direction");
            if (body is "flex-wrap" or "flex-wrap-reverse" or "flex-nowrap")
                return One("flex-wrap");
            if (body == "flex") return One("display");
            return One("flex");
        }
        if (Is(body, "grow")) return One("grow");
        if (Is(body, "shrink")) return One("shrink");
        if (Is(body, "order")) return One("order");
        if (Is(body, "grid-cols")) return One("grid-cols");
        if (Is(body, "grid-rows")) return One("grid-rows");
        // Grid column/row span vs start vs end set different sub-properties and are
        // commonly combined; keep them in separate groups. A bare `col-*`
        // (`col-auto`, `col-[..]`) maps to the span group, like tailwind-merge.
        if (Is(body, "col-span")) return One("grid-col-span");
        if (Is(body, "col-start")) return One("grid-col-start");
        if (Is(body, "col-end")) return One("grid-col-end");
        if (Is(body, "col")) return One("grid-col-span");
        if (Is(body, "row-span")) return One("grid-row-span");
        if (Is(body, "row-start")) return One("grid-row-start");
        if (Is(body, "row-end")) return One("grid-row-end");
        if (Is(body, "row")) return One("grid-row-span");

        // --- Alignment ---
        if (Is(body, "items")) return One("align-items");
        if (Is(body, "justify-items")) return One("justify-items");
        if (Is(body, "justify-self")) return One("justify-self");
        if (Is(body, "justify")) return One("justify-content");
        if (Is(body, "self")) return One("align-self");
        if (Is(body, "content")) return One("align-content");
        if (Is(body, "place-items")) return One("place-items");
        if (Is(body, "place-content")) return One("place-content");
        if (Is(body, "place-self")) return One("place-self");

        return (null, null);
    }

    // ---- group helpers ----

    private static (string[]?, string[]?) One(string group)
    {
        var g = new[] { group };
        return (g, g);
    }

    /// <summary>Superset: occupies <paramref name="self"/>, invalidates self + all subs.</summary>
    private static (string[]?, string[]?) Super(string self, params string[] subs)
    {
        var inv = new string[subs.Length + 1];
        inv[0] = self;
        Array.Copy(subs, 0, inv, 1, subs.Length);
        return (new[] { self }, inv);
    }

    /// <summary>Mid-level superset that also occupies (is invalidated by) a parent group implicitly via subs.</summary>
    private static (string[]?, string[]?) SuperOf(string self, params string[] subs)
        => Super(self, subs);

    private static (string[]?, string[]?) BorderEdge(string body, string widthGroup, string? subA = null, string? subB = null, bool isSuper = false)
    {
        // Determine whether this is a width or a color for the per-edge token.
        var prefix = widthGroup switch
        {
            "bw-x" => "border-x",
            "bw-y" => "border-y",
            "bw-t" => "border-t",
            "bw-r" => "border-r",
            "bw-b" => "border-b",
            "bw-l" => "border-l",
            "bw-s" => "border-s",
            "bw-e" => "border-e",
            _ => widthGroup,
        };
        var v = ValueOf(body, prefix);
        // Color edges are rare in Lumeo; treat numeric/empty/arbitrary as width.
        if (!LooksLikeWidth(v))
        {
            // Edge color group, keyed per edge.
            return One(widthGroup + "-color");
        }
        if (isSuper && subA is not null && subB is not null)
            return Super(widthGroup, subA, subB);
        return One(widthGroup);
    }

    // ---- matchers ----

    /// <summary>True when <paramref name="body"/> is exactly <paramref name="prefix"/> or starts with <c>prefix-</c>.</summary>
    private static bool Is(string body, string prefix)
        => body.Length >= prefix.Length
           && body.AsSpan(0, prefix.Length).SequenceEqual(prefix)
           && (body.Length == prefix.Length || body[prefix.Length] == '-');

    /// <summary>Returns the value portion after <c>prefix-</c>, or empty when bare.</summary>
    private static string ValueOf(string body, string prefix)
        => body.Length > prefix.Length && body[prefix.Length] == '-'
            ? body[(prefix.Length + 1)..]
            : string.Empty;

    private static bool LooksLikeWidth(string v)
    {
        if (v.Length == 0) return true; // bare `border` etc.
        if (v.StartsWith('[') && !v.Contains(' '))
        {
            // Arbitrary: width if it looks like a length, not a color.
            return !LooksLikeColorLiteral(v);
        }
        // All-numeric => width (border-2, border-4).
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool LooksLikeColor(string v)
    {
        if (v.Length == 0) return false;
        if (LooksLikeColorLiteral(v)) return true;
        // Named palette: word optionally followed by -<number>, or keywords.
        if (v is "current" or "transparent" or "inherit" or "white" or "black") return true;
        var dash = v.IndexOf('-');
        if (dash > 0 && int.TryParse(v[(dash + 1)..], out _)) return true;
        return false;
    }

    private static bool LooksLikeColorLiteral(string v)
        => v.Contains('#') || v.Contains("rgb", StringComparison.Ordinal)
           || v.Contains("hsl", StringComparison.Ordinal) || v.Contains("oklch", StringComparison.Ordinal)
           || v.StartsWith("[#", StringComparison.Ordinal);

    /// <summary>
    /// Resolves a <c>bg-*</c> value into its tailwind-merge background sub-group.
    /// Distinct CSS properties (color, image, size, position, repeat, origin, clip,
    /// blend, attachment) get distinct groups so they coexist.
    /// </summary>
    private static string BackgroundGroup(string v)
    {
        if (v.Length == 0) return "bg-color"; // bare `bg` is not real; keep safe

        // bg-image: none / gradient / url(...) arbitrary.
        if (v == "none" || v.StartsWith("gradient-", StringComparison.Ordinal)
            || v == "linear" || v == "radial" || v == "conic"
            || v.StartsWith("linear-", StringComparison.Ordinal)
            || v.StartsWith("radial-", StringComparison.Ordinal)
            || v.StartsWith("conic-", StringComparison.Ordinal)
            || (v.StartsWith('[') && v.Contains("url(", StringComparison.Ordinal))
            || (v.StartsWith('[') && (v.Contains("gradient", StringComparison.Ordinal)
                || v.Contains("image:", StringComparison.Ordinal))))
            return "bg-image";

        // bg-size: auto / cover / contain / [length:..].
        if (v is "auto" or "cover" or "contain"
            || (v.StartsWith('[') && (v.Contains("length:", StringComparison.Ordinal)
                || v.Contains("size:", StringComparison.Ordinal))))
            return "bg-size";

        // bg-position: keywords + corners + [position:..].
        if (v is "center" or "top" or "bottom" or "left" or "right"
            or "left-top" or "left-bottom" or "right-top" or "right-bottom"
            or "top-left" or "top-right" or "bottom-left" or "bottom-right"
            || (v.StartsWith('[') && v.Contains("position:", StringComparison.Ordinal)))
            return "bg-position";

        // bg-repeat: repeat / no-repeat / repeat-x / repeat-y / repeat-round / repeat-space.
        if (v is "repeat" or "no-repeat" or "repeat-x" or "repeat-y"
            or "repeat-round" or "repeat-space")
            return "bg-repeat";

        // bg-attachment.
        if (v is "fixed" or "local" or "scroll") return "bg-attachment";

        // bg-origin.
        if (v is "origin-border" or "origin-padding" or "origin-content") return "bg-origin";

        // bg-clip.
        if (v is "clip-border" or "clip-padding" or "clip-content" or "clip-text") return "bg-clip";

        // bg-blend.
        if (v.StartsWith("blend-", StringComparison.Ordinal)) return "bg-blend";

        // Default: a color (palette, transparent/current/inherit, arbitrary [#..]).
        return "bg-color";
    }

    private static bool IsTextSize(string v)
        => v is "xs" or "sm" or "base" or "lg" or "xl" or "2xl" or "3xl" or "4xl" or "5xl"
            or "6xl" or "7xl" or "8xl" or "9xl"
           || (v.StartsWith('[') && (v.Contains("rem", StringComparison.Ordinal)
                || v.Contains("px", StringComparison.Ordinal) || v.Contains("em", StringComparison.Ordinal)
                || v.Contains("length:", StringComparison.Ordinal)));

    private static bool IsAlign(string v)
        => v is "left" or "center" or "right" or "justify" or "start" or "end";

    private static bool IsFontWeight(string body)
        => body is "font-thin" or "font-extralight" or "font-light" or "font-normal"
            or "font-medium" or "font-semibold" or "font-bold" or "font-extrabold" or "font-black";

    private static bool IsDisplay(string body)
        => body is "block" or "inline-block" or "inline" or "inline-flex" or "table"
            or "inline-table" or "table-caption" or "table-cell" or "table-row"
            or "table-row-group" or "table-header-group" or "table-footer-group"
            or "table-column" or "table-column-group" or "flow-root" or "grid"
            or "inline-grid" or "contents" or "list-item" or "hidden";
}
