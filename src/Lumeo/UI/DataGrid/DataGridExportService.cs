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

    public static async Task ExportToExcelAsync<TItem>(
        ComponentInteropService interop,
        IEnumerable<TItem> items,
        IReadOnlyList<DataGridColumn<TItem>> columns,
        string fileName = "export.csv")
    {
        // Export as CSV with UTF-8 BOM so Excel opens it correctly
        var visibleColumns = columns.Where(c => c.Visible).ToList();
        var sb = new System.Text.StringBuilder();

        // Header
        sb.AppendLine(string.Join(",", visibleColumns.Select(c => EscapeCsv(c.Title ?? c.Field ?? ""))));

        // Rows
        foreach (var item in items)
        {
            sb.AppendLine(string.Join(",", visibleColumns.Select(c => EscapeCsv(c.GetFormattedValue(item)))));
        }

        // Prepend UTF-8 BOM (\xEF\xBB\xBF) so Excel auto-detects encoding
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var content = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = bom.Concat(content).ToArray();
        var base64 = Convert.ToBase64String(bytes);
        await interop.DownloadFile(fileName, base64, "text/csv; charset=utf-8");
    }

    public static async Task ExportToJsonAsync<TItem>(
        ComponentInteropService interop,
        IEnumerable<TItem> items,
        IReadOnlyList<DataGridColumn<TItem>> columns,
        string fileName = "export.json")
    {
        var visibleColumns = columns.Where(c => c.Visible).ToList();
        var rows = items.Select(item =>
        {
            var dict = new Dictionary<string, string?>();
            foreach (var col in visibleColumns)
            {
                var key = col.Title ?? col.Field ?? col.Id;
                dict[key] = col.GetFormattedValue(item);
            }
            return dict;
        }).ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var base64 = Convert.ToBase64String(bytes);
        await interop.DownloadFile(fileName, base64, "application/json");
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
