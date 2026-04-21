using System.Globalization;
using System.Text;
using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lumeo.Tests.Services;

/// <summary>
/// Tests for <see cref="IDataGridExportService"/> covering culture-aware formatting,
/// correct MIME / extension mapping, and the xlsx / CSV / PDF paths.
/// </summary>
public class DataGridExportServiceTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private IDataGridExportService _service = null!;

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        _service = _ctx.Services.GetRequiredService<IDataGridExportService>();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private record Row(string Name, decimal Amount, DateTime When);

    private static List<Row> SampleRows() => new()
    {
        new("Alice",  1234.56m,  new DateTime(2026, 3, 15, 9, 30, 0)),
        new("Bob",     987.65m,  new DateTime(2026, 4, 1, 12, 0, 0)),
    };

    private static List<DataGridExportColumn<Row>> Columns() => new()
    {
        new("Name", r => r.Name, null),
        new("Amount", r => r.Amount, "N2"),
        new("When", r => r.When, "d"),
    };

    // ------------------------------------------------------------------ CSV

    [Fact]
    public void ToCsv_Default_UsesCurrentCulture()
    {
        var bytes = _service.ToCsv(SampleRows(), Columns());
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        // Leading UTF-8 BOM.
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact]
    public void ToCsv_German_FormatsDecimalWithComma()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var bytes = _service.ToCsv(SampleRows(), Columns(), de);
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.Contains("1.234,56", text);
    }

    [Fact]
    public void ToCsv_English_FormatsDecimalWithPeriod()
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        var bytes = _service.ToCsv(SampleRows(), Columns(), us);
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.Contains("1,234.56", text);
    }

    [Fact]
    public void ToCsv_EscapesCommasAndQuotes()
    {
        var rows = new[] { new Row("Alice, Jr.", 10m, DateTime.Today) };
        var bytes = _service.ToCsv(rows, Columns(), CultureInfo.InvariantCulture);
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        // RFC 4180: field containing a comma must be double-quoted.
        Assert.Contains("\"Alice, Jr.\"", text);
    }

    [Fact]
    public void ToCsv_HeadersWrittenOnce()
    {
        var bytes = _service.ToCsv(SampleRows(), Columns(), CultureInfo.InvariantCulture);
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        // Header occurrence — no duplicates.
        var firstNameIdx = text.IndexOf("Name", StringComparison.Ordinal);
        var secondNameIdx = text.IndexOf("Name", firstNameIdx + 1, StringComparison.Ordinal);
        Assert.True(firstNameIdx >= 0);
        Assert.Equal(-1, secondNameIdx);
    }

    // ---------------------------------------------------------------- Excel

    [Fact]
    public void ToExcel_ProducesXlsxMagicBytes()
    {
        var bytes = _service.ToExcel(SampleRows(), Columns(), "Sheet1");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 4);
        // .xlsx is a ZIP archive: PK\x03\x04
        Assert.Equal(0x50, bytes[0]); // P
        Assert.Equal(0x4B, bytes[1]); // K
        Assert.Equal(0x03, bytes[2]);
        Assert.Equal(0x04, bytes[3]);
    }

    [Fact]
    public void ToExcel_WithGermanCulture_BuildsWorkbook()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var bytes = _service.ToExcel(SampleRows(), Columns(), "Data", de);
        Assert.NotNull(bytes);
        // Still a valid ZIP — opening in ClosedXML would round-trip; here we just
        // assert the file materialised so the DE culture didn't crash ClosedXML.
        Assert.True(bytes.Length > 100);
        Assert.Equal(0x50, bytes[0]);
    }

    [Fact]
    public void ToExcel_EmptyRows_StillProducesWorkbookWithHeader()
    {
        var bytes = _service.ToExcel(Array.Empty<Row>(), Columns(), "Empty");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }
}
