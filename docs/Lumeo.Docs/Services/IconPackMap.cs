using System.Text;

namespace Lumeo.Docs.Services;

/// <summary>
/// Data-driven resolution of the docs' semantic icon vocabulary onto ANY first-party
/// <c>Lumeo.Icons.*</c> pack class. The heavy data (<see cref="SemanticNames"/>,
/// <see cref="Default"/>, <see cref="BootstrapOverride"/>, <see cref="Synonyms"/>) lives in
/// the generated <c>IconPackMap.Data.cs</c>; this file holds the resolution mechanics.
///
/// <para>
/// For a given <c>(packKey, semantic)</c> we produce an ORDERED ladder of candidate property
/// names (<see cref="Candidates"/>): the pack-specific override (Bootstrap only), then the
/// shared default (the canonical Lucide name), the raw semantic key, digit/orientation-stripped
/// variants, and finally the cross-pack synonym list. The runtime resolver
/// (<see cref="DynamicIconResolver"/>) and the coverage test both walk this same ladder and take
/// the FIRST candidate that actually exists in the target pack (matched exactly, then by a
/// case/punctuation-insensitive normalization). Absent candidates are skipped harmlessly, so the
/// synonym lists can be generous. When nothing matches, a neutral <see cref="FallbackCandidates"/>
/// glyph (a circle) is used — that is the only "fallback" the test tolerates.
/// </para>
/// </summary>
public static partial class IconPackMap
{
    /// <summary>Neutral fallback glyphs, tried in order when no real candidate resolves.</summary>
    public static readonly string[] FallbackCandidates =
    {
        "Circle", "CircleFill", "CircleOutline", "RecordCircle", "Record",
        "StopCircle", "QuestionMarkCircle", "CircleDashed", "Square", "Stop", "SquareFill",
    };

    /// <summary>
    /// The ordered candidate property names for resolving <paramref name="semantic"/> in the
    /// pack identified by <paramref name="packKey"/>. Mirrors the generator's ladder exactly, so
    /// the coverage test and the runtime resolver never diverge.
    /// </summary>
    public static IReadOnlyList<string> Candidates(string packKey, string semantic)
    {
        var list = new List<string>(12);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(string? s)
        {
            if (!string.IsNullOrEmpty(s) && seen.Add(s!)) list.Add(s!);
        }

        // 1. Bootstrap keeps its hand-curated re-skin as the highest-priority override.
        if (packKey == "bootstrap" && BootstrapOverride.TryGetValue(semantic, out var bo))
            Add(bo);

        // 2. Shared default = the canonical first-party Lucide name.
        Default.TryGetValue(semantic, out var def);
        Add(def);

        // 3. The raw semantic key + cheap morphological variants.
        Add(semantic);
        Add(StripTrailingDigits(semantic));
        Add(StripOrientation(semantic));
        if (def is not null)
        {
            Add(StripTrailingDigits(def));
            Add(StripOrientation(def));
        }

        // 4. Cross-pack synonyms (ordered specific -> generic).
        if (Synonyms.TryGetValue(semantic, out var syn))
            foreach (var s in syn) Add(s);

        return list;
    }

    /// <summary>Case- and punctuation-insensitive key used for the fuzzy second-chance match.</summary>
    public static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    private static string StripTrailingDigits(string s)
    {
        var end = s.Length;
        while (end > 0 && char.IsDigit(s[end - 1])) end--;
        return end == s.Length ? s : s.Substring(0, end);
    }

    private static string StripOrientation(string s)
    {
        if (s.EndsWith("Horizontal", StringComparison.Ordinal)) return s.Substring(0, s.Length - "Horizontal".Length);
        if (s.EndsWith("Vertical", StringComparison.Ordinal)) return s.Substring(0, s.Length - "Vertical".Length);
        return s;
    }
}
