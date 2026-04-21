using System.Globalization;

namespace Lumeo.Services;

/// <summary>
/// Declarative description of a single column to export. <paramref name="Header"/> is the
/// column heading, <paramref name="Accessor"/> returns the cell value for a given row, and
/// <paramref name="Format"/> is an optional format string applied to <see cref="IFormattable"/>
/// values (e.g. "N2", "yyyy-MM-dd", "C").
/// </summary>
public record DataGridExportColumn<TItem>(string Header, Func<TItem, object?> Accessor, string? Format = null);

/// <summary>
/// Produces CSV / Excel (.xlsx) / PDF (A4) byte arrays from a DataGrid-style data set and
/// can trigger a browser download via JS interop.
/// </summary>
/// <remarks>
/// Excel export uses <c>ClosedXML</c>. PDF export uses <c>QuestPDF</c> under its Community
/// license — if you ship this in a commercial product with &gt; $1M annual revenue you must
/// purchase a QuestPDF Professional or Enterprise license. See <c>https://www.questpdf.com/license</c>.
/// The CSV path is dependency-free and safe for untrusted text (escapes commas, quotes, newlines).
/// <para>
/// All three export methods accept an optional <see cref="CultureInfo"/>. When null (default)
/// the export uses <see cref="CultureInfo.CurrentCulture"/>, which honours ASP.NET request-localization
/// so a <c>de-DE</c> user receives German-formatted dates and numbers in the export.
/// </para>
/// </remarks>
public interface IDataGridExportService
{
    /// <summary>Generate a UTF-8 .xlsx workbook. Header row is bolded with a muted background.</summary>
    byte[] ToExcel<TItem>(
        IEnumerable<TItem> items,
        IEnumerable<DataGridExportColumn<TItem>> columns,
        string sheetName = "Sheet1",
        CultureInfo? culture = null);

    /// <summary>Generate a portrait A4 PDF with title, timestamp, and data table.</summary>
    byte[] ToPdf<TItem>(
        IEnumerable<TItem> items,
        IEnumerable<DataGridExportColumn<TItem>> columns,
        string title = "Export",
        CultureInfo? culture = null);

    /// <summary>Generate RFC 4180 CSV prefixed with a UTF-8 BOM so Excel opens it correctly.</summary>
    byte[] ToCsv<TItem>(
        IEnumerable<TItem> items,
        IEnumerable<DataGridExportColumn<TItem>> columns,
        CultureInfo? culture = null);

    /// <summary>Trigger a browser download for the given bytes via JS interop.</summary>
    Task DownloadAsync(byte[] bytes, string fileName, string mimeType);
}
