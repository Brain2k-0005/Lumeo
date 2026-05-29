using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Lumeo.Services;

/// <summary>
/// Heavy export backend (Excel via ClosedXML, PDF via QuestPDF). Implemented in the
/// separate <c>Lumeo.DataGrid.Export</c> assembly so the ~1.6 MB of ClosedXML/QuestPDF
/// dependencies stay out of the core's eager load and can be lazy-loaded on demand.
/// </summary>
public interface IDataGridExportBackend
{
    byte[] ToExcel<TItem>(
        IEnumerable<TItem> items,
        IEnumerable<DataGridExportColumn<TItem>> columns,
        string sheetName,
        CultureInfo culture);

    byte[] ToPdf<TItem>(
        IEnumerable<TItem> items,
        IEnumerable<DataGridExportColumn<TItem>> columns,
        string title,
        CultureInfo culture);
}

/// <summary>
/// Registry that bridges the core <see cref="IDataGridExportService"/> facade to the
/// lazy-loaded <see cref="IDataGridExportBackend"/> implementation. The
/// <c>Lumeo.DataGrid.Export</c> assembly registers itself via a module initializer.
/// </summary>
public static class DataGridExportBackend
{
    private const string ExportAssemblyName = "Lumeo.DataGrid.Export";
    private static IDataGridExportBackend? _instance;

    /// <summary>Called by the export assembly's module initializer when it loads.</summary>
    public static void Register(IDataGridExportBackend backend) => _instance = backend;

    internal static IDataGridExportBackend Resolve()
    {
        if (_instance is not null) return _instance;

        // In eager scenarios (server, tests, consumers without lazy loading) the export
        // assembly is referenced but its module initializer hasn't run yet because no type
        // from it has been touched. Force-load it and run its module constructor. On WASM
        // with lazy loading the DataGrid pre-loads it via DataGridExportLoader first, so by
        // the time we get here the assembly is already in the ALC.
        try
        {
            var asm = Assembly.Load(ExportAssemblyName);
            RuntimeHelpers.RunModuleConstructor(asm.ManifestModule.ModuleHandle);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Excel/PDF export requires the '{ExportAssemblyName}' assembly, which ships " +
                "transitively with the Lumeo.DataGrid package. On Blazor WebAssembly with lazy " +
                "loading enabled, the assembly must be loaded before exporting — wire " +
                $"{nameof(DataGridExportLoader)}.{nameof(DataGridExportLoader.LoadAssembliesAsync)} " +
                "in your app startup.", ex);
        }

        return _instance ?? throw new InvalidOperationException(
            $"'{ExportAssemblyName}' loaded but did not register an export backend.");
    }
}

/// <summary>
/// Optional hook for Blazor WebAssembly apps that lazy-load the export assemblies. Set this
/// in <c>Program.cs</c> using the framework's <c>LazyAssemblyLoader</c>; the DataGrid calls
/// it before an Excel/PDF export so the heavy assemblies are fetched on demand instead of at
/// first paint. When unset (server, or WASM without lazy loading) export still works because
/// the assemblies are loaded eagerly.
/// </summary>
public static class DataGridExportLoader
{
    public static Func<IReadOnlyList<string>, Task>? LoadAssembliesAsync { get; set; }
}
