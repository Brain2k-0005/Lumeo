namespace Lumeo.RegistryGen;

/// <summary>
/// Detects entries in the hand-maintained catalog maps (componentToPackage, categoryMap,
/// descriptions) that name a component which no longer exists in any satellite's UI tree.
/// A name counts as existing when it is a directory directly under a UI root (a top-level
/// component) OR a <c>&lt;Name&gt;.razor</c> file anywhere beneath a UI root (a sub-component
/// such as <c>Lumeo.Maps/UI/Map/MapMarker.razor</c>).
/// </summary>
public static class StaleMapEntries
{
    public static HashSet<string> DiscoverExistingNames(IEnumerable<string> uiRoots)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in uiRoots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directory.EnumerateDirectories(root))
                names.Add(Path.GetFileName(dir));
            foreach (var file in Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories))
                names.Add(Path.GetFileNameWithoutExtension(file));
        }
        return names;
    }

    public static string[] Find(IEnumerable<string> mapKeys, ISet<string> existingNames) =>
        mapKeys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(k => !existingNames.Contains(k))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string Format(IReadOnlyCollection<string> stale) =>
        $"{stale.Count} hand-maintained map entries name a component that does not exist under any UI/ tree " +
        "(neither a UI/<Name>/ directory nor a <Name>.razor file). In tools/Lumeo.RegistryGen/Program.cs, " +
        "delete the matching line from componentToPackage, categoryMap and descriptions for each of the names below, " +
        "or restore the component.";
}
