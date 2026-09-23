using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Lumeo.Docs.Services;

/// <summary>
/// Convention-based lookup for catalog card showcases: for a registry component named
/// <c>"Accordion"</c>, resolves the type <c>Lumeo.Docs.Shared.Showcases.AccordionShowcase</c>
/// from the docs assembly, or <c>null</c> when no such component exists yet. Deliberately has
/// no central registration list — showcases are discovered purely by name, so parallel waves
/// can add their own <c>&lt;Name&gt;Showcase.razor</c> files without touching a shared file
/// and conflicting with each other.
/// </summary>
public static class ShowcaseResolver
{
    private const string ShowcaseNamespace = "Lumeo.Docs.Shared.Showcases";

    // Resolution is a reflection lookup (Type.GetType with a full assembly-qualified-ish
    // scan) — cheap once, but the catalog page can render this for ~170 components, so
    // cache both hits and misses per component name.
    private static readonly ConcurrentDictionary<string, Type?> Cache = new();

    /// <summary>
    /// Returns the showcase component type for a registry component name, or <c>null</c>
    /// when no <c>&lt;Name&gt;Showcase</c> component exists. Cached per name.
    /// </summary>
    public static Type? Resolve(string componentName)
    {
        if (string.IsNullOrWhiteSpace(componentName)) return null;
        return Cache.GetOrAdd(componentName, static name =>
        {
            // Type.GetType with an unqualified name searches the CALLING assembly (this
            // one, Lumeo.Docs — where every showcase lives) plus corelib, so it already
            // finds showcase types without an extra Assembly.GetExecutingAssembly()
            // fallback.
            var typeName = $"{ShowcaseNamespace}.{name}Showcase";
            var type = Type.GetType(typeName);
            return type is not null && typeof(IComponent).IsAssignableFrom(type) && !type.IsAbstract
                ? type
                : null;
        });
    }

    /// <summary>
    /// Registry component name -> the <c>BlazorWebAssemblyLazyLoad</c> DLL(s) (see
    /// <c>Lumeo.Docs.csproj</c>) its live showcase needs loaded before it mounts. The
    /// <c>/components</c> catalog page never navigates to a satellite component's own
    /// route, so it never triggers <c>App.razor</c>'s route-based lazy load — without this,
    /// a showcase that references a satellite-package type (e.g. <c>&lt;FlowCanvas&gt;</c>)
    /// throws a <c>TypeLoadException</c> the instant <c>CatalogCard</c> mounts it. Only
    /// components whose showcase is actually live need an entry; static-replica exceptions
    /// (CodeEditor, RichTextEditor, FileManager, FileViewer, Map, PdfViewer, ...) are not
    /// listed here because they never reference the satellite type at all.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> SatelliteAssemblies =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["FlowCanvas"] = new[] { "Lumeo.Flow.dll" },
            ["Gantt"] = new[] { "Lumeo.Gantt.dll" },
            ["GanttChart"] = new[] { "Lumeo.Gantt.dll" },
            ["Scheduler"] = new[] { "Lumeo.Scheduler.dll" },
        };

    /// <summary>
    /// The satellite DLL(s) a component's live showcase needs loaded before it mounts, or
    /// <c>null</c> when the showcase (if any) is built entirely from core <c>Lumeo</c> types.
    /// Consumed by <see cref="Lumeo.Docs.Shared.SatelliteBoundShowcase"/> via
    /// <see cref="ISatelliteAssemblyLoader"/>.
    /// </summary>
    public static IReadOnlyList<string>? SatelliteAssembliesFor(string componentName)
        => SatelliteAssemblies.TryGetValue(componentName, out var dlls) ? dlls : null;

    /// <summary>
    /// Every discovered <c>*Showcase</c> component type in the docs assembly, sorted by
    /// name. Used by the standalone render-smoke test so newly added showcases are
    /// automatically covered without editing that test.
    /// </summary>
    public static IReadOnlyList<Type> All()
    {
        return Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.Namespace == ShowcaseNamespace
                        && t.Name.EndsWith("Showcase", StringComparison.Ordinal)
                        && typeof(IComponent).IsAssignableFrom(t)
                        && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
    }
}
