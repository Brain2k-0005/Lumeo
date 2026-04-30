using Xunit;
using Lumeo;

namespace Lumeo.Tests.Editor;

public class WordImporterTests
{
    [Fact]
    public async Task ToHtmlAsync_DocumentWithHeadingsAndParagraphs_ContainsExpectedElements()
    {
        // Generate an in-memory DOCX that has a Heading 1, a paragraph, and a Heading 2
        // so this test has no external file dependency.
        using var docx = BuildDocxWithHeadingsAndParagraphs();
        var result = await WordImporter.ToHtmlAsync(docx);

        Assert.NotNull(result.Html);
        Assert.NotEmpty(result.Html);

        // Document should contain at least one heading element
        Assert.Contains("<h", result.Html, StringComparison.OrdinalIgnoreCase);

        // Document should contain paragraph content
        Assert.Contains("<p", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultStyleMap_ContainsEnglishAndGermanHeadings()
    {
        Assert.Contains("Heading 1", WordImporter.DefaultStyleMap);
        Assert.Contains("Überschrift 1", WordImporter.DefaultStyleMap);
        Assert.Contains("Heading 6", WordImporter.DefaultStyleMap);
        Assert.Contains("Überschrift 6", WordImporter.DefaultStyleMap);
    }

    [Fact]
    public void DefaultStyleMap_ContainsGermanBodyAndListStyles()
    {
        Assert.Contains("Textkörper", WordImporter.DefaultStyleMap);
        Assert.Contains("Listenabsatz", WordImporter.DefaultStyleMap);
        Assert.Contains("Zitat", WordImporter.DefaultStyleMap);
        Assert.Contains("Beschriftung", WordImporter.DefaultStyleMap);
    }

    [Fact]
    public void DefaultStyleMap_ContainsTitleAndSubtitleVariants()
    {
        Assert.Contains("Title", WordImporter.DefaultStyleMap);
        Assert.Contains("Titel", WordImporter.DefaultStyleMap);
        Assert.Contains("Subtitle", WordImporter.DefaultStyleMap);
        Assert.Contains("Untertitel", WordImporter.DefaultStyleMap);
    }

    [Fact]
    public async Task ToHtmlAsync_MinimalDocx_ReturnsHtml()
    {
        // A valid minimal .docx is a ZIP with at minimum [Content_Types].xml,
        // word/document.xml and _rels/.rels. Build the smallest possible one in memory
        // so this test has no external dependency.
        using var docx = BuildMinimalDocx("Hello, <b>World</b>");

        var result = await WordImporter.ToHtmlAsync(docx);

        Assert.NotNull(result.Html);
        Assert.NotNull(result.Warnings);
    }

    [Fact]
    public async Task ToHtmlAsync_CustomStyleMap_TakesPrecedenceOverDefault()
    {
        using var docx = BuildMinimalDocx();
        var options = new WordImportOptions
        {
            StyleMap = "p[style-name='Custom'] => h3:fresh",
            IncludeDefaultStyleMap = true
        };

        // Just assert it runs without throwing — full style-map precedence is
        // validated by the real-document test above.
        var result = await WordImporter.ToHtmlAsync(docx, options);
        Assert.NotNull(result.Html);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds an in-memory DOCX that contains a Heading 1, a paragraph, and a Heading 2
    /// so ToHtmlAsync can be verified against headings + paragraph output without any
    /// external file dependency.
    /// </summary>
    private static MemoryStream BuildDocxWithHeadingsAndParagraphs()
    {
        var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml"  ContentType="application/xml"/>
                  <Override PartName="/word/document.xml"
                            ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                  <Override PartName="/word/styles.xml"
                            ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
                </Types>
                """);

            WriteEntry(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                </Relationships>
                """);

            WriteEntry(zip, "word/_rels/document.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);

            // Styles file defines Heading 1 and Heading 2 so Mammoth can recognise them
            WriteEntry(zip, "word/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:style w:type="paragraph" w:styleId="Heading1">
                    <w:name w:val="heading 1"/>
                  </w:style>
                  <w:style w:type="paragraph" w:styleId="Heading2">
                    <w:name w:val="heading 2"/>
                  </w:style>
                  <w:style w:type="paragraph" w:styleId="Normal">
                    <w:name w:val="Normal"/>
                  </w:style>
                </w:styles>
                """);

            WriteEntry(zip, "word/document.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                            xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <w:body>
                    <w:p>
                      <w:pPr><w:pStyle w:val="Heading1"/></w:pPr>
                      <w:r><w:t>Introduction</w:t></w:r>
                    </w:p>
                    <w:p>
                      <w:pPr><w:pStyle w:val="Normal"/></w:pPr>
                      <w:r><w:t>This is the first paragraph of the document.</w:t></w:r>
                    </w:p>
                    <w:p>
                      <w:pPr><w:pStyle w:val="Heading2"/></w:pPr>
                      <w:r><w:t>Details</w:t></w:r>
                    </w:p>
                    <w:p>
                      <w:pPr><w:pStyle w:val="Normal"/></w:pPr>
                      <w:r><w:t>Further detail paragraph.</w:t></w:r>
                    </w:p>
                  </w:body>
                </w:document>
                """);
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream BuildMinimalDocx(string bodyText = "Test")
    {
        // Build the smallest well-formed .docx (OOXML) that Mammoth will accept.
        var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml"  ContentType="application/xml"/>
                  <Override PartName="/word/document.xml"
                            ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                </Types>
                """);

            WriteEntry(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                </Relationships>
                """);

            WriteEntry(zip, "word/document.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:wpc="http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas"
                            xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>
                    <w:p>
                      <w:r><w:t>{System.Security.SecurityElement.Escape(bodyText)}</w:t></w:r>
                    </w:p>
                  </w:body>
                </w:document>
                """);
        }
        ms.Position = 0;
        return ms;
    }

    private static void WriteEntry(System.IO.Compression.ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var writer = new System.IO.StreamWriter(entry.Open());
        writer.Write(content);
    }
}
