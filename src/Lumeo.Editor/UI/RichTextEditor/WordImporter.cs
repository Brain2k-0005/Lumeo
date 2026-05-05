namespace Lumeo;

/// <summary>Result of a Word document import.</summary>
public sealed record WordImportResult(string Html, IReadOnlyList<string> Warnings);

/// <summary>Options for <see cref="WordImporter.ToHtmlAsync"/>.</summary>
public sealed class WordImportOptions
{
    /// <summary>Custom style map (Mammoth syntax). When null, <see cref="WordImporter.DefaultStyleMap"/> is used.</summary>
    public string? StyleMap { get; init; }

    /// <summary>
    /// Optional callback to handle embedded images.
    /// Receives (contentType, imageStream) and returns the URL to use as the img src.
    /// When null, images are inlined as base64 data URIs (Mammoth default).
    /// </summary>
    public Func<string, Stream, ValueTask<string>>? ConvertImage { get; init; }

    /// <summary>
    /// When true (default) and <see cref="StyleMap"/> is also set,
    /// the default style map is prepended so custom rules take precedence.
    /// </summary>
    public bool IncludeDefaultStyleMap { get; init; } = true;
}

/// <summary>
/// Server-side helper that converts a .docx stream to clean HTML using Mammoth.NET.
/// Call from an HTTP endpoint; consumers wire the result into the editor via
/// <c>WordImportRequest.SetHtml</c>.
/// </summary>
public static class WordImporter
{
    /// <summary>
    /// Decodes a base64 string into a byte array, but rejects payloads whose
    /// decoded length would exceed <paramref name="maxBytes"/> BEFORE calling
    /// <see cref="Convert.FromBase64String"/> — so an attacker can't force a
    /// large allocation by submitting an oversized base64 blob.
    /// </summary>
    /// <exception cref="WordImportSizeException">When the decoded payload would exceed the limit.</exception>
    public static byte[] DecodeBase64WithLimit(string base64, long maxBytes)
    {
        if (base64 is null) throw new ArgumentNullException(nameof(base64));
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));

        // Compute decoded length without allocating: every 4 base64 chars = 3 bytes,
        // minus 1 byte per '=' padding char. Whitespace would skew this; we strip it
        // up-front since Convert.FromBase64String tolerates it but we need a true count.
        var sanitized = StripBase64Whitespace(base64);
        var paddingChars = 0;
        for (var i = sanitized.Length - 1; i >= 0 && sanitized[i] == '='; i--) paddingChars++;
        var decodedLength = ((long)(sanitized.Length - paddingChars) * 3L) / 4L;

        if (decodedLength > maxBytes)
        {
            throw new WordImportSizeException(decodedLength, maxBytes);
        }

        return Convert.FromBase64String(sanitized);
    }

    private static string StripBase64Whitespace(string s)
    {
        // Fast path: no whitespace.
        var hasWs = false;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == ' ' || c == '\r' || c == '\n' || c == '\t') { hasWs = true; break; }
        }
        if (!hasWs) return s;

        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c != ' ' && c != '\r' && c != '\n' && c != '\t') sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Default style map covering English and German Word built-in styles
    /// (Title/Titel, Heading 1-6/Überschrift 1-6, Body Text/Textkörper,
    /// List Paragraph/Listenabsatz, Quote/Zitat, Caption/Beschriftung,
    /// Strong/Emphasis runs).
    /// </summary>
    public const string DefaultStyleMap = """
        p[style-name='Title']          => h1.doc-title:fresh
        p[style-name='Titel']          => h1.doc-title:fresh
        p[style-name='Subtitle']       => h2.doc-subtitle:fresh
        p[style-name='Untertitel']     => h2.doc-subtitle:fresh
        p[style-name='Heading 1']      => h1:fresh
        p[style-name='Überschrift 1']  => h1:fresh
        p[style-name='Heading 2']      => h2:fresh
        p[style-name='Überschrift 2']  => h2:fresh
        p[style-name='Heading 3']      => h3:fresh
        p[style-name='Überschrift 3']  => h3:fresh
        p[style-name='Heading 4']      => h4:fresh
        p[style-name='Überschrift 4']  => h4:fresh
        p[style-name='Heading 5']      => h5:fresh
        p[style-name='Überschrift 5']  => h5:fresh
        p[style-name='Heading 6']      => h6:fresh
        p[style-name='Überschrift 6']  => h6:fresh
        p[style-name='Body Text']      => p:fresh
        p[style-name='Textkörper']     => p:fresh
        p[style-name='List Paragraph'] => li:fresh
        p[style-name='Listenabsatz']   => li:fresh
        p[style-name='Quote']          => blockquote:fresh
        p[style-name='Zitat']          => blockquote:fresh
        p[style-name='Caption']        => p.caption:fresh
        p[style-name='Beschriftung']   => p.caption:fresh
        r[style-name='Strong']         => strong
        r[style-name='Emphasis']       => em
        """;

    /// <summary>
    /// Converts a .docx stream to HTML.
    /// Runs Mammoth on a thread-pool thread so the calling thread is not blocked.
    /// </summary>
    /// <param name="docxStream">Readable stream containing the .docx bytes.</param>
    /// <param name="options">Conversion options; pass null to use all defaults.</param>
    public static async ValueTask<WordImportResult> ToHtmlAsync(
        Stream docxStream,
        WordImportOptions? options = null)
    {
        options ??= new WordImportOptions();

        // Build the effective style map: default + custom (custom wins because
        // Mammoth gives highest precedence to the last-added style map).
        var styleMap = options switch
        {
            { StyleMap: not null, IncludeDefaultStyleMap: true } => DefaultStyleMap + "\n" + options.StyleMap,
            { StyleMap: not null, IncludeDefaultStyleMap: false } => options.StyleMap,
            _ => DefaultStyleMap
        };

        // Capture the image callback so we can reference it inside Task.Run.
        var imageCallback = options.ConvertImage;

        var result = await Task.Run(() =>
        {
            var converter = new Mammoth.DocumentConverter()
                .AddStyleMap(styleMap);

            if (imageCallback is not null)
            {
                // Mammoth's ImageConverter is sync-only. We bridge async → sync by
                // buffering the image stream (already fully loaded in memory by Mammoth)
                // and blocking on the async callback. This runs on a thread-pool thread
                // so blocking here is safe.
                converter = converter.ImageConverter(image =>
                {
                    using var stream = image.GetStream();
                    using var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    buffer.Position = 0;

                    var url = imageCallback(image.ContentType, buffer)
                        .AsTask()
                        .GetAwaiter()
                        .GetResult();

                    return new Dictionary<string, string> { { "src", url } };
                });
            }

            return converter.ConvertToHtml(docxStream);
        });

        return new WordImportResult(result.Value, result.Warnings.ToArray());
    }
}
