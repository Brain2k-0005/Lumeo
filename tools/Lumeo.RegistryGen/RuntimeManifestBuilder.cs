namespace Lumeo.RegistryGen;

/// <summary>
/// Builds the registry's top-level <c>runtime</c> manifest: the shared C# substrate plus the
/// overlay host that the CLI vendors verbatim (keeping the <c>Lumeo</c> namespace) when a project
/// is in standalone / NuGet-free mode. The list is derived from the actual source tree so it never
/// drifts as the library grows — same generate-from-truth principle as the component catalog.
/// </summary>
public static class RuntimeManifestBuilder
{
    // Whole non-UI folders whose .cs/.razor are the shared substrate every component compiles against
    // (Cx/LumeoIds, the injected services + their interfaces, the AddLumeo DI extension, attributes,
    // theme token C#).
    private static readonly string[] SubstrateDirs = { "Internal", "Services", "Extensions", "Attributes", "Theming" };

    // Overlay host components — infrastructure (one <OverlayProvider/> in the layout), not a
    // user-added component. Their kebab keys are reported so the CLI's add-BFS skips re-vendoring
    // them into the user namespace (the runtime already provides them under Lumeo).
    private static readonly string[] OverlayHostDirs = { "UI/Overlay", "UI/OverlayForm" };

    /// <summary>
    /// Enumerates the runtime closure under <paramref name="coreSrcRoot"/> (= the <c>src/Lumeo</c>
    /// directory). Returns runtime file paths relative to that root (forward-slashed, sorted) and the
    /// kebab keys of the overlay-host components.
    /// </summary>
    public static (List<string> Files, List<string> Components) Build(string coreSrcRoot)
    {
        var files = new List<string>();

        foreach (var dir in SubstrateDirs)
        {
            var abs = Path.Combine(coreSrcRoot, dir);
            if (!Directory.Exists(abs)) continue;
            foreach (var f in Directory.EnumerateFiles(abs, "*.*", SearchOption.AllDirectories))
                if (IsSource(f)) files.Add(Rel(coreSrcRoot, f));
        }

        var imports = Path.Combine(coreSrcRoot, "_Imports.razor");
        if (File.Exists(imports)) files.Add("_Imports.razor");

        var components = new List<string>();
        foreach (var rel in OverlayHostDirs)
        {
            var abs = Path.Combine(coreSrcRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(abs)) continue;
            components.Add(Kebab(Path.GetFileName(abs)));
            foreach (var f in Directory.EnumerateFiles(abs, "*.*", SearchOption.AllDirectories))
                if (IsSource(f)) files.Add(Rel(coreSrcRoot, f));
        }

        files.Sort(StringComparer.Ordinal);
        components.Sort(StringComparer.Ordinal);
        return (files, components);
    }

    private static bool IsSource(string f)
        => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
        || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);

    private static string Rel(string root, string f)
        => Path.GetRelativePath(root, f).Replace('\\', '/');

    // PascalCase dir name -> kebab key ("Overlay" -> "overlay", "OverlayForm" -> "overlay-form"),
    // matching the component keys RegistryGen emits for these dirs.
    private static string Kebab(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 4);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsUpper(c) && i > 0) sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
