using System.Text;
using System.Text.RegularExpressions;

namespace Lumeo.Cli;

/// <summary>
/// Downloads Google Fonts into the consumer project so they can be served from
/// the same origin as the app — like Next.js' <c>next/font</c> or shadcn's CLI.
/// Zero runtime dependency on fonts.googleapis.com: privacy-respecting, cacheable
/// alongside your assets, works offline after install.
///
/// Flow:
///   1. Fetch the Google Fonts CSS with a modern-browser User-Agent so the
///      response uses woff2 (smaller than ttf, universally supported).
///   2. Extract every @font-face block from the CSS.
///   3. For each src URL, download the binary into <c>wwwroot/fonts/&lt;id&gt;/</c>.
///   4. Rewrite the CSS with relative local URLs and save as
///      <c>wwwroot/fonts/&lt;id&gt;/&lt;id&gt;.css</c>.
///   5. Return the relative path so callers can wire it into theme.js /
///      lumeo-theme.json.
/// </summary>
internal static class FontInstaller
{
    // Google Fonts URLs mapped by customizer id. Keep in sync with theme.js's
    // lumeoFontMap and LumeoPresetOptions.Fonts.
    private static readonly Dictionary<string, string> GoogleFontsCssUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["inter"] = "https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap",
        ["geist"] = "https://fonts.googleapis.com/css2?family=Geist:wght@400;500;600;700&display=swap",
        ["ibm-plex-sans"] = "https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600;700&display=swap",
        ["jetbrains-mono"] = "https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500;700&display=swap",
        ["fira-code"] = "https://fonts.googleapis.com/css2?family=Fira+Code:wght@400;500;700&display=swap",
    };

    // Regex: matches Google's `src: url(...) format('woff2')` tuples.
    private static readonly Regex SrcUrlRegex = new(@"url\((https://fonts\.gstatic\.com/[^)]+)\)", RegexOptions.Compiled);

    /// <summary>Downloads a font into wwwroot/fonts/&lt;id&gt;/ and returns the path
    /// to the local CSS file (relative to wwwroot). Returns null on failure.</summary>
    public static async Task<string?> InstallAsync(string fontId, string wwwroot, HttpClient http, bool silent)
    {
        if (!GoogleFontsCssUrls.TryGetValue(fontId, out var cssUrl))
        {
            Console.Error.WriteLine(Ansi.Yellow($"! Unknown font id '{fontId}' — no Google Fonts URL mapped."));
            return null;
        }

        var fontDir = Path.Combine(wwwroot, "fonts", fontId);
        Directory.CreateDirectory(fontDir);

        // Fetch the CSS with a modern-browser UA so Google serves woff2 URLs.
        string css;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, cssUrl);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            var res = await http.SendAsync(req);
            res.EnsureSuccessStatusCode();
            css = await res.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(Ansi.Yellow($"! Failed to fetch Google Fonts CSS for '{fontId}': {ex.Message}"));
            return null;
        }

        // Walk the CSS, replace each remote url(...) with a local one, and download
        // the binary on the side. Using a Replace-with-delegate keeps the rewrite
        // and the downloads paired 1:1 so we don't orphan stale files.
        var rewritten = new StringBuilder();
        int cursor = 0;
        int downloaded = 0;
        var matches = SrcUrlRegex.Matches(css);
        foreach (Match m in matches)
        {
            rewritten.Append(css, cursor, m.Index - cursor);
            var remoteUrl = m.Groups[1].Value;
            var fileName = Path.GetFileName(new Uri(remoteUrl).AbsolutePath);
            var localPath = Path.Combine(fontDir, fileName);
            if (!File.Exists(localPath))
            {
                try
                {
                    var bytes = await http.GetByteArrayAsync(remoteUrl);
                    await File.WriteAllBytesAsync(localPath, bytes);
                    downloaded++;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(Ansi.Yellow($"! Failed to download {fileName}: {ex.Message}"));
                    return null;
                }
            }
            // Local URL is simply the filename — the CSS and the woff2 live in the
            // same directory, so a bare filename resolves relatively at runtime.
            rewritten.Append($"url({fileName})");
            cursor = m.Index + m.Length;
        }
        rewritten.Append(css, cursor, css.Length - cursor);

        var cssOutPath = Path.Combine(fontDir, $"{fontId}.css");
        await File.WriteAllTextAsync(cssOutPath, rewritten.ToString());

        if (!silent)
        {
            var rel = Path.Combine("wwwroot", "fonts", fontId, $"{fontId}.css").Replace('\\', '/');
            Console.WriteLine(Ansi.Green("  fonts      ") + rel + Ansi.Dim($" ({downloaded} file{(downloaded == 1 ? "" : "s")} downloaded)"));
        }

        // Return the path that a <link rel="stylesheet" href="..."> in index.html
        // would use — i.e. relative to wwwroot.
        return $"fonts/{fontId}/{fontId}.css";
    }
}
