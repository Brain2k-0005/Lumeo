using System.Text;
using System.Text.RegularExpressions;

namespace Lumeo.Internal;

/// <summary>
/// Minimal, dependency-free CommonMark subset → HTML converter used as the
/// built-in fallback renderer for <c>StreamingText</c> (and any other component
/// that wants light markdown without pulling in Markdig). Consumers that need
/// full CommonMark / GFM should supply their own renderer via the component's
/// <c>MarkdownRenderer</c> hook — this is deliberately small.
///
/// Security: the source is HTML-escaped FIRST, then a fixed set of inline/block
/// patterns reintroduce a known-safe tag allow-list (p, br, strong, em, code,
/// pre, a, ul/ol/li, h1–h6, blockquote). No raw HTML from the input survives,
/// and emitted <c>&lt;a&gt;</c> hrefs are restricted to http(s)/mailto/relative
/// so a <c>javascript:</c> URL can't slip through.
/// </summary>
internal static class LumeoMarkdown
{
    public static string ToHtml(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;

        // Normalise newlines, then escape everything up front.
        var text = markdown.Replace("\r\n", "\n").Replace('\r', '\n');

        var lines = text.Split('\n');
        var sb = new StringBuilder();

        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];

            // Fenced code block ``` … ```
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                var code = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    code.Append(Escape(lines[i])).Append('\n');
                    i++;
                }
                i++; // skip closing fence (or run off the end)
                sb.Append("<pre><code>").Append(code.ToString().TrimEnd('\n')).Append("</code></pre>");
                continue;
            }

            // Blank line — paragraph separator, skip.
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // ATX heading: #..###### text
            var heading = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
            if (heading.Success)
            {
                var level = heading.Groups[1].Value.Length;
                sb.Append($"<h{level}>").Append(Inline(heading.Groups[2].Value)).Append($"</h{level}>");
                i++;
                continue;
            }

            // Blockquote: one or more consecutive "> " lines.
            if (line.TrimStart().StartsWith(">", StringComparison.Ordinal))
            {
                var quote = new StringBuilder();
                while (i < lines.Length && lines[i].TrimStart().StartsWith(">", StringComparison.Ordinal))
                {
                    var content = Regex.Replace(lines[i].TrimStart(), @"^>\s?", string.Empty);
                    quote.Append(Inline(content)).Append(' ');
                    i++;
                }
                sb.Append("<blockquote>").Append(quote.ToString().TrimEnd()).Append("</blockquote>");
                continue;
            }

            // Unordered list: -, *, + markers.
            if (Regex.IsMatch(line, @"^\s*[-*+]\s+"))
            {
                sb.Append("<ul>");
                while (i < lines.Length && Regex.IsMatch(lines[i], @"^\s*[-*+]\s+"))
                {
                    var item = Regex.Replace(lines[i], @"^\s*[-*+]\s+", string.Empty);
                    sb.Append("<li>").Append(Inline(item)).Append("</li>");
                    i++;
                }
                sb.Append("</ul>");
                continue;
            }

            // Ordered list: 1. 2. …
            if (Regex.IsMatch(line, @"^\s*\d+\.\s+"))
            {
                sb.Append("<ol>");
                while (i < lines.Length && Regex.IsMatch(lines[i], @"^\s*\d+\.\s+"))
                {
                    var item = Regex.Replace(lines[i], @"^\s*\d+\.\s+", string.Empty);
                    sb.Append("<li>").Append(Inline(item)).Append("</li>");
                    i++;
                }
                sb.Append("</ol>");
                continue;
            }

            // Paragraph: gather consecutive non-blank, non-block lines, joining
            // soft line breaks with <br>.
            var para = new StringBuilder();
            while (i < lines.Length
                   && !string.IsNullOrWhiteSpace(lines[i])
                   && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal)
                   && !Regex.IsMatch(lines[i], @"^(#{1,6})\s+")
                   && !lines[i].TrimStart().StartsWith(">", StringComparison.Ordinal)
                   && !Regex.IsMatch(lines[i], @"^\s*[-*+]\s+")
                   && !Regex.IsMatch(lines[i], @"^\s*\d+\.\s+"))
            {
                if (para.Length > 0) para.Append("<br>");
                para.Append(Inline(lines[i]));
                i++;
            }
            sb.Append("<p>").Append(para).Append("</p>");
        }

        return sb.ToString();
    }

    // Inline formatting applied to already-block-split text. Inline code spans
    // are extracted FIRST (and their content escaped) so emphasis markers inside
    // them are treated literally, then the surrounding text is escaped and
    // emphasis/links applied.
    private static string Inline(string raw)
    {
        var sb = new StringBuilder();
        var pos = 0;
        foreach (Match m in Regex.Matches(raw, "`([^`]+)`"))
        {
            sb.Append(EmphasisAndLinks(Escape(raw.Substring(pos, m.Index - pos))));
            sb.Append("<code>").Append(Escape(m.Groups[1].Value)).Append("</code>");
            pos = m.Index + m.Length;
        }
        sb.Append(EmphasisAndLinks(Escape(raw.Substring(pos))));
        return sb.ToString();
    }

    // Operates on ESCAPED text: the markdown markers (*, _, [ ]( )) are plain
    // ASCII and survive escaping, while any literal < > & from the source are
    // already neutralised, so reintroducing tags here is safe.
    private static string EmphasisAndLinks(string escaped)
    {
        // Links: [text](href) — validate the scheme.
        escaped = Regex.Replace(escaped, @"\[([^\]]+)\]\(([^)\s]+)\)", m =>
        {
            var label = m.Groups[1].Value;
            var href = m.Groups[2].Value;
            if (!IsSafeHref(href)) return label; // drop unsafe URL, keep the visible label
            // Shield emphasis markers inside the URL so the * / _ passes below
            // don't rewrite the href (e.g. /a_b_c → /a<em>b</em>c). The numeric
            // entities decode back to the literal characters in the browser, so
            // the link still resolves.
            var hrefAttr = href.Replace("_", "&#95;").Replace("*", "&#42;");
            return $"<a href=\"{hrefAttr}\">{label}</a>";
        });

        // Bold (**x** / __x__) before italic so the inner single-marker pass
        // doesn't consume the doubled markers.
        escaped = Regex.Replace(escaped, @"\*\*([^*]+)\*\*", "<strong>$1</strong>");
        escaped = Regex.Replace(escaped, @"__([^_]+)__", "<strong>$1</strong>");

        // Italic (*x* / _x_).
        escaped = Regex.Replace(escaped, @"\*([^*]+)\*", "<em>$1</em>");
        escaped = Regex.Replace(escaped, @"_([^_]+)_", "<em>$1</em>");

        return escaped;
    }

    private static bool IsSafeHref(string href)
    {
        // The href was HTML-escaped, so ':' is intact; check the scheme.
        if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return true;
        if (href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return true;
        if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return true;
        // Relative / fragment / query links are fine; anything with a scheme we
        // don't recognise (javascript:, data:, …) is rejected.
        return !href.Contains(':');
    }

    private static string Escape(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");
}
