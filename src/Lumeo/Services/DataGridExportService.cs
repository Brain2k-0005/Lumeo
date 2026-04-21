using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Lumeo.Services;

/// <summary>
/// Default <see cref="IDataGridExportService"/> implementation. Registered by <c>AddLumeo()</c>
/// as a scoped service.
/// </summary>
public sealed class DataGridExportService : IDataGridExportService
{
    private readonly IComponentInteropService _interop;

    // QuestPDF requires a license to be set exactly once per process. We pick Community here
    // which is free for individuals and companies below the revenue threshold defined at
    // https://www.questpdf.com/license — commercial users should replace this with their own
    // paid license before shipping, e.g. `QuestPDF.Settings.License = LicenseType.Professional;`
    // in their app's startup. We guard with a flag so changing this on the consumer side wins.
    private static readonly object _licenseLock = new();
    private static bool _licenseInitialized;

    public DataGridExportService(IComponentInteropService interop)
    {
        _interop = interop;
        // QuestPDF license init is deferred to ToPdf() — touching QuestPDF.Settings
        // at construction triggers a native-library probe that throws on browser-wasm.
    }

    private static void EnsureQuestPdfLicense()
    {
        if (_licenseInitialized) return;
        lock (_licenseLock)
        {
            if (_licenseInitialized) return;
            QuestPDF.Settings.License = LicenseType.Community;
            _licenseInitialized = true;
        }
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

    // ---------------------------------------------------------------- Excel

    public byte[] ToExcel<TItem>(
        IEnumerable<TItem> items,
        IEnumerable<DataGridExportColumn<TItem>> columns,
        string sheetName = "Sheet1",
        CultureInfo? culture = null)
    {
        var effectiveCulture = culture ?? CultureInfo.CurrentCulture;
        var cols = columns.ToList();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName);

        // Header row: bold, muted background.
        for (var c = 0; c < cols.Count; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Value = cols[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E5E7EB");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }

        // Data rows.
        var row = 2;
        foreach (var item in items)
        {
            for (var c = 0; c < cols.Count; c++)
            {
                var cell = sheet.Cell(row, c + 1);
                var raw = cols[c].Accessor(item);
                SetExcelCellValue(cell, raw, cols[c].Format, effectiveCulture);
            }
            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void SetExcelCellValue(IXLCell cell, object? value, string? format, CultureInfo culture)
    {
        if (value is null)
        {
            cell.Value = string.Empty;
            return;
        }

        // ClosedXML picks up the workbook/host locale for date and number formats. We pass the
        // native typed value (DateTime, decimal, …) plus a format string — Excel renders those
        // using the user's locale when opening the file. For text cells we format with the
        // supplied culture so strings mirror the exported CSV.
        switch (value)
        {
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = format ?? DefaultDateFormat(culture);
                break;
            case DateTimeOffset dto:
                cell.Value = dto.DateTime;
                cell.Style.DateFormat.Format = format ?? DefaultDateFormat(culture);
                break;
            case DateOnly dOnly:
                cell.Value = dOnly.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = format ?? DefaultDateOnlyFormat(culture);
                break;
            case bool b:
                cell.Value = b;
                break;
            case decimal dec:
                cell.Value = dec;
                if (format is not null) cell.Style.NumberFormat.Format = format;
                break;
            case double d:
                cell.Value = d;
                if (format is not null) cell.Style.NumberFormat.Format = format;
                break;
            case float f:
                cell.Value = f;
                if (format is not null) cell.Style.NumberFormat.Format = format;
                break;
            case int or long or short or byte or uint or ulong or ushort or sbyte:
                cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (format is not null) cell.Style.NumberFormat.Format = format;
                break;
            default:
                cell.Value = FormatValue(value, format, culture);
                break;
        }
    }

    private static string DefaultDateFormat(CultureInfo culture)
    {
        // Normalise .NET date format patterns (uppercase M for month) to the Excel variant
        // (lowercase m for month) — Excel treats lowercase m as minutes inside a time context
        // but as months outside it, which matches what .NET produces.
        var shortDate = culture.DateTimeFormat.ShortDatePattern;
        var shortTime = culture.DateTimeFormat.ShortTimePattern;
        return ConvertToExcelPattern(shortDate) + " " + shortTime.Replace("tt", "AM/PM");
    }

    private static string DefaultDateOnlyFormat(CultureInfo culture)
        => ConvertToExcelPattern(culture.DateTimeFormat.ShortDatePattern);

    private static string ConvertToExcelPattern(string netPattern)
        => netPattern.Replace("MMMM", "mmmm").Replace("MMM", "mmm").Replace("MM", "mm").Replace("M", "m");

    // ------------------------------------------------------------------ PDF

    public byte[] ToPdf<TItem>(
        IEnumerable<TItem> items,
        IEnumerable<DataGridExportColumn<TItem>> columns,
        string title = "Export",
        CultureInfo? culture = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(
                "QuestPDF-backed PDF export requires a server-side runtime — browser-wasm is not supported. " +
                "Use ToCsv / ToExcel on WASM, or perform PDF generation in a Blazor Server / API host.");
        }
        EnsureQuestPdfLicense();
        var effectiveCulture = culture ?? CultureInfo.CurrentCulture;
        var cols = columns.ToList();
        var data = items.ToList();
        var timestamp = DateTime.Now.ToString("g", effectiveCulture);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken4));

                page.Header().Column(col =>
                {
                    col.Item().Text(title).FontSize(16).SemiBold();
                    col.Item().Text(timestamp).FontSize(9).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(cd =>
                    {
                        foreach (var _ in cols) cd.RelativeColumn();
                    });

                    // Header row
                    table.Header(header =>
                    {
                        foreach (var col in cols)
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(col.Header).SemiBold();
                        }
                    });

                    // Data rows
                    foreach (var item in data)
                    {
                        foreach (var col in cols)
                        {
                            var text = FormatValue(col.Accessor(item), col.Format, effectiveCulture);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(text);
                        }
                    }
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    x.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }

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
