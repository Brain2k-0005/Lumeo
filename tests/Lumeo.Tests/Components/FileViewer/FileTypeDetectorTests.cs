using Xunit;

namespace Lumeo.Tests.Components.FileViewerComponent;

public class FileTypeDetectorTests
{
    [Theory]
    // PDF
    [InlineData("https://cdn.example.com/report.pdf", FileKind.Pdf)]
    // Images
    [InlineData("avatar.png", FileKind.Image)]
    [InlineData("photo.JPG", FileKind.Image)]
    [InlineData("vector.svg", FileKind.Image)]
    [InlineData("modern.webp", FileKind.Image)]
    // Video / audio
    [InlineData("/clips/demo.mp4", FileKind.Video)]
    [InlineData("song.flac", FileKind.Audio)]
    // Markdown / CSV / JSON
    [InlineData("README.md", FileKind.Markdown)]
    [InlineData("data.csv", FileKind.Csv)]
    [InlineData("payload.json", FileKind.Json)]
    // Code
    [InlineData("Program.cs", FileKind.Code)]
    [InlineData("app.tsx", FileKind.Code)]
    [InlineData("query.sql", FileKind.Code)]
    [InlineData("script.py", FileKind.Code)]
    // Text fallbacks
    [InlineData("debug.log", FileKind.Text)]
    [InlineData("app.txt", FileKind.Text)]
    // Unknown
    [InlineData("archive.zip", FileKind.Unknown)]
    [InlineData("/folder/", FileKind.Unknown)]
    [InlineData("", FileKind.Unknown)]
    public void DetectFromExtension_Maps_Known_Extensions(string src, FileKind expected)
    {
        Assert.Equal(expected, FileTypeDetector.DetectFromExtension(src));
    }

    [Theory]
    // Path segment has no dot → must NOT mistakenly read "?download" or
    // "#frag" as the extension.
    [InlineData("https://api.example.com/files/secret?download=true&token=abc#frag")]
    [InlineData("/path/to/file#section-1")]
    public void DetectFromExtension_Ignores_Query_And_Fragment_When_No_Extension(string src)
    {
        Assert.Equal(FileKind.Unknown, FileTypeDetector.DetectFromExtension(src));
    }

    [Theory]
    // Path segment HAS an extension; query/fragment after it must be stripped.
    [InlineData("doc.pdf?token=abc", FileKind.Pdf)]
    [InlineData("image.png#preview", FileKind.Image)]
    [InlineData("data.json?v=2&pretty=true", FileKind.Json)]
    public void DetectFromExtension_Strips_Query_And_Fragment(string src, FileKind expected)
    {
        Assert.Equal(expected, FileTypeDetector.DetectFromExtension(src));
    }

    [Fact]
    public void DetectFromExtension_Uses_Final_Segment_Even_With_Dotted_Path()
    {
        // The .com in the host must not be treated as the extension.
        Assert.Equal(FileKind.Pdf, FileTypeDetector.DetectFromExtension("https://files.example.com/quarterly.pdf"));
    }

    [Theory]
    [InlineData("application/pdf", FileKind.Pdf)]
    [InlineData("application/pdf; charset=binary", FileKind.Pdf)]
    [InlineData("image/png", FileKind.Image)]
    [InlineData("image/svg+xml", FileKind.Image)]
    [InlineData("video/mp4", FileKind.Video)]
    [InlineData("audio/mpeg", FileKind.Audio)]
    [InlineData("text/markdown", FileKind.Markdown)]
    [InlineData("text/csv", FileKind.Csv)]
    [InlineData("application/json", FileKind.Json)]
    [InlineData("application/ld+json", FileKind.Json)]
    [InlineData("text/html", FileKind.Code)]
    [InlineData("text/plain", FileKind.Text)]
    [InlineData("text/x-readme", FileKind.Text)]
    [InlineData(null, FileKind.Unknown)]
    [InlineData("", FileKind.Unknown)]
    [InlineData("application/octet-stream", FileKind.Unknown)]
    public void DetectFromMime_Maps_Common_Types(string? mime, FileKind expected)
    {
        Assert.Equal(expected, FileTypeDetector.DetectFromMime(mime));
    }

    [Theory]
    [InlineData("File.cs", "csharp")]
    [InlineData("page.razor", "csharp")]
    [InlineData("app.ts", "typescript")]
    [InlineData("app.tsx", "typescript")]
    [InlineData("server.py", "python")]
    [InlineData("query.sql", "sql")]
    [InlineData("style.scss", "css")]
    [InlineData("doc.md", "markdown")]
    [InlineData("data.json", "json")]
    [InlineData("Dockerfile", "plaintext")]   // no extension → plaintext
    [InlineData("README.go", "plaintext")]    // .go not in language map → plaintext
    [InlineData(null, "plaintext")]
    [InlineData("", "plaintext")]
    public void CodeLanguageFor_Maps_Extensions_To_CodeMirror_Tokens(string? src, string expected)
    {
        Assert.Equal(expected, FileTypeDetector.CodeLanguageFor(src));
    }

    [Theory]
    [InlineData("report.pdf", ".pdf")]
    [InlineData("REPORT.PDF", ".pdf")]                  // lower-cased
    [InlineData("photo.PNG?signature=abc", ".png")]
    [InlineData("/path/to/file", "")]
    [InlineData("https://a.b/c.d/e.f", ".f")]
    [InlineData(null, "")]
    public void ExtensionFor_Strips_Query_And_LowerCases(string? src, string expected)
    {
        Assert.Equal(expected, FileTypeDetector.ExtensionFor(src));
    }
}
