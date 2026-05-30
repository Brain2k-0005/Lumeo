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
                $"Excel/PDF export requires the '{ExportAssemblyName}' assembly. " +
                "It ships inside the 'Lumeo.DataGrid' NuGet package — if your app references only " +
                "the core 'Lumeo' package, add 'Lumeo.DataGrid' to enable Excel/PDF (CSV needs no " +
                "extra package). On Blazor WebAssembly that lazy-loads these assemblies, also " +
                $"await {nameof(DataGridExportLoader)}.{nameof(DataGridExportLoader.EnsureExcelAssembliesAsync)}() / " +
                $"{nameof(DataGridExportLoader.EnsurePdfAssembliesAsync)}() before a direct service call " +
                $"(the DataGrid component does this for you), and wire " +
                $"{nameof(DataGridExportLoader)}.{nameof(DataGridExportLoader.LoadAssembliesAsync)} in startup.", ex);
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
/// <remarks>
/// The DataGrid component awaits <see cref="EnsureExcelAssembliesAsync"/> /
/// <see cref="EnsurePdfAssembliesAsync"/> automatically before a toolbar export. If you call
/// <see cref="IDataGridExportService"/>.<c>ToExcel</c>/<c>ToPdf</c> <em>directly</em> from your
/// own code on a WASM app that lazy-marks these assemblies, await the matching Ensure method
/// first — the synchronous service methods cannot fetch a lazy assembly on their own.
/// </remarks>
public static class DataGridExportLoader
{
    /// <summary>
    /// Set by the app (WASM only) to the framework's
    /// <c>LazyAssemblyLoader.LoadAssembliesAsync</c>. Left <c>null</c> on server / eager apps,
    /// where the Ensure methods become no-ops because the assemblies are already loaded.
    /// </summary>
    public static Func<IReadOnlyList<string>, Task>? LoadAssembliesAsync { get; set; }

    /// <summary>The export backend assembly plus the ClosedXML closure needed for Excel.</summary>
    public static readonly IReadOnlyList<string> ExcelAssemblies = new[]
    {
        "Lumeo.DataGrid.Export.dll", "ClosedXML.dll", "ClosedXML.Parser.dll",
        "DocumentFormat.OpenXml.dll", "DocumentFormat.OpenXml.Framework.dll",
        "ExcelNumberFormat.dll", "RBush.dll", "SixLabors.Fonts.dll", "System.IO.Packaging.dll",
    };

    /// <summary>The export backend assembly plus QuestPDF, needed for PDF.</summary>
    public static readonly IReadOnlyList<string> PdfAssemblies = new[]
    {
        "Lumeo.DataGrid.Export.dll", "QuestPDF.dll",
    };

    /// <summary>
    /// Ensures the Excel backend assemblies are loaded before a direct
    /// <c>IDataGridExportService.ToExcel</c> call on a lazy-loading WASM app. No-op when
    /// <see cref="LoadAssembliesAsync"/> is unset (server / eager apps).
    /// </summary>
    public static Task EnsureExcelAssembliesAsync()
        => LoadAssembliesAsync is { } load ? load(ExcelAssemblies) : Task.CompletedTask;

    /// <summary>
    /// Ensures the PDF backend assemblies are loaded before a direct
    /// <c>IDataGridExportService.ToPdf</c> call on a lazy-loading WASM app. No-op when
    /// <see cref="LoadAssembliesAsync"/> is unset (server / eager apps).
    /// </summary>
    public static Task EnsurePdfAssembliesAsync()
        => LoadAssembliesAsync is { } load ? load(PdfAssemblies) : Task.CompletedTask;
}
