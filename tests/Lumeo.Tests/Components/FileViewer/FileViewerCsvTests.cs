using Xunit;

namespace Lumeo.Tests.Components.FileViewerComponent;

public class FileViewerCsvTests
{
    [Fact]
    public void Parses_Simple_Csv_With_Header()
    {
        const string csv = "name,age,city\nAlice,30,Berlin\nBob,25,Munich";
        var (rows, truncated) = Lumeo.FileViewer.ParseCsv(csv, maxRows: 100);

        Assert.False(truncated);
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "name", "age", "city" }, rows[0]);
        Assert.Equal(new[] { "Alice", "30", "Berlin" }, rows[1]);
        Assert.Equal(new[] { "Bob", "25", "Munich" }, rows[2]);
    }

    [Fact]
    public void Handles_Quoted_Fields_With_Embedded_Commas()
    {
        const string csv = "name,address\n\"Doe, John\",\"123 Main St, Apt 4\"";
        var (rows, _) = Lumeo.FileViewer.ParseCsv(csv, maxRows: 100);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Doe, John", rows[1][0]);
        Assert.Equal("123 Main St, Apt 4", rows[1][1]);
    }

    [Fact]
    public void Handles_Escaped_DoubleQuotes_Inside_Quoted_Field()
    {
        const string csv = "quote\n\"She said \"\"hello\"\"\"";
        var (rows, _) = Lumeo.FileViewer.ParseCsv(csv, maxRows: 100);

        Assert.Equal(2, rows.Count);
        Assert.Equal("She said \"hello\"", rows[1][0]);
    }

    [Fact]
    public void Detects_Tsv_When_First_Line_Has_Tab_And_No_Comma()
    {
        const string tsv = "name\tage\nAlice\t30";
        var (rows, _) = Lumeo.FileViewer.ParseCsv(tsv, maxRows: 100);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "name", "age" }, rows[0]);
        Assert.Equal(new[] { "Alice", "30" }, rows[1]);
    }

    [Fact]
    public void Truncates_At_MaxRows_And_Reports_Truncation()
    {
        var csv = "h\n" + string.Join("\n", Enumerable.Range(1, 50).Select(i => i.ToString()));
        var (rows, truncated) = Lumeo.FileViewer.ParseCsv(csv, maxRows: 10);

        Assert.True(truncated);
        Assert.Equal(10, rows.Count);
    }

    [Fact]
    public void Handles_Crlf_Line_Endings()
    {
        const string csv = "a,b\r\n1,2\r\n3,4";
        var (rows, _) = Lumeo.FileViewer.ParseCsv(csv, maxRows: 100);

        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "1", "2" }, rows[1]);
        Assert.Equal(new[] { "3", "4" }, rows[2]);
    }

    [Fact]
    public void Empty_Input_Yields_No_Rows()
    {
        var (rows, truncated) = Lumeo.FileViewer.ParseCsv(string.Empty, maxRows: 100);

        Assert.Empty(rows);
        Assert.False(truncated);
    }
}
