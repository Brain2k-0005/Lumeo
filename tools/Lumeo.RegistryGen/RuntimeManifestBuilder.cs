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

        // Root-level shared types: the unified Lumeo.Size / Side / Align / Orientation enums,
        // Density, TriggerSlot, etc. Every component references these, so they are part of the
        // runtime. Top-directory only (the UI/ component dirs are vendored per-component).
        foreach (var f in Directory.EnumerateFiles(coreSrcRoot, "*.cs", SearchOption.TopDirectoryOnly))
            files.Add(Rel(coreSrcRoot, f));

        var imports = Path.Combine(coreSrcRoot, "_Imports.razor");
        if (File.Exists(imports)) files.Add("_Imports.razor");

        // DismissEventArgs lives under UI/Overlay/ but is shared event-args (Dialog/Sheet/Drawer
        // OnBeforeClose), not the overlay host — include just that one file.
        if (File.Exists(Path.Combine(coreSrcRoot, "UI", "Overlay", "DismissEventArgs.cs")))
            files.Add("UI/Overlay/DismissEventArgs.cs");

        // The runtime is the pure C# substrate (plus DismissEventArgs) — NO UI components. The
        // service layer is decoupled from UI components (e.g. SignaturePadInit is generic, not typed
        // to the SignaturePad component), so the substrate has no UI references to drag in. Keeping
        // UI components OUT also avoids a namespace clash: they'd live under the root Lumeo namespace
        // that `@using Lumeo` pulls in for the enums, colliding with the consumer's own vendored
        // Button/etc. The overlay host (OverlayProvider) is likewise excluded — consumers reach
        // overlays via OverlayService and add <OverlayProvider/> separately. No skip-these components.
        var components = new List<string>();

        files.Sort(StringComparer.Ordinal);
        return (files, components);
    }

    private static bool IsSource(string f)
        => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
        || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);

    private static string Rel(string root, string f)
        => Path.GetRelativePath(root, f).Replace('\\', '/');
}
