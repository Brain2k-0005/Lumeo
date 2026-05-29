using System.Globalization;
using System.Text;

namespace Lumeo.Services;

/// <summary>
/// Default <see cref="IDataGridExportService"/> implementation. Registered by <c>AddLumeo()</c>
/// as a scoped service. CSV and the download trigger are dependency-free and live here; Excel
/// (ClosedXML) and PDF (QuestPDF) are delegated to the <see cref="IDataGridExportBackend"/> in
/// the separate <c>Lumeo.DataGrid.Export</c> assembly so their ~1.6 MB of dependencies stay out
/// of the core's eager load and can be lazy-loaded on demand.
/// </summary>
public sealed class DataGridExportService : IDataGridExportService
{
    private readonly IComponentInteropService _interop;

    public DataGridExportService(IComponentInteropService interop)
    {
        _interop = interop;
    }

    // ------------------------------------------------------------------ CSV

    public byte[] ToCsv<TItem>(
        IEnumerable<TItem> items,
        IEnumerable<DataGridExportColumn<TItem>> columns,
        CultureInfo? culture = null)
    {
        var effectiveCulture = culture ?? CultureInfo.CurrentCulture;
        var cols = columns.ToList();
        var sb = new StringBuilder();

        // Header row.
        for (var i = 0; i < cols.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(EscapeCsv(cols[i].Header));
        }
        sb.Append("\r\n");

        // Data rows.
        foreach (var item in items)
        {
            for (var i = 0; i < cols.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsv(FormatValue(cols[i].Accessor(item), cols[i].Format, effectiveCulture)));
            }
            sb.Append("\r\n");
        }

        // UTF-8 BOM so Excel on Windows detects encoding correctly.
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[bom.Length + body.Length];
        Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
        Buffer.BlockCopy(body, 0, result, bom.Length, body.Length);
        return result;
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var needsQuoting = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
        if (!needsQuoting) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // ---------------------------------------------------------- Excel / PDF

    public byte[] ToExcel<TItem>(
        IEnumerable<TItem> items,
        IEnumerable<DataGridExportColumn<TItem>> columns,
        string sheetName = "Sheet1",
        CultureInfo? culture = null)
        => DataGridExportBackend.Resolve().ToExcel(
            items, columns,
            string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName,
            culture ?? CultureInfo.CurrentCulture);

    public byte[] ToPdf<TItem>(
        IEnumerable<TItem> items,
        IEnumerable<DataGridExportColumn<TItem>> columns,
        string title = "Export",
        CultureInfo? culture = null)
        => DataGridExportBackend.Resolve().ToPdf(
            items, columns, title, culture ?? CultureInfo.CurrentCulture);

    // ------------------------------------------------------------- Download

    public async Task DownloadAsync(byte[] bytes, string fileName, string mimeType)
    {
        if (bytes is null) throw new ArgumentNullException(nameof(bytes));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name is required.", nameof(fileName));
        var base64 = Convert.ToBase64String(bytes);
        await _interop.DownloadFile(fileName, base64, mimeType ?? "application/octet-stream");
    }

    // ---------------------------------------------------------- Formatting

    private static string FormatValue(object? raw, string? format, CultureInfo culture)
    {
        if (raw is null) return string.Empty;
        if (!string.IsNullOrEmpty(format) && raw is IFormattable f)
            return f.ToString(format, culture);
        if (raw is IFormattable plainFormattable)
            return plainFormattable.ToString(null, culture);
        return raw.ToString() ?? string.Empty;
    }
}
