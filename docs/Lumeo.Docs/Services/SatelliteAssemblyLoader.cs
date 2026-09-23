using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace Lumeo.Docs.Services;

/// <summary>
/// Abstraction over Blazor WASM's <c>LazyAssemblyLoader</c> so a catalog showcase for a
/// satellite-package component (Flow, Gantt, GanttChart, Scheduler, ...) can ensure its
/// assembly is loaded before a live showcase mounts it (see
/// <see cref="ShowcaseResolver.SatelliteAssembliesFor"/>), without the catalog page itself
/// needing route-based lazy-load wiring for every component that ever gets a satellite
/// showcase. <c>LazyAssemblyLoader</c> is a WASM-host-only framework service bUnit cannot
/// supply (the same story as <c>AllComponentPagesRenderTests</c>' IconPage exclusion), so
/// tests register a no-op fake instead (<c>DocsTestContext.AddDocsServices</c>) that resolves
/// immediately — the docs test project references every satellite project directly, so the
/// real showcase types are already loaded in the test process either way.
/// </summary>
public interface ISatelliteAssemblyLoader
{
    /// <summary>
    /// Ensures every DLL in <paramref name="assemblyNames"/> has been fetched and loaded.
    /// Safe to call repeatedly, including concurrently from several catalog cards at once —
    /// already-loaded (or in-flight) names are not re-fetched. Returns <c>false</c> instead of
    /// throwing when the fetch fails, so a flaky network degrades the showcase to its
    /// thumbnail fallback rather than crashing the catalog page.
    /// </summary>
    ValueTask<bool> EnsureLoadedAsync(IReadOnlyList<string> assemblyNames);
}

/// <summary>Real implementation, backed by the framework's <c>LazyAssemblyLoader</c>.</summary>
public sealed class SatelliteAssemblyLoader : ISatelliteAssemblyLoader
{
    private readonly LazyAssemblyLoader _loader;
    private readonly HashSet<string> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SatelliteAssemblyLoader(LazyAssemblyLoader loader) => _loader = loader;

    public async ValueTask<bool> EnsureLoadedAsync(IReadOnlyList<string> assemblyNames)
    {
        if (assemblyNames.Count == 0) return true;
        if (assemblyNames.All(_loaded.Contains)) return true;

        await _gate.WaitAsync();
        try
        {
            var missing = assemblyNames.Where(n => !_loaded.Contains(n)).ToList();
            if (missing.Count == 0) return true;

            await _loader.LoadAssembliesAsync(missing);
            foreach (var name in missing) _loaded.Add(name);
            return true;
        }
        catch
        {
            // Left out of _loaded so a later render (e.g. a retry scroll) gets another chance.
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }
}
