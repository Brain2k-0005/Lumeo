using Lumeo.Services;

namespace Lumeo;

public static class DataGridExportService
{
    public static async Task ExportToCsvAsync<TItem>(
        ComponentInteropService interop,
        IEnumerable<TItem> items,
        IReadOnlyList<DataGridColumn<TItem>> columns,
        string fileName = "export.csv")
    {
        var visibleColumns = columns.Where(c => c.Visible).ToList();
        var sb = new System.Text.StringBuilder();

        // Header
        sb.AppendLine(string.Join(",", visibleColumns.Select(c => EscapeCsv(c.Title ?? c.Field ?? ""))));

        // Rows
        foreach (var item in items)
        {
            sb.AppendLine(string.Join(",", visibleColumns.Select(c => EscapeCsv(c.GetFormattedValue(item)))));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var base64 = Convert.ToBase64String(bytes);
        await interop.DownloadFile(fileName, base64, "text/csv");
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
