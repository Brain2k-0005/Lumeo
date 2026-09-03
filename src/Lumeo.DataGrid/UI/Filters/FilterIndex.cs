namespace Lumeo;

/// <summary>A lookup over a field schema: fields by path, the children of a branch, and a flat
/// list for deep search. Built once per schema by <see cref="Filters"/>.</summary>
public sealed class FilterIndex
{
    private const char Separator = '\0';
    private readonly Dictionary<string, FilterField> _byPath = new();
    private readonly Dictionary<string, List<FilterField>> _childrenOf = new();

    /// <summary>Every field with its path, in schema order.</summary>
    public IReadOnlyList<(FilterField Field, IReadOnlyList<string> Path)> All { get; }
    /// <summary>The top-level fields.</summary>
    public IReadOnlyList<FilterField> Roots { get; }

    public static string Join(IEnumerable<string> path) => string.Join(Separator, path);
    public static IReadOnlyList<string> Split(string key) => key.Length == 0 ? Array.Empty<string>() : key.Split(Separator);

    public FilterIndex(IReadOnlyList<FilterField> fields)
    {
        var all = new List<(FilterField, IReadOnlyList<string>)>();
        var roots = new List<FilterField>();
        void Walk(IReadOnlyList<FilterField> list, IReadOnlyList<string> parentPath, string parentKey)
        {
            var accepted = new List<FilterField>();
            var seen = new HashSet<string>();
            foreach (var field in list)
            {
                if (string.IsNullOrEmpty(field.Id) || !seen.Add(field.Id)) continue;
                var path = parentPath.Append(field.Id).ToList();
                var key = Join(path);
                if (_byPath.ContainsKey(key)) continue;
                _byPath[key] = field;
                all.Add((field, path));
                accepted.Add(field);
                if (field.IsBranch) Walk(field.Fields!, path, key);
            }
            _childrenOf[parentKey] = accepted;
            if (parentKey == "") roots.AddRange(accepted);
        }
        Walk(fields, Array.Empty<string>(), "");
        All = all;
        Roots = roots;
    }

    /// <summary>The field at a path, or null.</summary>
    public FilterField? Get(IReadOnlyList<string> path) => _byPath.GetValueOrDefault(Join(path));

    /// <summary>The fields along a path, root first; stops at the first unknown segment.</summary>
    public IReadOnlyList<FilterField> Chain(IReadOnlyList<string> path)
    {
        var chain = new List<FilterField>();
        for (var i = 1; i <= path.Count; i++)
        {
            if (Get(path.Take(i).ToList()) is not { } field) break;
            chain.Add(field);
        }
        return chain;
    }

    /// <summary>The children of a branch, or the roots for an empty path.</summary>
    public IReadOnlyList<FilterField> Children(IReadOnlyList<string> path)
        => _childrenOf.TryGetValue(path.Count == 0 ? "" : Join(path), out var list) ? list : Array.Empty<FilterField>();

    /// <summary>The labels along a path joined by a separator; falls back to the raw ids.</summary>
    public string FormatPath(IReadOnlyList<string> path, string separator)
    {
        var chain = Chain(path);
        return chain.Count == 0 ? string.Join(separator, path) : string.Join(separator, chain.Select(f => f.Label));
    }

    /// <summary>Case-insensitive match on the label and the keywords.</summary>
    public static bool Matches(FilterField field, string normalizedQuery)
    {
        if (normalizedQuery.Length == 0) return true;
        if (field.Label.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase)) return true;
        return field.Keywords?.Any(k => k.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase)) == true;
    }

    /// <summary>Every selectable field anywhere in the schema whose label or keywords match.</summary>
    public IReadOnlyList<(FilterField Field, IReadOnlyList<string> Path)> SearchDeep(string normalizedQuery, int limit = 200)
    {
        if (normalizedQuery.Length == 0) return Array.Empty<(FilterField, IReadOnlyList<string>)>();
        return All.Where(e => e.Field.IsSelectable && Matches(e.Field, normalizedQuery)).Take(limit).ToList();
    }

    /// <summary>Case-insensitive match on an option's label and keywords.</summary>
    public static bool Matches(FilterChoice option, string normalizedQuery)
    {
        if (normalizedQuery.Length == 0) return true;
        if (option.Label.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase)) return true;
        return option.Keywords?.Any(k => k.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase)) == true;
    }

    /// <summary>Applies the exclusive rule to a new selection: an exclusive option that arrives
    /// clears the others; an ordinary one that arrives clears the exclusive ones.</summary>
    public static IReadOnlyList<string> ApplyExclusive(IReadOnlyList<string> next, IReadOnlyList<string> previous, Func<string, bool> isExclusive)
    {
        var held = previous.ToHashSet();
        var added = next.Where(v => !held.Contains(v)).ToList();
        if (added.Count == 0) return next;
        var arrived = added.LastOrDefault(isExclusive);
        if (arrived is not null) return new[] { arrived };
        var kept = next.Where(v => !isExclusive(v)).ToList();
        return kept.Count == next.Count ? next : kept;
    }
}
