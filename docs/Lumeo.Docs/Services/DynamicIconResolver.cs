using System.Reflection;

namespace Lumeo.Docs.Services;

/// <summary>
/// Resolves a docs semantic icon name (e.g. <c>"Search"</c>, <c>"Home"</c>) to a concrete
/// <see cref="Lumeo.IconSource"/> from whichever first-party pack the customizer has selected —
/// the mechanism behind the site-wide icon re-skin (<c>DynamicIcon</c>) and the picker previews.
///
/// <para>
/// Uses the exact reflection pattern from <c>IconsGallery.razor</c>: after the pack assembly is
/// lazy-loaded, reflect its static <see cref="Lumeo.IconSource"/> properties ONCE into a
/// name-&gt;source dictionary (plus a normalized index for the fuzzy second-chance match) and
/// cache it. Resolution walks <see cref="IconPackMap.Candidates"/> and returns the first present
/// property, then a neutral <see cref="IconPackMap.FallbackCandidates"/> glyph. Packs themselves
/// never reflect — this reflection is docs-only.
/// </para>
/// </summary>
public sealed class DynamicIconResolver
{
    // packKey -> reflected index. Absent = not yet built (assembly not loaded); we retry until the
    // lazy dll lands rather than caching a negative, so a freshly-loaded pack resolves next render.
    private readonly Dictionary<string, PackIndex> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// True once <paramref name="packKey"/>'s assembly is loaded and its index is built —
    /// the picker uses this to show a per-pack loading state until the preview can render.
    /// </summary>
    public bool IsReady(string packKey) => TryEnsure(packKey, out _);

    /// <summary>
    /// Resolve <paramref name="semantic"/> for the active <paramref name="packKey"/>. If that pack
    /// is a first-party pack whose assembly has not loaded yet, degrade to Lucide (always eager) so
    /// the chrome keeps meaningful icons during the brief lazy-load, then upgrades on the next render.
    /// </summary>
    public Lumeo.IconSource Resolve(string packKey, string semantic)
    {
        if (!TryEnsure(packKey, out var idx))
        {
            // Unknown key or not-yet-loaded pack: fall back to Lucide (eager, always resolvable).
            if (!TryEnsure("lucide", out idx))
                return Lumeo.IconSource.Stroke(string.Empty); // impossible in practice
        }

        foreach (var cand in IconPackMap.Candidates(packKey, semantic))
            if (idx!.TryGet(cand, out var src)) return src;

        foreach (var fb in IconPackMap.FallbackCandidates)
            if (idx!.TryGet(fb, out var src)) return src;

        return idx!.First;
    }

    private bool TryEnsure(string packKey, out PackIndex? idx)
    {
        if (_cache.TryGetValue(packKey, out var cached)) { idx = cached; return true; }

        var pack = IconPackCatalog.Find(packKey);
        if (pack is null) { idx = null; return false; }

        // Type.GetType returns null until the (lazy) assembly is loaded into the app domain.
        var type = Type.GetType($"{pack.TypeName}, {pack.AssemblyName}");
        if (type is null) { idx = null; return false; }

        idx = PackIndex.Build(type);
        _cache[packKey] = idx;
        return true;
    }

    private sealed class PackIndex
    {
        private readonly Dictionary<string, Lumeo.IconSource> _byName;
        private readonly Dictionary<string, Lumeo.IconSource> _normalized;
        public Lumeo.IconSource First { get; }

        private PackIndex(Dictionary<string, Lumeo.IconSource> byName,
                          Dictionary<string, Lumeo.IconSource> normalized,
                          Lumeo.IconSource first)
        {
            _byName = byName;
            _normalized = normalized;
            First = first;
        }

        public static PackIndex Build(Type packClass)
        {
            var byName = packClass
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(Lumeo.IconSource))
                .ToDictionary(p => p.Name, p => (Lumeo.IconSource)p.GetValue(null)!, StringComparer.Ordinal);

            var normalized = new Dictionary<string, Lumeo.IconSource>(StringComparer.Ordinal);
            foreach (var (name, src) in byName)
                normalized.TryAdd(IconPackMap.Normalize(name), src);

            var first = byName.Count > 0 ? byName.Values.First() : Lumeo.IconSource.Stroke(string.Empty);
            return new PackIndex(byName, normalized, first);
        }

        public bool TryGet(string candidate, out Lumeo.IconSource src) =>
            _byName.TryGetValue(candidate, out src!) ||
            _normalized.TryGetValue(IconPackMap.Normalize(candidate), out src!);
    }
}
