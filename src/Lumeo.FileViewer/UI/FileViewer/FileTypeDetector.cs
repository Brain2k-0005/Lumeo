using System.Collections.Generic;

namespace Lumeo;

/// <summary>
/// Pure-logic helpers for inferring <see cref="FileKind"/> from a URL / file
/// name extension or from an HTTP <c>Content-Type</c>. No I/O — callers do the
/// HEAD request and pass the value in.
/// </summary>
public static class FileTypeDetector
{
    // Extension → kind. Listed lower-case; lookup is case-insensitive.
    // Curated to avoid false positives on the boundary kinds (.txt vs .log:
    // both are Text; .yaml is Code via highlighter rather than Text so the
    // CodeEditor renders syntax instead of a plain <pre>).
    public static readonly IReadOnlyDictionary<string, FileKind> ByExtension = new Dictionary<string, FileKind>(StringComparer.OrdinalIgnoreCase)
    {
        // PDF
        [".pdf"] = FileKind.Pdf,
        // Images
        [".png"] = FileKind.Image, [".jpg"] = FileKind.Image, [".jpeg"] = FileKind.Image,
        [".gif"] = FileKind.Image, [".webp"] = FileKind.Image, [".avif"] = FileKind.Image,
        [".bmp"] = FileKind.Image, [".ico"] = FileKind.Image, [".svg"] = FileKind.Image,
        [".heic"] = FileKind.Image, [".heif"] = FileKind.Image,
        // Video
        [".mp4"] = FileKind.Video, [".webm"] = FileKind.Video, [".ogv"] = FileKind.Video,
        [".mov"] = FileKind.Video, [".m4v"] = FileKind.Video,
        // Audio
        [".mp3"] = FileKind.Audio, [".wav"] = FileKind.Audio, [".ogg"] = FileKind.Audio,
        [".oga"] = FileKind.Audio, [".m4a"] = FileKind.Audio, [".flac"] = FileKind.Audio,
        [".aac"] = FileKind.Audio,
        // Markdown
        [".md"] = FileKind.Markdown, [".markdown"] = FileKind.Markdown, [".mdx"] = FileKind.Markdown,
        // CSV / TSV
        [".csv"] = FileKind.Csv, [".tsv"] = FileKind.Csv,
        // JSON
        [".json"] = FileKind.Json, [".jsonc"] = FileKind.Json, [".json5"] = FileKind.Json,
        // Code (handled by Lumeo.CodeEditor with detected language)
        [".cs"] = FileKind.Code, [".razor"] = FileKind.Code,
        [".ts"] = FileKind.Code, [".tsx"] = FileKind.Code,
        [".js"] = FileKind.Code, [".jsx"] = FileKind.Code, [".mjs"] = FileKind.Code, [".cjs"] = FileKind.Code,
        [".py"] = FileKind.Code, [".rb"] = FileKind.Code, [".go"] = FileKind.Code,
        [".rs"] = FileKind.Code, [".java"] = FileKind.Code, [".kt"] = FileKind.Code,
        [".swift"] = FileKind.Code, [".c"] = FileKind.Code, [".cpp"] = FileKind.Code,
        [".cc"] = FileKind.Code, [".cxx"] = FileKind.Code,
        [".h"] = FileKind.Code, [".hpp"] = FileKind.Code,
        [".php"] = FileKind.Code, [".sh"] = FileKind.Code, [".bash"] = FileKind.Code,
        [".zsh"] = FileKind.Code, [".fish"] = FileKind.Code,
        [".ps1"] = FileKind.Code, [".sql"] = FileKind.Code,
        [".yaml"] = FileKind.Code, [".yml"] = FileKind.Code, [".toml"] = FileKind.Code,
        [".xml"] = FileKind.Code, [".html"] = FileKind.Code, [".htm"] = FileKind.Code,
        [".css"] = FileKind.Code, [".scss"] = FileKind.Code, [".sass"] = FileKind.Code,
        [".less"] = FileKind.Code,
        [".dockerfile"] = FileKind.Code,
        // Text — last resort for human-readable, no syntax to highlight
        [".txt"] = FileKind.Text, [".log"] = FileKind.Text,
        [".ini"] = FileKind.Text, [".conf"] = FileKind.Text, [".cfg"] = FileKind.Text,
        [".env"] = FileKind.Text,
        // Office — Word / Excel / PowerPoint (OOXML + legacy binary). Not
        // renderable inline; FileViewer shows a graceful download fallback.
        [".doc"] = FileKind.Office, [".docx"] = FileKind.Office,
        [".xls"] = FileKind.Office, [".xlsx"] = FileKind.Office,
        [".ppt"] = FileKind.Office, [".pptx"] = FileKind.Office,
    };

    // Maps a Code-kind extension to the language token Lumeo.CodeEditor's
    // CodeMirror wrapper understands (see Lumeo.CodeEditor/wwwroot/js/code-editor.js).
    // Anything not listed falls back to "plaintext", which still gives a
    // reasonable monospace render without syntax colors.
    public static readonly IReadOnlyDictionary<string, string> CodeLanguageByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp", [".razor"] = "csharp",
        [".ts"] = "typescript", [".tsx"] = "typescript",
        [".js"] = "javascript", [".jsx"] = "javascript", [".mjs"] = "javascript", [".cjs"] = "javascript",
        [".py"] = "python",
        [".sql"] = "sql",
        [".html"] = "html", [".htm"] = "html", [".xml"] = "xml",
        [".css"] = "css", [".scss"] = "css", [".sass"] = "css", [".less"] = "css",
        [".md"] = "markdown", [".markdown"] = "markdown", [".mdx"] = "markdown",
        [".json"] = "json", [".jsonc"] = "json", [".json5"] = "json",
    };

    // MIME → kind for exact matches. Wildcards (image/*, video/*, audio/*,
    // text/*) are handled in DetectFromMime below.
    public static readonly IReadOnlyDictionary<string, FileKind> ByMime = new Dictionary<string, FileKind>(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = FileKind.Pdf,
        ["text/markdown"] = FileKind.Markdown,
        ["text/csv"] = FileKind.Csv,
        ["text/tab-separated-values"] = FileKind.Csv,
        ["application/json"] = FileKind.Json,
        ["application/ld+json"] = FileKind.Json,
        ["application/xml"] = FileKind.Code,
        ["text/xml"] = FileKind.Code,
        ["text/html"] = FileKind.Code,
        ["text/css"] = FileKind.Code,
        ["text/javascript"] = FileKind.Code,
        ["application/javascript"] = FileKind.Code,
        ["application/typescript"] = FileKind.Code,
        ["text/x-csharp"] = FileKind.Code,
        ["text/x-python"] = FileKind.Code,
        ["text/x-yaml"] = FileKind.Code,
        ["application/x-yaml"] = FileKind.Code,
        ["application/sql"] = FileKind.Code,
        // Office — OOXML
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = FileKind.Office,
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = FileKind.Office,
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = FileKind.Office,
        // Office — legacy binary
        ["application/msword"] = FileKind.Office,
        ["application/vnd.ms-excel"] = FileKind.Office,
        ["application/vnd.ms-powerpoint"] = FileKind.Office,
    };

    /// <summary>
    /// Strip query string and fragment from <paramref name="src"/>, then
    /// return the last path segment's lower-case extension (including the
    /// leading dot), or empty string if there is none.
    /// </summary>
    public static string ExtensionFor(string? src)
    {
        if (string.IsNullOrWhiteSpace(src)) return string.Empty;
        var s = src;
        var qIdx = s.IndexOfAny(new[] { '?', '#' });
        if (qIdx >= 0) s = s[..qIdx];
        // strip trailing slash (directory-style URLs)
        s = s.TrimEnd('/');
        var slash = s.LastIndexOf('/');
        var name = slash >= 0 ? s[(slash + 1)..] : s;
        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[dot..].ToLowerInvariant() : string.Empty;
    }

    /// <summary>Best-guess <see cref="FileKind"/> from a URL / file name's extension.</summary>
    public static FileKind DetectFromExtension(string? src)
    {
        var ext = ExtensionFor(src);
        if (ext.Length == 0) return FileKind.Unknown;
        return ByExtension.TryGetValue(ext, out var k) ? k : FileKind.Unknown;
    }

    /// <summary>
    /// Map an HTTP <c>Content-Type</c> header (with optional parameters) to a
    /// <see cref="FileKind"/>. Parameters like <c>;charset=utf-8</c> are
    /// stripped before lookup.
    /// </summary>
    public static FileKind DetectFromMime(string? mime)
    {
        if (string.IsNullOrWhiteSpace(mime)) return FileKind.Unknown;
        var bare = mime.Split(';', 2)[0].Trim();
        if (ByMime.TryGetValue(bare, out var k)) return k;
        if (bare.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return FileKind.Image;
        if (bare.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return FileKind.Video;
        if (bare.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return FileKind.Audio;
        if (bare.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return FileKind.Text;
        return FileKind.Unknown;
    }

    /// <summary>
    /// CodeMirror language token for a code file, derived from its extension.
    /// Returns <c>"plaintext"</c> for anything not in
    /// <see cref="CodeLanguageByExtension"/>.
    /// </summary>
    public static string CodeLanguageFor(string? src)
    {
        var ext = ExtensionFor(src);
        return CodeLanguageByExtension.TryGetValue(ext, out var lang) ? lang : "plaintext";
    }
}
